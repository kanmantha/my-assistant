using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MyAssistant.Application.AI;
using MyAssistant.Application.Interfaces;

namespace MyAssistant.Infrastructure.Services.Speech;

/// <summary>Google Cloud Speech-to-Text via the v1 REST API (requires GOOGLE_APPLICATION_CREDENTIALS or SPEECH_API_KEY).</summary>
public class GoogleSpeechService : ISpeechRecognitionService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger<GoogleSpeechService> _logger;

    public GoogleSpeechService(string apiKey, ILogger<GoogleSpeechService> logger)
    {
        _http = new HttpClient();
        _apiKey = apiKey;
        _logger = logger;
    }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<SpeechRecognitionResult> RecognizeAsync(string audioBase64, string? languageCode = null)
    {
        if (!IsAvailable)
            return new SpeechRecognitionResult { Success = false, Error = "SPEECH_API_KEY not configured" };

        var languageMap = new Dictionary<string, string>
        {
            ["en-IN"] = "en-IN",
            ["hi-IN"] = "hi-IN",
            ["te-IN"] = "te-IN"
        };
        var lang = languageCode ?? "en-IN";

        var body = new
        {
            config = new
            {
                encoding = "LINEAR16",
                sampleRateHertz = 16000,
                languageCode = languageMap.GetValueOrDefault(lang, "en-IN"),
                maxAlternatives = 1
            },
            audio = new { content = audioBase64 }
        };

        var url = $"https://speech.googleapis.com/v1/speech:recognize?key={_apiKey}";
        using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync(url, content);
        var respBody = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("Google STT returned {Status}: {Body}", (int)resp.StatusCode, respBody);
            return new SpeechRecognitionResult { Success = false, Error = "Speech recognition failed" };
        }

        try
        {
            using var doc = JsonDocument.Parse(respBody);
            var results = doc.RootElement.TryGetProperty("results", out var r) && r.GetArrayLength() > 0 ? r[0] : new JsonElement();
            var text = results.TryGetProperty("alternatives", out var alts) && alts.GetArrayLength() > 0
                ? alts[0].GetProperty("transcript").GetString()
                : null;
            var confidence = results.TryGetProperty("alternatives", out var alt2) && alt2.GetArrayLength() > 0
                ? alt2[0].TryGetProperty("confidence", out var c) ? c.GetDouble() : 1.0
                : 1.0;

            return new SpeechRecognitionResult { Success = true, Text = text, Confidence = confidence, DetectedLanguage = lang };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Google Speech response");
            return new SpeechRecognitionResult { Success = false, Error = "Parse failure" };
        }
    }

    public Task<string> DetectLanguageAsync(string text) => Task.FromResult(LanguageDetector.Detect(text));
}

/// <summary>Google Cloud Text-to-Speech via the v1 REST API.</summary>
public class GoogleTtsService : ITextToSpeechService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger<GoogleTtsService> _logger;

    public GoogleTtsService(string apiKey, ILogger<GoogleTtsService> logger)
    {
        _http = new HttpClient();
        _apiKey = apiKey;
        _logger = logger;
    }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<string> SynthesizeAsync(string text, string? languageCode = null)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("Google TTS API key is not configured. Set the GOOGLE_TTS_API_KEY environment variable.");

        var voiceMap = new Dictionary<string, (string Lang, string Name)>
        {
            ["en-IN"] = ("en-IN", "en-IN-Standard-C"),
            ["hi-IN"] = ("hi-IN", "hi-IN-Standard-A"),
            ["te-IN"] = ("te-IN", "te-IN-Standard-A")
        };
        var (lang, name) = voiceMap.GetValueOrDefault(languageCode ?? "en-IN", ("en-IN", "en-IN-Standard-C"));

        var body = new
        {
            input = new { text },
            voice = new { languageCode = lang, name },
            audioConfig = new { audioEncoding = "LINEAR16", speakingRate = 1.0 }
        };

        var url = $"https://texttospeech.googleapis.com/v1/text:synthesize?key={_apiKey}";
        using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync(url, content);
        var respBody = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("Google TTS returned {Status}: {Body}", (int)resp.StatusCode, respBody);
            throw new InvalidOperationException($"Google TTS failed with status {(int)resp.StatusCode}");
        }

        try
        {
            using var doc = JsonDocument.Parse(respBody);
            return doc.RootElement.GetProperty("audioContent").GetString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TTS parse failure");
            throw new InvalidOperationException("Failed to parse Google TTS response", ex);
        }
    }
}