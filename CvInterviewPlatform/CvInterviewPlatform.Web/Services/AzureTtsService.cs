using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CvInterviewPlatform.Web.Services
{
    // Soruların Azure Neural TTS (tr-TR-EmelNeural) ile seslendirilmesi — tarayıcının
    // yerleşik speechSynthesis'i Linux/Chrome'da düşük kaliteli/robotik ses üretiyordu.
    // Azure'ın ücretsiz (F0) katmanı, sınır aşılınca otomatik faturalandırma YAPMIYOR
    // (sadece kota hatası döner), bu yüzden Google Cloud TTS yerine tercih edildi.
    public class AzureTtsService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AzureTtsService> _logger;
        private readonly string _subscriptionKey;
        private readonly string _region;

        // Ses klipleri küçük (~50-100KB) ve mülakat başına birkaç kez üretiliyor —
        // basit bir bellek-içi önbellek yeterli, ek bir eviction mekanizması gerekmiyor.
        private readonly ConcurrentDictionary<string, byte[]> _audioCache = new();

        public AzureTtsService(HttpClient httpClient, IConfiguration configuration, ILogger<AzureTtsService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _subscriptionKey = configuration["Azure:Speech:SubscriptionKey"] ?? "";
            _region = configuration["Azure:Speech:Region"] ?? "westeurope";
        }

        public static string BuildCacheKey(string sessionId, int questionNumber) => $"{sessionId}:{questionNumber}";

        public byte[]? TryGetCached(string cacheKey)
        {
            return _audioCache.TryGetValue(cacheKey, out var bytes) ? bytes : null;
        }

        public async Task SynthesizeAndCacheAsync(string text, string cacheKey)
        {
            if (string.IsNullOrEmpty(_subscriptionKey))
            {
                _logger.LogWarning("Azure Speech anahtarı tanımlı değil, ses üretimi atlanıyor.");
                return;
            }

            try
            {
                string ssml = BuildSsml(text);
                var request = new HttpRequestMessage(HttpMethod.Post, $"https://{_region}.tts.speech.microsoft.com/cognitiveservices/v1")
                {
                    Content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml")
                };
                request.Headers.Add("Ocp-Apim-Subscription-Key", _subscriptionKey);
                request.Headers.Add("X-Microsoft-OutputFormat", "audio-16khz-64kbitrate-mono-mp3");
                request.Headers.UserAgent.Add(new ProductInfoHeaderValue("CvInterviewPlatform", "1.0"));

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Azure TTS isteği başarısız ({response.StatusCode}): {errorBody}");
                    return;
                }

                byte[] audioBytes = await response.Content.ReadAsByteArrayAsync();
                _audioCache[cacheKey] = audioBytes;
            }
            catch (System.Exception ex)
            {
                _logger.LogError($"Azure TTS hata: {ex.Message}");
            }
        }

        private static string BuildSsml(string text)
        {
            string escaped = SecurityElement.Escape(text) ?? "";
            return $"<speak version='1.0' xml:lang='tr-TR'>" +
                   $"<voice xml:lang='tr-TR' name='tr-TR-EmelNeural'>" +
                   $"<prosody rate='0%'>{escaped}</prosody>" +
                   $"</voice></speak>";
        }
    }
}
