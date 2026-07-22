using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;

namespace CvInterviewPlatform.Web.Models
{
    // Firestore'da string olarak saklanır (Mode property'si); bu enum sadece kod tarafında parse/switch için kullanılır.
    public enum InterviewMode
    {
        Preparation,
        Realistic
    }

    [FirestoreData]
    public class InterviewStep
    {
        [FirestoreProperty("question")]
        public string Question { get; set; } = string.Empty;

        [FirestoreProperty("answer")]
        public string Answer { get; set; } = string.Empty;

        [FirestoreProperty("askedAt")]
        public DateTime AskedAt { get; set; } = DateTime.UtcNow;
    }

    [FirestoreData]
    public class InterviewSession
    {
        [FirestoreDocumentId]
        public string SessionId { get; set; } = string.Empty;

        [FirestoreProperty("username")]
        public string Username { get; set; } = string.Empty;

        [FirestoreProperty("jobTitle")]
        public string JobTitle { get; set; } = string.Empty;

        [FirestoreProperty("startedAt")]
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        [FirestoreProperty("currentQuestionNumber")]
        public int CurrentQuestionNumber { get; set; } = 1;

        [FirestoreProperty("isCompleted")]
        public bool IsCompleted { get; set; } = false;

        [FirestoreProperty("history")]
        public List<InterviewStep> History { get; set; } = new();

        [FirestoreProperty("finalEvaluation")]
        public string? FinalEvaluation { get; set; }

        [FirestoreProperty("mode")]
        public string Mode { get; set; } = "Preparation";

        [FirestoreProperty("timeLimitSeconds")]
        public int TimeLimitSeconds { get; set; } = 300;

        [FirestoreProperty("difficultyLevel")]
        public string DifficultyLevel { get; set; } = "Mid";

        [FirestoreProperty("totalQuestions")]
        public int TotalQuestions { get; set; } = 5;
    }
}
