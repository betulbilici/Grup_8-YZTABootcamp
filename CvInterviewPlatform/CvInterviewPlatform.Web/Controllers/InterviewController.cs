using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Google.Cloud.Firestore;
using CvInterviewPlatform.Web.Models;
using CvInterviewPlatform.Web.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CvInterviewPlatform.Web.Controllers
{
    public class InterviewController : Controller
    {
        private readonly FirestoreDb _db;
        private readonly GeminiService _geminiService;
        private readonly AzureTtsService _azureTtsService;

        public InterviewController(FirestoreService firestoreService, GeminiService geminiService, AzureTtsService azureTtsService)
        {
            _db = firestoreService.Db;
            _geminiService = geminiService;
            _azureTtsService = azureTtsService;
        }

        // Mülakat Dashboard'u: Geçmiş mülakatları gösterir ve yeni mülakat başlatır.
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            string username = HttpContext.Session.GetString("Username") ?? "";
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("SignIn", "Account");
            }

            try
            {
                // Kullanıcının mülakatlarını çekiyoruz
                CollectionReference sessionsRef = _db.Collection("Sessions");
                Query query = sessionsRef.WhereEqualTo("username", username);
                QuerySnapshot snapshot = await query.GetSnapshotAsync();

                List<InterviewSession> sessions = snapshot.Documents
                    .Select(d => d.ConvertTo<InterviewSession>())
                    .OrderByDescending(s => s.StartedAt)
                    .ToList();

                return View(sessions);
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Mülakat geçmişi yüklenirken hata oluştu: {ex.Message}";
                return View(new List<InterviewSession>());
            }
        }

        // Yeni mülakat başlatma (POST)
        [HttpPost]
        public async Task<IActionResult> StartInterview(string jobTitle, string mode, string difficulty, int timeLimit)
        {
            string username = HttpContext.Session.GetString("Username") ?? "";
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("SignIn", "Account");
            }

            if (string.IsNullOrWhiteSpace(jobTitle))
            {
                TempData["Error"] = "Lütfen mülakat yapılacak pozisyon adını giriniz.";
                return RedirectToAction("Index");
            }

            // Kullanıcıdan gelen değerleri beyaz liste ile doğruluyoruz, doğrudan Firestore'a yazmıyoruz
            string validatedMode = mode == "Realistic" ? "Realistic" : "Preparation";
            string validatedDifficulty = (difficulty == "Junior" || difficulty == "Senior") ? difficulty : "Mid";
            int validatedTimeLimit;
            if (validatedMode == "Realistic")
            {
                validatedTimeLimit = 60;
            }
            else
            {
                validatedTimeLimit = (timeLimit == 0 || timeLimit == 300 || timeLimit == 600) ? timeLimit : 300;
            }

            try
            {
                // Adayın CV içeriğini Firestore'daki User dökümanından çekiyoruz
                string cvContent = string.Empty;
                DocumentReference userRef = _db.Collection("Users").Document(username);
                DocumentSnapshot userSnapshot = await userRef.GetSnapshotAsync();
                if (userSnapshot.Exists)
                {
                    User user = userSnapshot.ConvertTo<User>();
                    cvContent = user.CvContent ?? string.Empty;
                }

                // Firestore'da yeni bir döküman referansı oluşturuyoruz (ID otomatik oluşur)
                DocumentReference newSessionRef = _db.Collection("Sessions").Document();
                string sessionId = newSessionRef.Id;

                InterviewSession newSession = new InterviewSession
                {
                    SessionId = sessionId,
                    Username = username,
                    JobTitle = jobTitle,
                    StartedAt = DateTime.UtcNow,
                    CurrentQuestionNumber = 1,
                    IsCompleted = false,
                    Mode = validatedMode,
                    DifficultyLevel = validatedDifficulty,
                    TimeLimitSeconds = validatedTimeLimit,
                    TotalQuestions = 5,
                    History = new List<InterviewStep>()
                };

                // 1. Soru üretimi (CV içeriği dahil edildi)
                string firstQuestion = await _geminiService.GenerateQuestionAsync(newSession, cvContent, 1);
                newSession.History.Add(new InterviewStep
                {
                    Question = firstQuestion,
                    Answer = string.Empty,
                    AskedAt = DateTime.UtcNow
                });

                await newSessionRef.SetAsync(newSession);

                // Sesi de aynı istekte üretip önbelleğe alıyoruz — kullanıcı zaten
                // Gemini'nin ~13sn'lik cevabını beklediği bir yükleniyor ekranında,
                // Azure'ın ~1-2sn'lik gecikmesi bu bekleme içinde fark edilmeden geçiyor.
                await _azureTtsService.SynthesizeAndCacheAsync(firstQuestion, AzureTtsService.BuildCacheKey(sessionId, 1));

                return RedirectToAction("Session", new { id = sessionId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Mülakat başlatılamadı: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // Aktif mülakat ekranı veya mülakat sonucu (GET)
        [HttpGet]
        public async Task<IActionResult> Session(string id)
        {
            string username = HttpContext.Session.GetString("Username") ?? "";
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("SignIn", "Account");
            }

            if (string.IsNullOrEmpty(id))
            {
                return RedirectToAction("Index");
            }

            try
            {
                DocumentReference docRef = _db.Collection("Sessions").Document(id);
                DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

                if (!snapshot.Exists)
                {
                    return NotFound();
                }

                InterviewSession session = snapshot.ConvertTo<InterviewSession>();

                // Oturum güvenliği: Başkasının mülakatına erişimi engelle
                if (session.Username != username)
                {
                    return Forbid();
                }

                return View(session);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Mülakat oturumu yüklenemedi: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // Adayın cevabını gönderme (POST)
        [HttpPost]
        public async Task<IActionResult> SubmitAnswer(string id, string answer)
        {
            string username = HttpContext.Session.GetString("Username") ?? "";
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("SignIn", "Account");
            }

            if (string.IsNullOrEmpty(id))
            {
                return RedirectToAction("Index");
            }

            try
            {
                DocumentReference docRef = _db.Collection("Sessions").Document(id);
                DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

                if (!snapshot.Exists)
                {
                    return NotFound();
                }

                InterviewSession session = snapshot.ConvertTo<InterviewSession>();

                if (session.Username != username)
                {
                    return Forbid();
                }

                if (session.IsCompleted)
                {
                    return RedirectToAction("Session", new { id = id });
                }

                // Adayın CV içeriğini Firestore'daki User dökümanından çekiyoruz
                string cvContent = string.Empty;
                DocumentReference userRef = _db.Collection("Users").Document(username);
                DocumentSnapshot userSnapshot = await userRef.GetSnapshotAsync();
                if (userSnapshot.Exists)
                {
                    User user = userSnapshot.ConvertTo<User>();
                    cvContent = user.CvContent ?? string.Empty;
                }

                // Adayın cevabını şu anki soru adımına kaydediyoruz
                int currentIndex = session.CurrentQuestionNumber - 1;
                if (currentIndex >= 0 && currentIndex < session.History.Count)
                {
                    session.History[currentIndex].Answer = answer ?? "";
                }

                if (session.CurrentQuestionNumber < session.TotalQuestions)
                {
                    // Sıradaki soruyu oluşturup ekliyoruz (CV içeriği dahil edildi)
                    int nextQuestionNumber = session.CurrentQuestionNumber + 1;
                    string nextQuestion = await _geminiService.GenerateQuestionAsync(session, cvContent, nextQuestionNumber);

                    session.History.Add(new InterviewStep
                    {
                        Question = nextQuestion,
                        Answer = string.Empty,
                        AskedAt = DateTime.UtcNow
                    });

                    session.CurrentQuestionNumber = nextQuestionNumber;

                    await _azureTtsService.SynthesizeAndCacheAsync(nextQuestion, AzureTtsService.BuildCacheKey(id, nextQuestionNumber));
                }
                else
                {
                    // Tüm sorular tamamlandıysa değerlendirme oluşturup mülakatı sonlandırıyoruz (CV içeriği dahil edildi)
                    string evaluation = await _geminiService.GenerateEvaluationAsync(session, cvContent);
                    session.FinalEvaluation = evaluation;
                    session.IsCompleted = true;
                }

                // Değişiklikleri Firestore'da güncelliyoruz
                await docRef.SetAsync(session);

                return RedirectToAction("Session", new { id = id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Cevap gönderilirken bir hata oluştu: {ex.Message}";
                return RedirectToAction("Session", new { id = id });
            }
        }

        // Aktif sorunun ipucunu getirme (sadece Hazırlık modunda) (POST)
        [HttpPost]
        public async Task<IActionResult> GetHint(string id)
        {
            string username = HttpContext.Session.GetString("Username") ?? "";
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("SignIn", "Account");
            }

            if (string.IsNullOrEmpty(id))
            {
                return RedirectToAction("Index");
            }

            try
            {
                DocumentReference docRef = _db.Collection("Sessions").Document(id);
                DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

                if (!snapshot.Exists)
                {
                    return NotFound();
                }

                InterviewSession session = snapshot.ConvertTo<InterviewSession>();

                if (session.Username != username)
                {
                    return Forbid();
                }

                if (session.Mode != "Preparation")
                {
                    return BadRequest();
                }

                int currentIndex = session.CurrentQuestionNumber - 1;
                if (currentIndex < 0 || currentIndex >= session.History.Count)
                {
                    return BadRequest();
                }

                string question = session.History[currentIndex].Question;
                string hint = await _geminiService.GenerateHintAsync(question, session.JobTitle, session.DifficultyLevel);

                return Json(new { hint });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"GetHint hata: {ex.Message}");
                return StatusCode(500);
            }
        }

        // Sorunun önceden üretilmiş Azure TTS ses klibini döner (bkz. StartInterview/SubmitAnswer)
        [HttpGet]
        public async Task<IActionResult> QuestionAudio(string id, int questionNumber)
        {
            string username = HttpContext.Session.GetString("Username") ?? "";
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized();
            }

            byte[]? audioBytes = _azureTtsService.TryGetCached(AzureTtsService.BuildCacheKey(id, questionNumber));
            if (audioBytes == null)
            {
                return NotFound();
            }

            // Başkasının mülakat sesine erişimi engelle (aynı sahiplik kontrolü diğer action'larda da var)
            DocumentSnapshot snapshot = await _db.Collection("Sessions").Document(id).GetSnapshotAsync();
            if (!snapshot.Exists || snapshot.ConvertTo<InterviewSession>().Username != username)
            {
                return Forbid();
            }

            return File(audioBytes, "audio/mpeg");
        }

        // Mülakat Geçmişini Silme (POST)
        [HttpPost]
        public async Task<IActionResult> DeleteSession(string id)
        {
            string username = HttpContext.Session.GetString("Username") ?? "";
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("SignIn", "Account");
            }

            try
            {
                DocumentReference docRef = _db.Collection("Sessions").Document(id);
                DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

                if (snapshot.Exists)
                {
                    InterviewSession session = snapshot.ConvertTo<InterviewSession>();
                    if (session.Username == username)
                    {
                        await docRef.DeleteAsync();
                        TempData["Success"] = "Mülakat geçmişi başarıyla silindi.";
                    }
                    else
                    {
                        TempData["Error"] = "Bu işlem için yetkiniz bulunmamaktadır.";
                    }
                }
                else
                {
                    TempData["Error"] = "Silinmek istenen mülakat bulunamadı.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Mülakat silinirken bir hata oluştu: {ex.Message}";
            }

            return RedirectToAction("Index");
        }
    }
}
