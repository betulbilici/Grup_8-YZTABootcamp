using System.Security.Cryptography;
using System.Text;
namespace CvInterviewPlatform.Web.Helpers
{
    public static class PasswordHasher
    {
        // Girilen düz şifreyi geri döndürülemez bir SHA256 hash'ine çevirir
        public static string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("x2")); // Byte verisini 16'lık (hexadecimal) metne çevirir
                }
                return builder.ToString();
            }
        }

        // Kullanıcının giriş yaparken girdiği şifre ile DB'deki hash'i karşılaştırır
        public static bool VerifyPassword(string password, string hashedPassword)
        {
            string hashOfInput = HashPassword(password);
            return string.Equals(hashOfInput, hashedPassword, StringComparison.OrdinalIgnoreCase);
        }
    }
}