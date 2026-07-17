using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CvInterviewPlatform.Web.Services
{
    public class CvParserService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<CvParserService> _logger;
        private readonly string _baseUrl;

        public CvParserService(HttpClient httpClient, IConfiguration configuration, ILogger<CvParserService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            // İlk model yüklemesini de hesaba katarak docling işlemi için zaman aşımı süresini 120 saniyeye ayarlıyoruz
            _httpClient.Timeout = TimeSpan.FromSeconds(120);

            var url = configuration["ParserService:BaseUrl"] ?? "http://127.0.0.1:8000";
            _baseUrl = url.EndsWith("/") ? url : url + "/";
        }

        public async Task<string> ParsePdfAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                _logger.LogError($"CV file not found at path: {filePath}");
                return string.Empty;
            }

            try
            {
                _logger.LogInformation($"Sending CV to parser service: {filePath}");

                using var form = new MultipartFormDataContent();
                using var fileStream = File.OpenRead(filePath);
                using var streamContent = new StreamContent(fileStream);
                
                // Dosyayı güvenli bir ASCII dosya adı kullanarak çok parçalı form verisine (multipart form data) ekliyoruz
                form.Add(streamContent, "file", "cv_upload.pdf");

                // FastAPI ayrıştırıcı servisine (parser service) istek gönderiyoruz
                var response = await _httpClient.PostAsync(_baseUrl + "parse", form);
                
                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Parser service returned error code {response.StatusCode}: {errorContent}");
                    return string.Empty;
                }

                string jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);
                
                if (doc.RootElement.TryGetProperty("markdown", out var markdownProp))
                {
                    string extractedText = markdownProp.GetString() ?? string.Empty;
                    _logger.LogInformation($"Successfully parsed CV using microservice. Extracted {extractedText.Length} characters.");
                    return extractedText;
                }

                _logger.LogWarning("Parser service returned success but the response did not contain 'markdown' property.");
                return string.Empty;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"HTTP error occurred while calling parser service: {ex.Message}. Make sure FastAPI microservice is running on port 8000.");
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error while parsing CV: {ex.Message}");
                return string.Empty;
            }
        }
    }
}
