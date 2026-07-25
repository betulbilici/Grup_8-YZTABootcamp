using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CvInterviewPlatform.Web.Services
{
    // Soruların Piper TTS (tr_TR-dfki-medium) ile seslendirilmesi — CvParserService
    // mikroservisindeki /tts endpoint'ine istek atıyor. Piper tamamen yerel çalışan
    // açık kaynak bir model olduğu için (Azure/Gemini'nin aksine) hesap, API key
    // veya kota derdi yok; tarayıcının yerleşik speechSynthesis'i Linux/Chrome'da
    // düşük kaliteli/robotik ses ürettiği için buna geçildi.
    public class PiperTtsService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PiperTtsService> _logger;
        private readonly string _baseUrl;

        // Ses klipleri küçük (~100-200KB) ve mülakat başına birkaç kez üretiliyor —
        // basit bir bellek-içi önbellek yeterli, ek bir eviction mekanizması gerekmiyor.
        private readonly ConcurrentDictionary<string, byte[]> _audioCache = new();

        public PiperTtsService(HttpClient httpClient, IConfiguration configuration, ILogger<PiperTtsService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            // Piper modeli önceden yüklü olduğu için (Docling'in aksine dosya başına
            // model indirmiyor), kısa bir zaman aşımı yeterli.
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            var url = configuration["ParserService:BaseUrl"] ?? "http://127.0.0.1:8000";
            _baseUrl = url.EndsWith("/") ? url : url + "/";
        }

        public static string BuildCacheKey(string sessionId, int questionNumber) => $"{sessionId}:{questionNumber}";

        public byte[]? TryGetCached(string cacheKey)
        {
            return _audioCache.TryGetValue(cacheKey, out var bytes) ? bytes : null;
        }

        public async Task SynthesizeAndCacheAsync(string text, string cacheKey)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}tts", new { text });
                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Piper TTS isteği başarısız ({response.StatusCode}): {errorBody}");
                    return;
                }

                byte[] audioBytes = await response.Content.ReadAsByteArrayAsync();
                _audioCache[cacheKey] = audioBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Piper TTS hata: {ex.Message}");
            }
        }
    }
}
