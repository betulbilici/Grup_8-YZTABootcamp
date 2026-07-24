using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CvInterviewPlatform.Web.Services
{
    // CV ve profil fotoğrafı dosyalarını Cloudflare R2'de (S3 uyumlu API) saklıyoruz.
    // R2 herkese açık değil; dosyalar sadece süreli, imzalı (presigned) URL ile açılabiliyor.
    public class CvStorageService
    {
        private readonly AmazonS3Client _client;
        private readonly string _bucketName;
        private readonly ILogger<CvStorageService> _logger;

        public CvStorageService(IConfiguration configuration, ILogger<CvStorageService> logger)
        {
            _logger = logger;
            _bucketName = configuration["R2:BucketName"] ?? "";
            string accountId = configuration["R2:AccountId"] ?? "";
            string accessKey = configuration["R2:AccessKeyId"] ?? "";
            string secretKey = configuration["R2:SecretAccessKey"] ?? "";

            var config = new AmazonS3Config
            {
                ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
                ForcePathStyle = true
            };
            _client = new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), config);
        }

        public async Task UploadAsync(Stream fileStream, string objectKey, string contentType)
        {
            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                InputStream = fileStream,
                ContentType = contentType,
                // R2, AWS SDK'nın varsayılan "streaming" (chunked) payload imzalamasını
                // desteklemiyor — Cloudflare'in resmi .NET örneği bu ikisini kapatmayı
                // zorunlu kılıyor, yoksa istek TLS seviyesinde reddediliyor.
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true
            };
            await _client.PutObjectAsync(request);
            _logger.LogInformation($"Uploaded object to R2: {objectKey}");
        }

        // İmzalama tamamen yerelde (HMAC) yapılıyor, R2'ye ağ çağrısı gitmiyor —
        // bu yüzden her sayfa render'ında çağırmak (ör. avatar/CV linki için) güvenli.
        public string GetPresignedUrl(string objectKey, int expiryMinutes = 60)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                Expires = DateTime.UtcNow.AddMinutes(expiryMinutes)
            };
            return _client.GetPreSignedURL(request);
        }

        // Geriye dönük uyumluluk: R2'ye geçmeden önce yüklenmiş dosyalar Firestore'da
        // hâlâ eski yerel yol biçimini (/uploads/...) taşıyor — bunlar olduğu gibi
        // döndürülüyor, yeni yüklemelerden gelen R2 obje anahtarları ise presigned
        // URL'e çevriliyor.
        public string? ResolvePreviewUrl(string? storedValue)
        {
            if (string.IsNullOrEmpty(storedValue)) return null;
            if (storedValue.StartsWith("/uploads/")) return storedValue;
            return GetPresignedUrl(storedValue);
        }
    }
}
