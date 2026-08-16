using MyAssistant.Application.AI;

namespace MyAssistant.Application.Interfaces;

public interface ITokenService
{
    string CreateAccessToken(Domain.Entities.User user);
    string GenerateRefreshToken();
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

/// <summary>
/// AI provider abstraction. Implementations: MockAIService, OpenAIService, AzureOpenAIService, GeminiService.
/// </summary>
public interface IAssistantAiService
{
    Task<AssistantResult> ProcessCommandAsync(AssistantRequest request);
    Task<IntentResult> DetectIntentAsync(AssistantRequest request);
}

public interface ISpeechRecognitionService
{
    Task<SpeechRecognitionResult> RecognizeAsync(string audioBase64, string? languageCode = null);
    Task<string> DetectLanguageAsync(string text);
    bool IsAvailable { get; }
}

public interface ITextToSpeechService
{
    Task<string> SynthesizeAsync(string text, string? languageCode = null);
    bool IsAvailable { get; }
}

public interface IWakeWordService
{
    event EventHandler? WakeWordDetected;
    Task<bool> StartAsync();
    Task StopAsync();
    bool IsRunning { get; }
    bool IsSupported { get; }
}

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Role { get; }
}

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
    Task<DateTime> GetNowAsync(string timezone);
}