using System;
using System.Collections.Generic;
using Google.Cloud.Firestore;

namespace CvInterviewPlatform.Web.Models
{
    // Bir rol+zorluk seviyesi için admin onaylı, hazır soru havuzu.
    // GenerateQuestionAsync'e (Gemini) gitmeden önce burada eşleşme aranır.
    [FirestoreData]
    public class QuestionPoolEntry
    {
        [FirestoreDocumentId]
        public string Id { get; set; } = string.Empty;

        [FirestoreProperty("roleLabel")]
        public string RoleLabel { get; set; } = string.Empty;

        [FirestoreProperty("difficultyLevel")]
        public string DifficultyLevel { get; set; } = "Mid";

        [FirestoreProperty("embedding")]
        public List<double> Embedding { get; set; } = new();

        [FirestoreProperty("questions")]
        public List<string> Questions { get; set; } = new();

        [FirestoreProperty("approvedAt")]
        public DateTime ApprovedAt { get; set; } = DateTime.UtcNow;
    }

    // Havuzda eşleşme bulunamadığında (Gemini canlı soru ürettiğinde) düşen,
    // admin onayı bekleyen rol kaydı. Onaylanınca QuestionPoolEntry'e dönüşür.
    [FirestoreData]
    public class PendingRole
    {
        [FirestoreDocumentId]
        public string Id { get; set; } = string.Empty;

        [FirestoreProperty("jobTitle")]
        public string JobTitle { get; set; } = string.Empty;

        [FirestoreProperty("difficultyLevel")]
        public string DifficultyLevel { get; set; } = "Mid";

        [FirestoreProperty("embedding")]
        public List<double> Embedding { get; set; } = new();

        [FirestoreProperty("sampleQuestions")]
        public List<string> SampleQuestions { get; set; } = new();

        [FirestoreProperty("occurrenceCount")]
        public int OccurrenceCount { get; set; } = 0;

        [FirestoreProperty("firstSeenAt")]
        public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;

        [FirestoreProperty("status")]
        public string Status { get; set; } = "pending";
    }
}
