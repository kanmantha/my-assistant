using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using MyAssistant.Application.DTOs.Auth;
using MyAssistant.Application.Interfaces;
using MyAssistant.Domain.Entities;
using MyAssistant.Infrastructure.Auth;
using MyAssistant.Infrastructure.Data;
using MyAssistant.Infrastructure.Email;
using MyAssistant.Infrastructure.Services;

namespace MyAssistant.Tests;

public class AuthServiceTests
{
    private static Mock<UserManager<AppUser>> CreateUserManager()
    {
        var store = new Mock<IUserStore<AppUser>>();
        return new Mock<UserManager<AppUser>>(
            store.Object,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<AppUser>(),
            Array.Empty<IUserValidator<AppUser>>(),
            Array.Empty<IPasswordValidator<AppUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            new NullLogger<UserManager<AppUser>>());
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options);

    private static AuthService CreateService(
        Mock<UserManager<AppUser>> userManager,
        Mock<IEmailSender> email,
        EmailOptions? emailOptions = null) =>
        new(
            userManager.Object,
            null!,
            null!,
            Options.Create(new JwtOptions()),
            CreateContext(),
            new NullLogger<AuthService>(),
            email.Object,
            Options.Create(emailOptions ?? new EmailOptions()));

    [Fact]
    public async Task ForgotPasswordAsync_ExistingUser_SendsEmailWithResetLink()
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = "demo@example.com",
            UserName = "demo@example.com",
            FirstName = "Demo"
        };
        var userManager = CreateUserManager();
        userManager.Setup(um => um.FindByEmailAsync("demo@example.com")).ReturnsAsync(user);
        userManager.Setup(um => um.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset-token/abc+=");

        var email = new Mock<IEmailSender>();
        var service = CreateService(userManager, email,
            new EmailOptions { FrontendUrl = "http://localhost:5173/" });

        await service.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "demo@example.com" });

        email.Verify(e => e.SendAsync(
            It.Is<EmailMessage>(m =>
                m.To == "demo@example.com"
                && m.Subject == "Reset your MyAssistant password"
                && m.Body.Contains("http://localhost:5173/reset-password")
                && m.Body.Contains("demo%40example.com")
                && m.Body.Contains("reset-token%2Fabc%2B%3D")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForgotPasswordAsync_UnknownUser_DoesNotSendEmail()
    {
        var userManager = CreateUserManager();
        userManager.Setup(um => um.FindByEmailAsync("missing@example.com"))
            .ReturnsAsync((AppUser?)null);

        var email = new Mock<IEmailSender>();
        var service = CreateService(userManager, email);

        await service.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "missing@example.com" });

        email.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LogEmailSender_WritesMessageToLogger()
    {
        var logger = new TestLogger<LogEmailSender>();
        var sender = new LogEmailSender(logger);

        await sender.SendAsync(new EmailMessage("a@b.c", "Subject", "Body"));

        logger.Messages.Should().HaveCount(1);
        logger.Messages[0].Should().Contain("a@b.c").And.Contain("Subject").And.Contain("Body");
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
