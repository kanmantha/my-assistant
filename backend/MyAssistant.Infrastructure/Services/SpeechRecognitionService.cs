using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyAssistant.Application.Interfaces;

namespace MyAssistant.Infrastructure.Services;

public class SpeechRecognitionService : ISpeechRecognitionService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SpeechRecognitionService> _logger;

    public SpeechRecognitionService(IConfiguration configuration, ILogger<SpeechRecognitionService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_configuration["SPEECH_API_KEY"]);

    public Task<SpeechRecognitionResult> RecognizeAsync(Stream audioStream, string language, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Speech recognition called for language {Language}. " +
            "This endpoint is a placeholder for server-side providers (Azure/Google/OpenAI). " +
            "By default transcription happens locally in the browser via the Web Speech API.",
            language);
        return Task.FromResult(new SpeechRecognitionResult
        {
            Success = false,
            Error = "Server-side speech recognition is not configured. The frontend uses the browser Web Speech API."
        });
    }

    public Task<SpeechRecognitionResult> RecognizeTextAsync(string base64Audio, string language, CancellationToken cancellationToken = default)
        => RecognizeAsync(new MemoryStream(Convert.FromBase64String(base64Audio)), language, cancellationToken);
}
