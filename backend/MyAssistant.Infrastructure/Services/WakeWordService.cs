using Microsoft.Extensions.Logging;
using MyAssistant.Application.Interfaces;

namespace MyAssistant.Infrastructure.Services;

public class WakeWordService : IWakeWordService
{
    private readonly ILogger<WakeWordService> _logger;

    public WakeWordService(ILogger<WakeWordService> logger)
    {
        _logger = logger;
    }

    public bool IsListening { get; private set; }

#pragma warning disable CS0067
    public event EventHandler<WakeWordEventArgs>? WakeWordDetected;
#pragma warning restore CS0067

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Wake word engine placeholder started. " +
            "Wake-word detection is implemented locally in the browser to avoid uploading continuous audio.");
        IsListening = true;
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        IsListening = false;
        return Task.CompletedTask;
    }
}
