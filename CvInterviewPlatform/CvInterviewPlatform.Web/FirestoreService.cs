using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace CvInterviewPlatform.Web
{
    public class FirestoreService
    {
        private readonly FirestoreDb _db;

        public FirestoreService(IConfiguration configuration)
        {
            string projectId = configuration["Firestore:ProjectId"]
                ?? Environment.GetEnvironmentVariable("FIRESTORE_PROJECT_ID")
                ?? "cv-interview-platform-prod";

            // Deploy ortamında container'a elle dosya bırakmak pratik değil,
            // bu yüzden servis hesabı anahtarının JSON içeriği tek bir env
            // değişkeninden (FIRESTORE_CREDENTIALS_JSON) okunabiliyor. Yerel
            // geliştirmede bu değişken tanımlı değilse eskisi gibi proje
            // kökündeki firebase-key.json dosyasına düşülüyor.
            string? credentialsJson = configuration["Firestore:CredentialsJson"]
                ?? Environment.GetEnvironmentVariable("FIRESTORE_CREDENTIALS_JSON");

            GoogleCredential credential = !string.IsNullOrEmpty(credentialsJson)
                ? GoogleCredential.FromJson(credentialsJson)
                : GoogleCredential.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "firebase-key.json"));

            FirestoreDbBuilder builder = new FirestoreDbBuilder
            {
                ProjectId = projectId,
                GoogleCredential = credential
            };

            // Bağlantıyı inşa ediyoruz
            _db = builder.Build();
        }

        // Veritabanına erişmek istediğimizde bu Property'yi kullanacağız
        public FirestoreDb Db => _db;
    }
}