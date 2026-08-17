namespace MyAssistant.Application.Interfaces;

public interface IWakeWordService
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
    bool IsListening { get; }
    event EventHandler<WakeWordEventArgs>? WakeWordDetected;
}

public class WakeWordEventArgs : EventArgs
{
    public string WakeWord { get; set; } = "Assistant";
    public float Confidence { get; set; }
}
