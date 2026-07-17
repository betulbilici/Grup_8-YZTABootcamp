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

        public InterviewController(FirestoreService firestoreService, GeminiService geminiService)
        {
            _db = firestoreService.Db;
            _geminiService = geminiService;
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
        public async Task<IActionResult> StartInterview(string jobTitle)
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

                // 1. Soru üretimi (CV içeriği dahil edildi)
                string firstQuestion = await _geminiService.GenerateQuestionAsync(jobTitle, cvContent, new List<InterviewStep>(), 1);

                InterviewSession newSession = new InterviewSession
                {
                    SessionId = sessionId,
                    Username = username,
                    JobTitle = jobTitle,
                    StartedAt = DateTime.UtcNow,
                    CurrentQuestionNumber = 1,
                    IsCompleted = false,
                    History = new List<InterviewStep>
                    {
                        new InterviewStep
                        {
                            Question = firstQuestion,
                            Answer = string.Empty,
                            AskedAt = DateTime.UtcNow
                        }
                    }
                };

                await newSessionRef.SetAsync(newSession);

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

                if (session.CurrentQuestionNumber < 5)
                {
                    // Sıradaki soruyu oluşturup ekliyoruz (CV içeriği dahil edildi)
                    int nextQuestionNumber = session.CurrentQuestionNumber + 1;
                    string nextQuestion = await _geminiService.GenerateQuestionAsync(session.JobTitle, cvContent, session.History, nextQuestionNumber);

                    session.History.Add(new InterviewStep
                    {
                        Question = nextQuestion,
                        Answer = string.Empty,
                        AskedAt = DateTime.UtcNow
                    });

                    session.CurrentQuestionNumber = nextQuestionNumber;
                }
                else
                {
                    // 5 soru tamamlandıysa değerlendirme oluşturup mülakatı sonlandırıyoruz (CV içeriği dahil edildi)
                    string evaluation = await _geminiService.GenerateEvaluationAsync(session.JobTitle, cvContent, session.History);
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
