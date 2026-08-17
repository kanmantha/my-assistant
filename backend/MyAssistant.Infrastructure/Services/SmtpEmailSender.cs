using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyAssistant.Application.Interfaces;
using MyAssistant.Infrastructure.Email;

namespace MyAssistant.Infrastructure.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpClient _client;
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
#pragma warning disable SYSLIB0014
        _client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseSsl,
            Credentials = string.IsNullOrEmpty(_options.Username)
                ? null
                : new NetworkCredential(_options.Username, _options.Password)
        };
#pragma warning restore SYSLIB0014
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var mail = new MailMessage
        {
            From = new MailAddress(_options.From, "MyAssistant"),
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = false
        };
        mail.To.Add(message.To);
#pragma warning disable SYSLIB0014
        await _client.SendMailAsync(mail, cancellationToken);
#pragma warning restore SYSLIB0014
        _logger.LogInformation("Email sent to {To}", message.To);
    }
}
