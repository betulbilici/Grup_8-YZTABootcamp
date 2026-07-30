using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using CvInterviewPlatform.Web.Models;

namespace CvInterviewPlatform.Web.Services
{
    // Rol+zorluk seviyesi bazlı soru havuzu (semantic cache). Amaç: aynı rol için
    // daha önce admin onaylı bir soru havuzu varsa Gemini'ye hiç gitmeden soru
    // dönmek. Firestore'un idiomatic .NET istemcisinde native vektör araması
    // (FindNearest) yok (sadece düşük seviye V1 protobuf katmanında var), bu
    // yüzden eşleştirme burada, uygulama içinde brute-force cosine similarity
    // ile yapılıyor — havuz boyutu (rol×seviye kombinasyonu) küçük kalacağı için
    // bu yeterince hızlı.
    //
    // Bu servis diğerleri gibi (GeminiService, CvParserService vb.) asla exception
    // fırlatmaz: herhangi bir adım başarısız olursa null/false döner, çağıran
    // taraf (InterviewController) bunu "cache miss" sayıp mevcut canlı Gemini
    // akışına düşer. Mülakat akışı bu servis yüzünden asla kırılmamalı.
    public class QuestionPoolService
    {
        private const double SimilarityThreshold = 0.85;
        private const int MaxSampleQuestions = 5;

        private readonly FirestoreDb _db;
        private readonly GeminiService _geminiService;

        public QuestionPoolService(FirestoreService firestoreService, GeminiService geminiService)
        {
            _db = firestoreService.Db;
            _geminiService = geminiService;
        }

        private static double CosineSimilarity(List<double> a, List<double> b)
        {
            if (a.Count == 0 || a.Count != b.Count)
            {
                return 0.0;
            }

            double dot = 0.0, normA = 0.0, normB = 0.0;
            for (int i = 0; i < a.Count; i++)
            {
                dot += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            if (normA == 0.0 || normB == 0.0)
            {
                return 0.0;
            }

            return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        // Havuzda bu rol+seviyeye yeterince benzer, onaylı bir kayıt varsa ve
        // içinde bu oturumda henüz sorulmamış bir soru varsa döner. Yoksa null
        // döner — çağıran taraf mevcut Gemini akışına düşer.
        public async Task<string?> TryGetPooledQuestionAsync(string jobTitle, string difficultyLevel, IEnumerable<string> alreadyAskedQuestions)
        {
            try
            {
                List<double> queryEmbedding = await _geminiService.EmbedTextAsync(jobTitle);

                Query query = _db.Collection("QuestionPools").WhereEqualTo("difficultyLevel", difficultyLevel);
                QuerySnapshot snapshot = await query.GetSnapshotAsync();

                QuestionPoolEntry? bestMatch = null;
                double bestSimilarity = SimilarityThreshold;

                foreach (DocumentSnapshot doc in snapshot.Documents)
                {
                    QuestionPoolEntry entry = doc.ConvertTo<QuestionPoolEntry>();
                    double similarity = CosineSimilarity(queryEmbedding, entry.Embedding);
                    if (similarity >= bestSimilarity)
                    {
                        bestSimilarity = similarity;
                        bestMatch = entry;
                    }
                }

                if (bestMatch == null)
                {
                    return null;
                }

                HashSet<string> asked = new(alreadyAskedQuestions);
                return bestMatch.Questions.FirstOrDefault(q => !asked.Contains(q));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"TryGetPooledQuestionAsync hata: {ex.Message}");
                return null;
            }
        }

        // Havuzda eşleşme bulunamadığında (Gemini canlı soru ürettiğinde) fire-and-forget
        // çağrılır. Aynı rol+seviye için zaten bekleyen bir kayıt varsa örnek soruyu ekler
        // ve sayaci artırır; yoksa yeni bir PendingRole dokümanı oluşturur.
        public async Task RecordPendingRoleAsync(string jobTitle, string difficultyLevel, string generatedQuestion)
        {
            try
            {
                List<double> embedding = await _geminiService.EmbedTextAsync(jobTitle);

                Query query = _db.Collection("PendingRoles")
                    .WhereEqualTo("difficultyLevel", difficultyLevel)
                    .WhereEqualTo("status", "pending");
                QuerySnapshot snapshot = await query.GetSnapshotAsync();

                DocumentSnapshot? existingDoc = null;
                foreach (DocumentSnapshot doc in snapshot.Documents)
                {
                    PendingRole candidate = doc.ConvertTo<PendingRole>();
                    if (CosineSimilarity(embedding, candidate.Embedding) >= SimilarityThreshold)
                    {
                        existingDoc = doc;
                        break;
                    }
                }

                if (existingDoc != null)
                {
                    PendingRole pending = existingDoc.ConvertTo<PendingRole>();
                    pending.OccurrenceCount++;
                    if (pending.SampleQuestions.Count < MaxSampleQuestions && !pending.SampleQuestions.Contains(generatedQuestion))
                    {
                        pending.SampleQuestions.Add(generatedQuestion);
                    }
                    await existingDoc.Reference.SetAsync(pending);
                }
                else
                {
                    DocumentReference newRef = _db.Collection("PendingRoles").Document();
                    PendingRole pending = new PendingRole
                    {
                        Id = newRef.Id,
                        JobTitle = jobTitle,
                        DifficultyLevel = difficultyLevel,
                        Embedding = embedding,
                        SampleQuestions = new List<string> { generatedQuestion },
                        OccurrenceCount = 1,
                        FirstSeenAt = DateTime.UtcNow,
                        Status = "pending"
                    };
                    await newRef.SetAsync(pending);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"RecordPendingRoleAsync hata: {ex.Message}");
            }
        }
    }
}
