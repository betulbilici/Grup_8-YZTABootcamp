using Google.Cloud.Firestore;

namespace CvInterviewPlatform.Web.Models
{
    // Bu sınıfın Firestore'a bir doküman olarak kaydedileceğini belirtiyoruz
    [FirestoreData]
    public class User
    {
        [FirestoreProperty("username")]
        public string Username { get; set; } = string.Empty;

        [FirestoreProperty("firstName")]
        public string FirstName { get; set; } = string.Empty;

        [FirestoreProperty("lastName")]
        public string LastName { get; set; } = string.Empty;

        [FirestoreProperty("email")]
        public string Email { get; set; } = string.Empty;

        [FirestoreProperty("phoneNumber")]
        public string PhoneNumber { get; set; } = string.Empty;

        // Şifreyi açık metin olarak değil, hash'lenmiş olarak burada tutacağız
        [FirestoreProperty("passwordHash")]
        public string PasswordHash { get; set; } = string.Empty;

        // Profil fotoğrafı başlangıçta zorunlu olmadığı için null (boş) olabilir dedik
        [FirestoreProperty("profilePictureUrl")]
        public string? ProfilePictureUrl { get; set; } = null;

        [FirestoreProperty("cvUrl")]
        public string? CvUrl { get; set; } = null;
    }
}