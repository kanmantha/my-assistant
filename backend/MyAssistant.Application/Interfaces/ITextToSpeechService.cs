namespace MyAssistant.Application.Interfaces;

public interface ITextToSpeechService
{
    Task<byte[]?> SynthesizeAsync(string text, string language, double speed = 1.0, double volume = 1.0, CancellationToken cancellationToken = default);
    string GetMimeType();
    bool IsConfigured { get; }
}
