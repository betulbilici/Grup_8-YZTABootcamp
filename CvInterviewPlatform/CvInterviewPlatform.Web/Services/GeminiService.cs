using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Google.GenAI;
using Google.GenAI.Types;
using CvInterviewPlatform.Web.Models;

namespace CvInterviewPlatform.Web.Services
{
    public class GeminiService
    {
        private readonly Client _client;
        private const string ModelName = "gemini-2.5-flash";

        public GeminiService(IConfiguration configuration)
        {
            string? apiKey = configuration["Gemini:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                // Çevre değişkenine geri düşüş (fallback) asistanı
                apiKey = System.Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            }

            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("Gemini API Key is not configured. Please add Gemini:ApiKey to appsettings.json or set GEMINI_API_KEY environment variable.");
            }

            _client = new Client(apiKey: apiKey);
        }

        private Content GetSystemInstruction()
        {
            return new Content
            {
                Parts = new List<Part>
                {
                    new Part { Text = "Sen 10 yıllık deneyime sahip, kıdemli bir İnsan Kaynakları (İK) uzmanısın. Adaylarla iş mülakatları gerçekleştiriyorsun. " +
                                      "Tavrın profesyonel, yapıcı, gözlemci ve mülakat tekniklerine (STAR metodu vb.) hakimdir. " +
                                      "Adaya soracağın sorular, başvurduğu pozisyona uygun, hem teknik hem de yetkinlik bazlı (soft skills) olmalıdır. " +
                                      "Mülakat boyunca adaya karşı saygılı ve destekleyici bir dil kullanırsın. " +
                                      "Soruları sorarken ve değerlendirme yaparken Türkçe dil kurallarına özen gösterirsin." }
                }
            };
        }

        private string GetDifficultyInstruction(string level)
        {
            switch (level)
            {
                case "Junior":
                    return "Zorluk seviyesi: Junior. Sorular temel kavram bilgisine, öğrenme isteğine, staj ve okul projelerine odaklansın; \"nasıl öğrendin\" tarzı sorular sor.";
                case "Senior":
                    return "Zorluk seviyesi: Senior. Sorular mimari kararlara, teknik liderliğe, ödünleşim (trade-off) analizine odaklansın; \"neden bu yaklaşımı seçtin ve alternatifi neydi\" tarzı sorular sor.";
                case "Mid":
                default:
                    return "Zorluk seviyesi: Mid. Sorular uygulamalı senaryolara, takım içi problem çözmeye, hata ayıklama deneyimine odaklansın; \"şöyle bir durumda ne yapardın\" tarzı sorular sor.";
            }
        }

        private string GetModeInstruction(string mode)
        {
            if (mode == "Realistic")
            {
                return "Mülakat modu: Gerçekçi. Ton doğrudan ve kısa olsun, gereksiz nezaket cümlesi kurma, gerçek bir mülakatın temposunu yansıt.";
            }

            return "Mülakat modu: Hazırlık. Ton destekleyici olsun, sorudan önce adayı rahatlatan kısa bir bağlam cümlesi kur.";
        }

