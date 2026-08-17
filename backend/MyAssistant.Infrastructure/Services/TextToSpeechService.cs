using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyAssistant.Application.Interfaces;

namespace MyAssistant.Infrastructure.Services;

public class TextToSpeechService : ITextToSpeechService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TextToSpeechService> _logger;

    public TextToSpeechService(IConfiguration configuration, ILogger<TextToSpeechService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_configuration["TTS_API_KEY"]);

    public Task<byte[]?> SynthesizeAsync(string text, string language, double speed = 1.0, double volume = 1.0, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Text-to-speech called for language {Language}. " +
            "This endpoint is a placeholder for server-side providers (Azure/Google/OpenAI). " +
            "By default speech synthesis happens locally in the browser via speechSynthesis.",
            language);
        return Task.FromResult<byte[]?>(null);
    }

    public string GetMimeType() => "audio/mpeg";
}
