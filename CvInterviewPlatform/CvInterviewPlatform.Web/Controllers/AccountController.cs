using Microsoft.AspNetCore.Mvc;
using Google.Cloud.Firestore;
using CvInterviewPlatform.Web.Models;
using CvInterviewPlatform.Web.Helpers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CvInterviewPlatform.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly FirestoreDb _db;

        // Adım 3.3'te Program.cs'e kaydettiğimiz servisi buraya enjekte ediyoruz
        public AccountController(FirestoreService firestoreService)
        {
            _db = firestoreService.Db;
        }

        // Kayıt sayfasını ekrana açan metot (GET)
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // Kayıt ol butonuna basıldığında çalışan metot (POST)
        [HttpPost]
        public async Task<IActionResult> Register(string username, string firstName, string lastName, string email, string phoneNumber, string password, string confirmPassword)
        {
            // 1. Şifre Eşleşme Kontrolü
            if (password != confirmPassword)
            {
                ViewBag.Error = "Şifreler birbiriyle uyuşmuyor!";
                return View();
            }

            // 2. Format Kontrolleri (Regex)
            // Sadece gmail.com uzantısını kabul eder
            if (!Regex.IsMatch(email ?? "", @"^[a-zA-Z0-9._%+-]+@gmail\.com$"))
            {
                ViewBag.Error = "Lütfen geçerli bir Gmail adresi girin (örn: ornek@gmail.com)!";
                return View();
            }

            // Türkiye formatına uygun 10 veya 11 haneli numara kontrolü (Örn: 05xxxxxxxxx veya 5xxxxxxxxx)
            if (!Regex.IsMatch(phoneNumber ?? "", @"^0?5[0-9]{9}$"))
            {
                ViewBag.Error = "Lütfen geçerli bir telefon numarası girin (örn: 05xxxxxxxxx)!";
                return View();
            }

            // 3. Firestore Benzersizlik Kontrolleri
            CollectionReference usersRef = _db.Collection("Users");

            // Kullanıcı adı kontrolü
            Query usernameQuery = usersRef.WhereEqualTo("username", username);
            QuerySnapshot usernameSnapshot = await usernameQuery.GetSnapshotAsync();
            if (usernameSnapshot.Count > 0)
            {
                ViewBag.Error = "Bu kullanıcı adı zaten alınmış!";
                return View();
            }

            // E-posta kontrolü
            Query emailQuery = usersRef.WhereEqualTo("email", email);
            QuerySnapshot emailSnapshot = await emailQuery.GetSnapshotAsync();
            if (emailSnapshot.Count > 0)
            {
                ViewBag.Error = "Bu Gmail hesabı ile zaten kayıt yapılmış!";
                return View();
            }

            // Telefon kontrolü
            Query phoneQuery = usersRef.WhereEqualTo("phoneNumber", phoneNumber);
            QuerySnapshot phoneSnapshot = await phoneQuery.GetSnapshotAsync();
            if (phoneSnapshot.Count > 0)
            {
                ViewBag.Error = "Bu telefon numarası zaten sistemde kayıtlı!";
                return View();
            }

            // 4. Tüm kontroller başarılıysa Firestore'a Kaydetme
            User newUser = new User
            {
                Username = username,
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                PhoneNumber = phoneNumber,
                PasswordHash = PasswordHasher.HashPassword(password) // Şifreyi hashleyerek atıyoruz
            };

            // Firestore'da doküman kimliğini (Document ID) benzersiz olan kullanıcı adı yapıyoruz
            await usersRef.Document(username).SetAsync(newUser);

            // Kayıt bitince Giriş sayfasına yönlendireceğiz (Giriş metodunu bir sonraki adımda ekleyeceğiz)
            return RedirectToAction("SignIn");
        }

        // Giriş sayfasını ekrana açan metot (GET)
        [HttpGet]
        public IActionResult SignIn()
        {
            return View();
        }

        // Giriş yap butonuna basıldığında çalışan metot (POST)
        [HttpPost]
        public async Task<IActionResult> SignIn(string loginInput, string password)
        {
            CollectionReference usersRef = _db.Collection("Users");
            Query query;

            // 1. Girdide '@' karakteri varsa E-posta, yoksa Kullanıcı Adı ile arama yapıyoruz
            if (loginInput.Contains("@"))
            {
                query = usersRef.WhereEqualTo("email", loginInput);
            }
            else
            {
                query = usersRef.WhereEqualTo("username", loginInput);
            }

            QuerySnapshot snapshot = await query.GetSnapshotAsync();

            // Kullanıcı bulunamadıysa güvenlik nedeniyle genel bir hata mesajı dönüyoruz
            if (snapshot.Count == 0)
            {
                ViewBag.Error = "Kullanıcı adı/E-posta veya şifre hatalı!";
                return View();
            }

            // 2. Eşleşen dökümandaki veriyi Kullanıcı modeline çeviriyoruz
            DocumentSnapshot userDoc = snapshot.Documents[0];
            User user = userDoc.ConvertTo<User>();

            // 3. Girilen şifre ile veritabanındaki hash'lenmiş şifreyi karşılaştırıyoruz
            if (!PasswordHasher.VerifyPassword(password, user.PasswordHash))
            {
                ViewBag.Error = "Kullanıcı adı/E-posta veya şifre hatalı!";
                return View();
            }

            // 4. Şifre doğruysa .NET Session (Oturum) sistemine kullanıcının bilgilerini kaydediyoruz
            // Böylece tarayıcı kapatılana kadar kullanıcı giriş yapmış olarak kalacak
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("FirstName", user.FirstName);

            // Başarılı girişte projenin varsayılan ana sayfasına (Home/Index) yönlendiriyoruz
            return RedirectToAction("Index", "Home");
        }

        // Şifremi unuttum sayfasını ekrana açan metot (GET)
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // Şifre yenileme butonuna basıldığında çalışan metot (POST)
        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string username, string firstName, string lastName, string email, string phoneNumber, string newPassword, string confirmNewPassword)
        {
            // 1. Yeni şifrelerin eşleşme kontrolü
            if (newPassword != confirmNewPassword)
            {
                ViewBag.Error = "Yeni şifreler birbiriyle uyuşmuyor!";
                return View();
            }

            CollectionReference usersRef = _db.Collection("Users");

            // 2. İstediğin 5'li tam doğrulamayı Firestore üzerinde filtreliyoruz (Compound Query)
            Query query = usersRef
                .WhereEqualTo("username", username)
                .WhereEqualTo("firstName", firstName)
                .WhereEqualTo("lastName", lastName)
                .WhereEqualTo("email", email)
                .WhereEqualTo("phoneNumber", phoneNumber);

            QuerySnapshot snapshot = await query.GetSnapshotAsync();

            // Eğer bu 5 bilginin tamamı tek bir kullanıcı belgesiyle eşleşmiyorsa hata dönüyoruz
            if (snapshot.Count == 0)
            {
                ViewBag.Error = "Girdiğiniz kimlik bilgileri sistemdeki kayıtlarla eşleşmedi!";
                return View();
            }

            // 3. Bilgiler tamamen doğruysa ilgili dökümanın referansını alıyoruz
            DocumentSnapshot userDoc = snapshot.Documents[0];
            DocumentReference docRef = userDoc.Reference;

            // 4. Yeni şifreyi hashleyerek sadece ilgili alanı güncelliyoruz
            string encryptedNewPassword = PasswordHasher.HashPassword(newPassword);
            await docRef.UpdateAsync("passwordHash", encryptedNewPassword);

            ViewBag.Success = "Şifreniz başarıyla güncellendi! Giriş yapabilirsiniz.";
            return View();
        }

        // Kullanıcı çıkış yaptığında oturumu temizleyen metot
        public IActionResult SignOut()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("SignIn");
        }
    }
}