        public async Task<string> GenerateQuestionAsync(InterviewSession session, string cvContent, int questionNumber)
        {
            var systemInstruction = GetSystemInstruction();
            var history = session.History;

            string prompt = $"Adayın başvurduğu pozisyon: {session.JobTitle}\n";
            prompt += $"{GetDifficultyInstruction(session.DifficultyLevel)}\n";
            prompt += $"{GetModeInstruction(session.Mode)}\n";
            if (!string.IsNullOrEmpty(cvContent))
            {
                prompt += $"Adayın Özgeçmiş (CV) Bilgileri:\n{cvContent}\n\n";
            }
            prompt += $"Şu anki soru numarası: {questionNumber} / {session.TotalQuestions}\n\n";

            if (history.Count == 0 || questionNumber == 1)
            {
                prompt += "Mülakatın ilk sorusunu sor. Adayı sıcak bir şekilde selamla ve pozisyona yönelik ilk sorunu yönelt. Adayın özgeçmişindeki (CV) deneyim ve eğitim bilgilerini göz önünde bulundurarak, hem başvurulan pozisyonun gereksinimlerine hem de adayın geçmişine uygun ilk sorunu yönelt. Sadece İK uzmanının sorusunu döndür, başka hiçbir açıklama veya ek metin ekleme.";
            }
            else
            {
                prompt += "Mülakattaki önceki diyalog geçmişi aşağıdadır:\n";
                for (int i = 0; i < history.Count; i++)
                {
                    var step = history[i];
                    if (!string.IsNullOrEmpty(step.Question))
                    {
                        prompt += $"İK Uzmanı (Soru {i+1}): {step.Question}\n";
                    }
                    if (!string.IsNullOrEmpty(step.Answer))
                    {
                        prompt += $"Aday (Cevap {i+1}): {step.Answer}\n";
                    }
                }
                prompt += $"\nŞimdi adaydan gelen son cevabı ve adayın özgeçmişini (CV) analiz et ve buna göre {questionNumber}. soruyu sor. Soruların hem başvurulan pozisyonla hem de adayın özgeçmişindeki niteliklerle/deneyimlerle uyumlu ve onları test edecek nitelikte olmasına özen göster. Sadece soruyu döndür, başka hiçbir açıklama veya ek metin ekleme.";
            }

            try
            {
                var response = await _client.Models.GenerateContentAsync(
                    model: ModelName,
                    contents: prompt,
                    config: new GenerateContentConfig
                    {
                        SystemInstruction = systemInstruction,
                        Temperature = 0.7f
                    }
                );

                string? questionText = response.Candidates?[0]?.Content?.Parts?[0]?.Text;
                return questionText?.Trim() ?? "Pozisyonla ilgili detaylı deneyimlerinizden bahseder misiniz?";
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"GenerateQuestionAsync hata: {ex.Message}");
                return $"Bir hata oluştu, ancak mülakata devam edelim. {session.JobTitle} alanındaki deneyimlerinizden ve bu pozisyonu neden istediğinizden bahseder misiniz?";
            }
        }

        public async Task<string> GenerateHintAsync(string question, string jobTitle, string difficulty)
        {
            var systemInstruction = GetSystemInstruction();

            string prompt = $"Adayın başvurduğu pozisyon: {jobTitle}\n" +
                             $"{GetDifficultyInstruction(difficulty)}\n" +
                             $"Adaya sorulan soru: {question}\n\n" +
                             "Adaya bu soruyu cevaplaması için bir ipucu ver. ÖNEMLİ: Cevabı yazma, sadece adayın hangi konulara/noktalara değinmesi gerektiğini 2-3 madde halinde kısaca söyle. Sadece maddeleri döndür, başka açıklama ekleme.";

            try
            {
                var response = await _client.Models.GenerateContentAsync(
                    model: ModelName,
                    contents: prompt,
                    config: new GenerateContentConfig
                    {
                        SystemInstruction = systemInstruction,
                        Temperature = 0.7f
                    }
                );

                string? hintText = response.Candidates?[0]?.Content?.Parts?[0]?.Text;
                return hintText?.Trim() ?? "Şu an ipucu üretilemedi, kendi deneyimlerinizden somut bir örnekle başlayabilirsiniz.";
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"GenerateHintAsync hata: {ex.Message}");
                return "Şu an ipucu üretilemedi, kendi deneyimlerinizden somut bir örnekle başlayabilirsiniz.";
            }
        }

        public async Task<string> GenerateEvaluationAsync(InterviewSession session, string cvContent)
        {
            var systemInstruction = GetSystemInstruction();
            var history = session.History;

            string prompt = $"Adayın başvurduğu pozisyon: {session.JobTitle}\n";
            if (!string.IsNullOrEmpty(cvContent))
            {
                prompt += $"Adayın Özgeçmiş (CV) Bilgileri:\n{cvContent}\n\n";
            }
            prompt += $"Mülakat tamamlandı. Aşağıda {session.TotalQuestions} soruluk mülakatın tam geçmişi bulunmaktadır:\n\n";

            for (int i = 0; i < history.Count; i++)
            {
                var step = history[i];
                prompt += $"İK Uzmanı (Soru {i+1}): {step.Question}\n";
                prompt += $"Aday (Cevap {i+1}): {step.Answer}\n\n";
            }

            prompt += "Lütfen 10 yıllık kıdemli bir İK uzmanı olarak bu mülakatı detaylıca değerlendir. Değerlendirmeyi yaparken adayın cevaplarının özgeçmişindeki (CV) beyanlarıyla ne derece uyuştuğunu ve başvurulan pozisyonun gereksinimlerine göre ne kadar yetkin olduğunu da analiz et. \n" +
                      "ÖNEMLİ KURAL: Raporun en başına 'Değerlendirme Raporu: ...', 'Aday Adı: ...' ve 'Mülakat Tarihi: ...' üst bilgilerini ekle; ancak 'Değerlendiren: ...' veya İK Uzmanı adı/unvanı gibi hiçbir alanı rapora EKLEME.\n\n" +
                      "Değerlendirme raporunda sırasıyla şu başlıklar yer alsın:\n" +
                      "1. **Genel İzlenim**: Adayın mülakat genelindeki duruşu, profesyonelliği ve CV-cevap tutarlılığı.\n" +
                      "2. **Teknik ve Mesleki Yetkinlikler**: Adayın pozisyona dair teknik bilgisi ve cevaplarının derinliği.\n" +
                      "3. **Sosyal/Yumuşak Yetkinlikler (Soft Skills)**: İletişim becerisi, problem çözme, kendini ifade etme yeteneği.\n" +
                      "4. **Güçlü Yönler**: Adayın en çok öne çıkan olumlu özellikleri.\n" +
                      "5. **Geliştirilmesi Gereken Alanlar**: Adayın kendisini geliştirmesi önerilen noktalar.\n" +
                      "6. **Nihai Karar ve Puan**: Mülakata dair genel bir puan (10 üzerinden) ve işe alım kararına dair tavsiyen (Olumlu/Olumsuz/Geliştirilebilir).\n" +
                      "7. **Soru-Cevap Mentörlük Analizi**: Adayın mülakatta karşılaştığı her soru (1'den 5'e kadar) için sırasıyla şu bilgileri detaylıca listele:\n" +
                      "   * **Soru X**: [Sorulan Soru]\n" +
                      "   * *Adayın Verdiği Yanıt*: [Adayın cevabının kısa özeti ve analizi]\n" +
                      "   * *İdeal Cevap Örneği (STAR Metoduyla)*: [Bu soruya verilmesi gereken örnek, profesyonel, teknik ve yetkinlik bazlı ideal cevap]\n" +
                      "   * *Geliştirme Önerisi*: [Adayın bu yanıtı bir dahaki sefere daha iyi sunabilmesi için 2 somut tavsiye (örn: 'şundan bahsetmeliydin', 'şu teknolojiyi vurgulamalısın')]\n\n" +
                      $"Bu aday {session.DifficultyLevel} seviye pozisyon için değerlendiriliyor. Puanlamayı bu seviyenin beklentilerine göre kalibre et; junior bir adayı senior standardıyla değerlendirme.\n\n" +
                      "Lütfen raporu profesyonel ve kurumsal bir dille, Markdown formatında oluştur.";

            try
            {
                var response = await _client.Models.GenerateContentAsync(
                    model: ModelName,
                    contents: prompt,
                    config: new GenerateContentConfig
                    {
                        SystemInstruction = systemInstruction,
                        Temperature = 0.5f
                    }
                );

                string? evaluationText = response.Candidates?[0]?.Content?.Parts?[0]?.Text;
                return evaluationText?.Trim() ?? "Değerlendirme raporu oluşturulamadı.";
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"GenerateEvaluationAsync hata: {ex.Message}");
                return "Değerlendirme oluşturulurken bir hata oluştu, lütfen daha sonra tekrar deneyin.";
            }
        }
    }
}
