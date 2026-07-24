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
        private static readonly string[] CvAllowedExtensions = { ".pdf", ".docx", ".txt", ".md", ".png", ".jpg", ".jpeg" };
        private static readonly string[] ProfilePictureAllowedExtensions = { ".png", ".jpg", ".jpeg", ".webp" };
        private const long CvMaxSizeBytes = 10 * 1024 * 1024;
        private const long ProfilePictureMaxSizeBytes = 5 * 1024 * 1024;

        private readonly FirestoreDb _db;
        private readonly IWebHostEnvironment _env;
        private readonly CvParserService _cvParserService;
        private readonly GeminiService _geminiService;
        private readonly CvStorageService _cvStorageService;

        // Firestore servisini ve sunucu klasör yapısına erişmek için IWebHostEnvironment'ı enjekte ediyoruz
        public HomeController(FirestoreService firestoreService, IWebHostEnvironment env, CvParserService cvParserService, GeminiService geminiService, CvStorageService cvStorageService)
        {
            _db = firestoreService.Db;
            _env = env;
            _cvParserService = cvParserService;
            _geminiService = geminiService;
            _cvStorageService = cvStorageService;
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

        // CV Yükleme Tetikleyicisi (Ana Sayfa)
        [HttpPost]
        public async Task<IActionResult> UploadCv(IFormFile cvFile)
        {
            string username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username)) return RedirectToAction("SignIn", "Account");

            if (cvFile == null || cvFile.Length == 0)
            {
                TempData["Error"] = "Lütfen yüklemek için bir CV dosyası seçin.";
                return RedirectToAction("Index");
            }

            // Uzantıyı kullanıcının gönderdiği ham dosya adından alıp doğrudan diske
            // yazmıyoruz — sadece whitelist ile eşleşiyorsa devam ediyoruz, diske
            // yazılan dosya adı da bu normalize edilmiş uzantıyı kullanıyor.
            string ext = Path.GetExtension(cvFile.FileName).ToLowerInvariant();
            if (!CvAllowedExtensions.Contains(ext))
            {
                TempData["Error"] = $"Desteklenmeyen dosya formatı. İzin verilen formatlar: {string.Join(", ", CvAllowedExtensions)}";
                return RedirectToAction("Index");
            }

            if (cvFile.Length > CvMaxSizeBytes)
            {
                TempData["Error"] = "CV dosyası 10 MB sınırını aşıyor.";
                return RedirectToAction("Index");
            }

            DocumentReference docRef = _db.Collection("Users").Document(username);
            Dictionary<string, object> updates = new Dictionary<string, object>();

            // Docling parse'ı hâlâ bir dosya yolu istiyor, bu yüzden geçici bir dosyaya
            // yazıyoruz; kalıcı depolama artık wwwroot değil Cloudflare R2.
            string objectKey = "cvs/" + username + "_cv" + ext;
            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ext);

            try
            {
                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await cvFile.CopyToAsync(stream);
                }

                using (var uploadStream = System.IO.File.OpenRead(tempPath))
                {
                    await _cvStorageService.UploadAsync(uploadStream, objectKey, cvFile.ContentType);
                }
                updates["cvUrl"] = objectKey;

                // CV metin içeriğini ayıklamak ve Firestore'a kaydetmek için parser servisini çağırıyoruz
                try
                {
                    string parsedContent = await _cvParserService.ParseDocumentAsync(tempPath);
                    if (!string.IsNullOrEmpty(parsedContent))
                    {
                        updates["cvContent"] = parsedContent;

                        // Analizi burada üretmiyoruz — CvAnalysis() action'ı ilk ziyarette
                        // otomatik üretiyor zaten, upload'ı ekstra bir Gemini çağrısı kadar
                        // bekletmemek için eski analizi burada temizliyoruz ki bir sonraki
                        // ziyarette yeni CV'ye göre yeniden üretilsin.
                        updates["cvAnalysis"] = "";

                        TempData["Success"] = "CV belgeniz başarıyla yüklendi.";
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
            finally
            {
                if (System.IO.File.Exists(tempPath))
                {
                    System.IO.File.Delete(tempPath);
                }
            }

            await docRef.UpdateAsync(updates);
            return RedirectToAction("Index");
        }

        // Profil Fotoğrafı Yükleme Tetikleyicisi (Ayarlar sayfası)
        [HttpPost]
        public async Task<IActionResult> UploadProfilePicture(IFormFile profilePicture)
        {
            string username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username)) return RedirectToAction("SignIn", "Account");

            if (profilePicture == null || profilePicture.Length == 0)
            {
                TempData["Error"] = "Lütfen yüklemek için bir fotoğraf seçin.";
                return RedirectToAction("Settings");
            }

            string ext = Path.GetExtension(profilePicture.FileName).ToLowerInvariant();
            if (!ProfilePictureAllowedExtensions.Contains(ext))
            {
                TempData["Error"] = $"Desteklenmeyen dosya formatı. İzin verilen formatlar: {string.Join(", ", ProfilePictureAllowedExtensions)}";
                return RedirectToAction("Settings");
            }

            if (profilePicture.Length > ProfilePictureMaxSizeBytes)
            {
                TempData["Error"] = "Fotoğraf 5 MB sınırını aşıyor.";
                return RedirectToAction("Settings");
            }

            DocumentReference docRef = _db.Collection("Users").Document(username);

            // Kalıcı depolama artık wwwroot değil Cloudflare R2 — Firestore'a ve
            // Session'a dosyanın kendisi değil R2 obje anahtarı yazılıyor.
            string objectKey = "profiles/" + username + "_profile" + ext;
            using (var stream = profilePicture.OpenReadStream())
            {
                await _cvStorageService.UploadAsync(stream, objectKey, profilePicture.ContentType);
            }

            await docRef.UpdateAsync("profilePictureUrl", objectKey);

            // Sidebar/topbar'daki avatar Session'dan okunuyor — anında güncellensin diye
            // burada da yazıyoruz, yoksa yeniden giriş yapana kadar eski (harf) avatar görünür.
            HttpContext.Session.SetString("ProfilePictureUrl", objectKey);

            TempData["Success"] = "Profil fotoğrafınız başarıyla güncellendi.";
            return RedirectToAction("Settings");
        }

        // Yapay Zeka Destekli CV Analizi Sayfası (GET)
        [HttpGet]
        public async Task<IActionResult> CvAnalysis()
        {
            string username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username)) return RedirectToAction("SignIn", "Account");

            DocumentReference docRef = _db.Collection("Users").Document(username);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

            if (!snapshot.Exists) return RedirectToAction("SignOut", "Account");

            User user = snapshot.ConvertTo<User>();

            // Geriye dönük uyumluluk: CV'si zaten yüklü ama daha önce analiz üretilmemiş
            // kullanıcılar için (bu özellikten önce yüklenmiş CV'ler) analiz burada tetiklenir.
            // Gemini'yi burada BEKLEMİYORUZ — 13-20sn sürebiliyor, sayfayı bloklamak yerine
            // arka planda üretilip bittiğinde Firestore'a yazılıyor (bkz. PersistWhenReadyAsync),
            // sayfa "hazırlanıyor" durumuyla hemen açılıyor ve CvAnalysisStatus() ile polling yapıyor.
            if (!string.IsNullOrEmpty(user.CvContent) && string.IsNullOrEmpty(user.CvAnalysis))
            {
                Task<string> analysisTask = _geminiService.GetOrStartCvAnalysisAsync(username, user.CvContent);
                _ = PersistCvAnalysisWhenReadyAsync(username, analysisTask);
                ViewBag.AnalysisPending = true;
            }

            // "CV Dosyanı Görüntüle" linki artık bir R2 obje anahtarı değil, süreli
            // imzalı bir URL istiyor — bunu view'in kendisi üretemediği için burada hazırlıyoruz.
            ViewBag.CvPreviewUrl = _cvStorageService.ResolvePreviewUrl(user.CvUrl);

            return View(user);
        }

        // CV Analizim sayfasındaki polling script'inin sorduğu durum uç noktası —
        // Gemini'yi tetiklemez, sadece PersistCvAnalysisWhenReadyAsync'in Firestore'a
        // yazıp yazmadığını kontrol eder.
        [HttpGet]
        public async Task<IActionResult> CvAnalysisStatus()
        {
            string username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            DocumentSnapshot snapshot = await _db.Collection("Users").Document(username).GetSnapshotAsync();
            if (!snapshot.Exists) return Unauthorized();

            User user = snapshot.ConvertTo<User>();
            bool ready = !string.IsNullOrEmpty(user.CvAnalysis);
            return Json(new { ready, analysis = ready ? user.CvAnalysis : null });
        }

        private async Task PersistCvAnalysisWhenReadyAsync(string username, Task<string> analysisTask)
        {
            try
            {
                string result = await analysisTask;
                await _db.Collection("Users").Document(username).UpdateAsync("cvAnalysis", result);
            }
            catch (System.Exception ex)
            {
                System.Console.Error.WriteLine($"PersistCvAnalysisWhenReadyAsync hata: {ex.Message}");
            }
            finally
            {
                _geminiService.ClearInFlightCvAnalysis(username);
            }
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
            ViewBag.ProfilePicturePreviewUrl = _cvStorageService.ResolvePreviewUrl(user.ProfilePictureUrl);
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