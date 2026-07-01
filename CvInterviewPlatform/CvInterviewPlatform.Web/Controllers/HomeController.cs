using Microsoft.AspNetCore.Mvc;
using Google.Cloud.Firestore;
using CvInterviewPlatform.Web.Models;
using System.IO;
using System.Threading.Tasks;

namespace CvInterviewPlatform.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly FirestoreDb _db;
        private readonly IWebHostEnvironment _env;

        // Firestore servisini ve sunucu klasör yapýsýna eriþmek için IWebHostEnvironment'ý enjekte ediyoruz
        public HomeController(FirestoreService firestoreService, IWebHostEnvironment env)
        {
            _db = firestoreService.Db;
            _env = env;
        }

        // Ana Ekran (Dashboard)
        public async Task<IActionResult> Index()
        {
            // Session'dan giriþ yapan kullanýcýnýn adýný kontrol ediyoruz
            string username = HttpContext.Session.GetString("Username");

            // Eðer giriþ yapýlmadýysa doðrudan giriþ sayfasýna postyalýyoruz
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("SignIn", "Account");
            }

            // Firestore'dan kullanýcýnýn güncel verilerini çekiyoruz
            DocumentReference docRef = _db.Collection("Users").Document(username);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

            if (!snapshot.Exists)
            {
                return RedirectToAction("SignOut", "Account");
            }

            User user = snapshot.ConvertTo<User>();

            // Verileri arayüze (View) model olarak gönderiyoruz
            return View(user);
        }

        // Profil Resmi ve CV Yükleme Tetikleyicisi
        [HttpPost]
        public async Task<IActionResult> UploadDocuments(IFormFile profilePicture, IFormFile cvFile)
        {
            string username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username)) return RedirectToAction("SignIn", "Account");

            DocumentReference docRef = _db.Collection("Users").Document(username);
            Dictionary<string, object> updates = new Dictionary<string, object>();

            // 1. Profil Resmi Yükleme Ýþlemi
            if (profilePicture != null && profilePicture.Length > 0)
            {
                // wwwroot/uploads/profiles klasörünü hedefliyoruz
                string profileFolder = Path.Combine(_env.WebRootPath, "uploads", "profiles");
                Directory.CreateDirectory(profileFolder); // Klasör yoksa otomatik oluþturur

                string uniqueProfileName = username + "_profile" + Path.GetExtension(profilePicture.FileName);
                string filePath = Path.Combine(profileFolder, uniqueProfileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await profilePicture.CopyToAsync(stream);
                }

                updates["profilePictureUrl"] = "/uploads/profiles/" + uniqueProfileName;
            }

            // 2. CV (PDF) Yükleme Ýþlemi
            if (cvFile != null && cvFile.Length > 0)
            {
                // wwwroot/uploads/cvs klasörünü hedefliyoruz
                string cvFolder = Path.Combine(_env.WebRootPath, "uploads", "cvs");
                Directory.CreateDirectory(cvFolder);

                string uniqueCvName = username + "_cv" + Path.GetExtension(cvFile.FileName);
                string filePath = Path.Combine(cvFolder, uniqueCvName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await cvFile.CopyToAsync(stream);
                }

                updates["cvUrl"] = "/uploads/cvs/" + uniqueCvName;
            }

            // Eðer herhangi bir dosya yüklendiyse Firestore dokümanýný tek seferde güncelliyoruz
            if (updates.Count > 0)
            {
                await docRef.UpdateAsync(updates);
            }

            return RedirectToAction("Index");
        }

        // Ayarlar Sayfasýný Açan Metot (GET)
        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            string username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username)) return RedirectToAction("SignIn", "Account");

            DocumentReference docRef = _db.Collection("Users").Document(username);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

            if (!snapshot.Exists) return RedirectToAction("SignOut", "Account");

            User user = snapshot.ConvertTo<User>();
            return View(user);
        }

        // Ayarlarý Güncelleyen Metot (POST)
        [HttpPost]
        public async Task<IActionResult> UpdateSettings(string firstName, string lastName, string email, string phoneNumber, string currentPassword, string newPassword)
        {
            string username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username)) return RedirectToAction("SignIn", "Account");

            DocumentReference docRef = _db.Collection("Users").Document(username);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();
            User user = snapshot.ConvertTo<User>();

            // 1. Format Kontrolleri (Regex)
            if (!System.Text.RegularExpressions.Regex.IsMatch(email ?? "", @"^[a-zA-Z0-9._%+-]+@gmail\.com$"))
            {
                ViewBag.Error = "Lütfen geçerli bir Gmail adresi girin!";
                return View("Settings", user);
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(phoneNumber ?? "", @"^0?5[0-9]{9}$"))
            {
                ViewBag.Error = "Lütfen geçerli bir telefon numarasý girin!";
                return View("Settings", user);
            }

            // 2. Benzersizlik Kontrolleri (Deðiþen bilgiler baþka birinde var mý?)
            // E-posta kontrolü
            QuerySnapshot emailSnapshot = await _db.Collection("Users").WhereEqualTo("email", email).GetSnapshotAsync();
            foreach (var doc in emailSnapshot.Documents)
            {
                if (doc.Id != username)
                {
                    ViewBag.Error = "Bu Gmail adresi baþka bir kullanýcýya ait!";
                    return View("Settings", user);
                }
            }

            // Telefon kontrolü
            QuerySnapshot phoneSnapshot = await _db.Collection("Users").WhereEqualTo("phoneNumber", phoneNumber).GetSnapshotAsync();
            foreach (var doc in phoneSnapshot.Documents)
            {
                if (doc.Id != username)
                {
                    ViewBag.Error = "Bu telefon numarasý baþka bir kullanýcýya ait!";
                    return View("Settings", user);
                }
            }

            // 3. Güncelleme Paketi Hazýrlama
            Dictionary<string, object> updates = new Dictionary<string, object>
            {
                { "firstName", firstName },
                { "lastName", lastName },
                { "email", email },
                { "phoneNumber", phoneNumber }
            };

            // 4. Þifre Deðiþtirilmek Ýsteniyorsa
            if (!string.IsNullOrEmpty(newPassword))
            {
                // Mevcut þifre doðru mu kontrol et
                if (!CvInterviewPlatform.Web.Helpers.PasswordHasher.VerifyPassword(currentPassword, user.PasswordHash))
                {
                    ViewBag.Error = "Mevcut þifrenizi hatalý girdiniz! Þifre deðiþtirilemedi.";
                    return View("Settings", user);
                }

                updates["passwordHash"] = CvInterviewPlatform.Web.Helpers.PasswordHasher.HashPassword(newPassword);
            }

            // Firestore'u güncelle
            await docRef.UpdateAsync(updates);

            // Session isim bilgisini de güncelle ki Navbar anýnda yenilensin
            HttpContext.Session.SetString("FirstName", firstName);

            ViewBag.Success = "Profil bilgileriniz baþarýyla güncellendi!";

            // Güncel veriyi tekrar çekip sayfaya basýyoruz
            snapshot = await docRef.GetSnapshotAsync();
            user = snapshot.ConvertTo<User>();

            return View("Settings", user);
        }
    }
}