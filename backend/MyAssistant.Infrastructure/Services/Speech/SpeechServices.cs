using Microsoft.Extensions.Logging;
using MyAssistant.Application.AI;
using MyAssistant.Application.Interfaces;

namespace MyAssistant.Infrastructure.Services.Speech;

/// <summary>Development mock for speech recognition that returns a canned transcript.</summary>
public class MockSpeechService : ISpeechRecognitionService
{
    private readonly ILogger<MockSpeechService> _logger;
    public MockSpeechService(ILogger<MockSpeechService> logger) => _logger = logger;

    public bool IsAvailable => true;

    public Task<SpeechRecognitionResult> RecognizeAsync(string audioBase64, string? languageCode = null)
    {
        _logger.LogInformation("Mock speech recognition invoked (audio bytes: {Bytes})", audioBase64?.Length ?? 0);
        return Task.FromResult(new SpeechRecognitionResult
        {
            Success = true,
            Text = "Remind me tomorrow at 9 AM to call Ravi",
            Confidence = 1.0,
            DetectedLanguage = languageCode ?? "en-IN"
        });
    }

    public Task<string> DetectLanguageAsync(string text) => Task.FromResult(LanguageDetector.Detect(text));
}

public static class LanguageDetector
{
    public static string Detect(string text)
    {
        int devanagari = 0, telugu = 0, latin = 0;
        foreach (var ch in text)
        {
            if (ch is >= '\u0900' and <= '\u097F') devanagari++;
            else if (ch is >= '\u0C00' and <= '\u0C7F') telugu++;
            else if (ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z') latin++;
        }
        if (devanagari > telugu && devanagari > 0) return "hi-IN";
        if (telugu > devanagari && telugu > 0) return "te-IN";
        return "en-IN";
    }
}

/// <summary>Mock TTS returning a placeholder audio payload (recognized downstream by the app).</summary>
public class MockTtsService : ITextToSpeechService
{
    public bool IsAvailable => true;

    public Task<string> SynthesizeAsync(string text, string? languageCode = null)
        => Task.FromResult($"MOCK_AUDIO:{text}");
}

/// <summary>No-op wake word service. Real wake word detection is done on device with
/// on-device models (Porcupine/Picovoice); this server stub keeps the interface consistent.</summary>
public class NoopWakeWordService : IWakeWordService
{
    public event EventHandler? WakeWordDetected;
    public bool IsRunning => false;
    public bool IsSupported => false;
    public Task<bool> StartAsync() => Task.FromResult(false);
    public Task StopAsync() => Task.CompletedTask;
}