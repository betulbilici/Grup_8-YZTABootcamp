using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Google.Cloud.Firestore;
using CvInterviewPlatform.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CvInterviewPlatform.Web.Controllers
{
    // Soru havuzu (semantic cache) için minimal admin paneli. Self-servis bir
    // rol atama akışı yok — ilk admin(ler) Firestore console'dan User.IsAdmin
    // alanı elle true yapılarak belirlenir (bkz. User.cs).
    public class AdminController : Controller
    {
        private readonly FirestoreDb _db;

        public AdminController(FirestoreService firestoreService)
        {
            _db = firestoreService.Db;
        }

        // Diğer controller'lardaki session-check desenine ek olarak IsAdmin kontrolü yapar.
        // Yetkisizse null döner ve action Forbid ile sonlanır.
        private async Task<User?> GetCurrentAdminOrNullAsync()
        {
            string username = HttpContext.Session.GetString("Username") ?? "";
            if (string.IsNullOrEmpty(username))
            {
                return null;
            }

            DocumentSnapshot userSnap = await _db.Collection("Users").Document(username).GetSnapshotAsync();
            if (!userSnap.Exists)
            {
                return null;
            }

            User user = userSnap.ConvertTo<User>();
            return user.IsAdmin ? user : null;
        }

        // Onay bekleyen roller listesi (GET)
        [HttpGet]
        public async Task<IActionResult> PendingRoles()
        {
            if (await GetCurrentAdminOrNullAsync() == null)
            {
                return Forbid();
            }

            try
            {
                Query query = _db.Collection("PendingRoles").WhereEqualTo("status", "pending");
                QuerySnapshot snapshot = await query.GetSnapshotAsync();

                List<PendingRole> pendingRoles = snapshot.Documents
                    .Select(d => d.ConvertTo<PendingRole>())
                    .OrderByDescending(p => p.OccurrenceCount)
                    .ToList();

                return View(pendingRoles);
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Bekleyen roller yüklenirken hata oluştu: {ex.Message}";
                return View(new List<PendingRole>());
            }
        }

        // Bekleyen rolü onaylayıp soru havuzuna ekler (POST)
        [HttpPost]
        public async Task<IActionResult> ApprovePendingRole(string id)
        {
            if (await GetCurrentAdminOrNullAsync() == null)
            {
                return Forbid();
            }

            try
            {
                DocumentReference pendingRef = _db.Collection("PendingRoles").Document(id);
                DocumentSnapshot pendingSnap = await pendingRef.GetSnapshotAsync();

                if (!pendingSnap.Exists)
                {
                    TempData["Error"] = "Bekleyen rol kaydı bulunamadı.";
                    return RedirectToAction("PendingRoles");
                }

                PendingRole pending = pendingSnap.ConvertTo<PendingRole>();

                DocumentReference poolRef = _db.Collection("QuestionPools").Document();
                QuestionPoolEntry poolEntry = new QuestionPoolEntry
                {
                    Id = poolRef.Id,
                    RoleLabel = pending.JobTitle,
                    DifficultyLevel = pending.DifficultyLevel,
                    Embedding = pending.Embedding,
                    Questions = pending.SampleQuestions,
                    ApprovedAt = DateTime.UtcNow
                };
                await poolRef.SetAsync(poolEntry);

                pending.Status = "approved";
                await pendingRef.SetAsync(pending);

                TempData["Success"] = $"\"{pending.JobTitle}\" ({pending.DifficultyLevel}) soru havuzuna eklendi.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Havuza eklenirken bir hata oluştu: {ex.Message}";
            }

            return RedirectToAction("PendingRoles");
        }

        // Bekleyen rolü yoksayar (POST)
        [HttpPost]
        public async Task<IActionResult> DismissPendingRole(string id)
        {
            if (await GetCurrentAdminOrNullAsync() == null)
            {
                return Forbid();
            }

            try
            {
                DocumentReference pendingRef = _db.Collection("PendingRoles").Document(id);
                DocumentSnapshot pendingSnap = await pendingRef.GetSnapshotAsync();

                if (pendingSnap.Exists)
                {
                    PendingRole pending = pendingSnap.ConvertTo<PendingRole>();
                    pending.Status = "dismissed";
                    await pendingRef.SetAsync(pending);
                    TempData["Success"] = "Kayıt yoksayıldı.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"İşlem sırasında bir hata oluştu: {ex.Message}";
            }

            return RedirectToAction("PendingRoles");
        }
    }
}
