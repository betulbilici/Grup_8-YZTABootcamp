using Microsoft.AspNetCore.Mvc;
using Google.Cloud.Firestore;
using CvInterviewPlatform.Web.Models;
using CvInterviewPlatform.Web.Services;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace CvInterviewPlatform.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly FirestoreDb _db;
        private readonly IWebHostEnvironment _env;
        private readonly CvParserService _cvParserService;

        // Firestore servisini ve sunucu klasör yapısına erişmek için IWebHostEnvironment'ı enjekte ediyoruz
        public HomeController(FirestoreService firestoreService, IWebHostEnvironment env, CvParserService cvParserService)
        {
            _db = firestoreService.Db;
            _env = env;
            _cvParserService = cvParserService;
        }

        // Ana Ekran (Dashboard)
        public async Task<IActionResult> Index()
        {
            // Session'dan giriş yapan kullanıcının adını kontrol ediyoruz
            string username = HttpContext.Session.GetString("Username");

            // Eğer giriş yapılmadıysa doğrudan giriş sayfasına postyalıyoruz
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("SignIn", "Account");
            }

            // Firestore'dan kullanıcının güncel verilerini çekiyoruz
            DocumentReference docRef = _db.Collection("Users").Document(username);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

            if (!snapshot.Exists)
            {
                return RedirectToAction("SignOut", "Account");
            }

            User user = snapshot.ConvertTo<User>();

            // Ana Sayfa'daki özet istatistikler ve "Son Mülakatlar" listesi için geçmişi çekiyoruz
            CollectionReference sessionsRef = _db.Collection("Sessions");
            Query query = sessionsRef.WhereEqualTo("username", username);
            QuerySnapshot sessionsSnapshot = await query.GetSnapshotAsync();

            List<InterviewSession> sessions = sessionsSnapshot.Documents
                .Select(d => d.ConvertTo<InterviewSession>())
                .OrderByDescending(s => s.StartedAt)
                .ToList();

            ViewBag.CompletedCount = sessions.Count(s => s.IsCompleted);
            ViewBag.InProgressCount = sessions.Count(s => !s.IsCompleted);
            ViewBag.LastJobTitle = sessions.FirstOrDefault()?.JobTitle;
            ViewBag.RecentSessions = sessions.Take(3).ToList();

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

            // 1. Profil Resmi Yükleme İşlemi
            if (profilePicture != null && profilePicture.Length > 0)
            {
                // wwwroot/uploads/profiles klasörünü hedefliyoruz
                string profileFolder = Path.Combine(_env.WebRootPath, "uploads", "profiles");
                Directory.CreateDirectory(profileFolder); // Klasör yoksa otomatik oluşturur

                string uniqueProfileName = username + "_profile" + Path.GetExtension(profilePicture.FileName);
                string filePath = Path.Combine(profileFolder, uniqueProfileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await profilePicture.CopyToAsync(stream);
                }

                updates["profilePictureUrl"] = "/uploads/profiles/" + uniqueProfileName;
            }

            // 2. CV (PDF) Yükleme İşlemi
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

                // CV metin içeriğini ayıklamak ve Firestore'a kaydetmek için parser servisini çağırıyoruz
                try
                {
                    string parsedContent = await _cvParserService.ParsePdfAsync(filePath);
                    if (!string.IsNullOrEmpty(parsedContent))
                    {
                        updates["cvContent"] = parsedContent;
                        TempData["Success"] = "CV belgeniz başarıyla yüklendi ve yapay zeka analizi için hazırlandı.";
                    }
                    else
                    {
                        TempData["Warning"] = "CV dosyanız yüklendi ancak içeriği metne dönüştürülemedi. Yapay zeka mülakat sırasında özgeçmişinizi okuyamayabilir.";
                    }
                }
                catch (System.Exception ex)
                {
                    TempData["Warning"] = "CV dosyanız yüklendi ancak yapay zeka analiz servisi (FastAPI) şu an kapalı olduğu için metin okunamadı.";
                }
            }

            // Eğer herhangi bir dosya yüklendiyse Firestore dokümanını tek seferde güncelliyoruz
            if (updates.Count > 0)
            {
                await docRef.UpdateAsync(updates);
                
                // Sadece profil resmi güncellendiyse başarı mesajı ekleyelim
                if (TempData["Success"] == null && TempData["Warning"] == null)
                {
                    TempData["Success"] = "Profil fotoğrafınız başarıyla güncellendi.";
                }
            }

            return RedirectToAction("Index");
        }

        // Ayarlar Sayfasını Açan Metot (GET)
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

        // Ayarları Güncelleyen Metot (POST)
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
                ViewBag.Error = "Lütfen geçerli bir telefon numarası girin!";
                return View("Settings", user);
            }

            // 2. Benzersizlik Kontrolleri (Değişen bilgiler başka birinde var mı?)
            // E-posta kontrolü
            QuerySnapshot emailSnapshot = await _db.Collection("Users").WhereEqualTo("email", email).GetSnapshotAsync();
            foreach (var doc in emailSnapshot.Documents)
            {
                if (doc.Id != username)
                {
                    ViewBag.Error = "Bu Gmail adresi başka bir kullanıcıya ait!";
                    return View("Settings", user);
                }
            }

            // Telefon kontrolü
            QuerySnapshot phoneSnapshot = await _db.Collection("Users").WhereEqualTo("phoneNumber", phoneNumber).GetSnapshotAsync();
            foreach (var doc in phoneSnapshot.Documents)
            {
                if (doc.Id != username)
                {
                    ViewBag.Error = "Bu telefon numarası başka bir kullanıcıya ait!";
                    return View("Settings", user);
                }
            }

            // 3. Güncelleme Paketi Hazırlama
            Dictionary<string, object> updates = new Dictionary<string, object>
            {
                { "firstName", firstName },
                { "lastName", lastName },
                { "email", email },
                { "phoneNumber", phoneNumber }
            };

            // 4. Şifre Değiştirilmek İsteniyorsa
            if (!string.IsNullOrEmpty(newPassword))
            {
                // Mevcut şifre doğru mu kontrol et
                if (!CvInterviewPlatform.Web.Helpers.PasswordHasher.VerifyPassword(currentPassword, user.PasswordHash))
                {
                    ViewBag.Error = "Mevcut şifrenizi hatalı girdiniz! Şifre değiştirilemedi.";
                    return View("Settings", user);
                }

                updates["passwordHash"] = CvInterviewPlatform.Web.Helpers.PasswordHasher.HashPassword(newPassword);
            }

            // Firestore'u güncelle
            await docRef.UpdateAsync(updates);

            // Session isim bilgisini de güncelle ki Navbar anında yenilensin
            HttpContext.Session.SetString("FirstName", firstName);

            ViewBag.Success = "Profil bilgileriniz başarıyla güncellendi!";

            // Güncel veriyi tekrar çekip sayfaya basıyoruz
            snapshot = await docRef.GetSnapshotAsync();
            user = snapshot.ConvertTo<User>();

            return View("Settings", user);
        }

        // Gizlilik Politikası (footer linki)
        public IActionResult Privacy()
        {
            return View();
        }
    }
}