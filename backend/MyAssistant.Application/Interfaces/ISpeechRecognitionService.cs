using MyAssistant.Application.DTOs.Assistant;

namespace MyAssistant.Application.Interfaces;

public interface ISpeechRecognitionService
{
    Task<SpeechRecognitionResult> RecognizeAsync(Stream audioStream, string language, CancellationToken cancellationToken = default);
    Task<SpeechRecognitionResult> RecognizeTextAsync(string base64Audio, string language, CancellationToken cancellationToken = default);
    bool IsConfigured { get; }
}

public class SpeechRecognitionResult
{
    public bool Success { get; set; }
    public string? Text { get; set; }
    public string? Language { get; set; }
    public double Confidence { get; set; }
    public string? Error { get; set; }
}
