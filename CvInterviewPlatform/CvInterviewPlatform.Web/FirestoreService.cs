using Google.Cloud.Firestore;
using System;
using System.IO;

namespace CvInterviewPlatform.Web
{
    public class FirestoreService
    {
        private readonly FirestoreDb _db;

        public FirestoreService()
        {
            // Çıktı dizinine kopyalanan JSON dosyasının yolunu buluyoruz
            string keyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "firebase-key.json");

            // Görseldeki proje ID'nizi buraya doğrudan entegre ettim
            string projectId = "cvinterviewplatform";

            // Veritabanı bağlantı ayarlarını yapılandırıyoruz
            FirestoreDbBuilder builder = new FirestoreDbBuilder
            {
                ProjectId = projectId,
                CredentialsPath = keyPath
            };

            // Bağlantıyı inşa ediyoruz
            _db = builder.Build();
        }

        // Veritabanına erişmek istediğimizde bu Property'yi kullanacağız
        public FirestoreDb Db => _db;
    }
}