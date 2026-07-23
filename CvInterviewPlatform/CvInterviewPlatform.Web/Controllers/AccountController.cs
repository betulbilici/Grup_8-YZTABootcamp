using Microsoft.AspNetCore.Mvc;
using Google.Cloud.Firestore;
using CvInterviewPlatform.Web.Models;
using CvInterviewPlatform.Web.Helpers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using System.Security.Claims;

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
        public async Task<IActionResult> SignOut()
        {
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("SignIn");
        }

        // "Google ile Giriş Yap" butonuna basılınca Google'ın OAuth ekranına yönlendirir
        [HttpGet]
        public IActionResult GoogleLogin()
        {
            var redirectUrl = Url.Action("GoogleResponse", "Account");
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        // Google'dan onaylı kimlikle geri döndüğümüzde çalışan metot
        [HttpGet]
        public async Task<IActionResult> GoogleResponse()
        {
            AuthenticateResult authResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!authResult.Succeeded || authResult.Principal == null)
            {
                ViewBag.Error = "Google ile giriş başarısız oldu. Lütfen tekrar deneyin.";
                return View("SignIn");
            }

            string? email = authResult.Principal.FindFirstValue(ClaimTypes.Email);
            string? firstName = authResult.Principal.FindFirstValue(ClaimTypes.GivenName);
            string? lastName = authResult.Principal.FindFirstValue(ClaimTypes.Surname);

            if (string.IsNullOrEmpty(email))
            {
                ViewBag.Error = "Google hesabınızdan e-posta bilgisi alınamadı.";
                return View("SignIn");
            }

            CollectionReference usersRef = _db.Collection("Users");
            Query emailQuery = usersRef.WhereEqualTo("email", email);
            QuerySnapshot emailSnapshot = await emailQuery.GetSnapshotAsync();

            User user;
            if (emailSnapshot.Count > 0)
            {
                // Bu e-posta ile daha önce (şifreli veya Google ile) kayıt olunmuş — mevcut hesaba giriş yap
                user = emailSnapshot.Documents[0].ConvertTo<User>();
            }
            else
            {
                // İlk kez Google ile giriş yapıyor — e-posta önekinden benzersiz bir kullanıcı adı türetiyoruz
                string baseUsername = Regex.Replace(email.Split('@')[0], @"[^a-zA-Z0-9]", "").ToLower();
                if (string.IsNullOrEmpty(baseUsername))
                {
                    baseUsername = "kullanici";
                }

                string username = baseUsername;
                int suffix = 1;
                while ((await usersRef.Document(username).GetSnapshotAsync()).Exists)
                {
                    username = $"{baseUsername}{suffix}";
                    suffix++;
                }

                user = new User
                {
                    Username = username,
                    FirstName = string.IsNullOrEmpty(firstName) ? "Kullanıcı" : firstName,
                    LastName = lastName ?? "",
                    Email = email,
                    PhoneNumber = "",
                    // Google ile giriş yapan kullanıcılar şifreyle giriş yapamasın diye
                    // tahmin edilemeyen rastgele bir şifre hash'i atıyoruz.
                    PasswordHash = PasswordHasher.HashPassword(Guid.NewGuid().ToString())
                };

                await usersRef.Document(username).SetAsync(user);
            }

            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("FirstName", user.FirstName);

            // Google el sıkışması için kullanılan geçici çerezi temizliyoruz — uygulama
            // artık kendi Session mekanizmasını kullanıyor.
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Index", "Home");
        }
    }
}