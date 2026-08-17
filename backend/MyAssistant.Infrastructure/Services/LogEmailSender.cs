using Microsoft.Extensions.Logging;
using MyAssistant.Application.Interfaces;

namespace MyAssistant.Infrastructure.Services;

public class LogEmailSender : IEmailSender
{
    private readonly ILogger<LogEmailSender> _logger;

    public LogEmailSender(ILogger<LogEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[Email] To: {To} | Subject: {Subject} | Body: {Body}",
            message.To,
            message.Subject,
            message.Body);
        return Task.CompletedTask;
    }
}
