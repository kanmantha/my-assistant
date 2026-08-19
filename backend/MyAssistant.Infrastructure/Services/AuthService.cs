using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using MyAssistant.Application.Common;
using MyAssistant.Application.Interfaces;
using MyAssistant.Domain.Entities;

namespace MyAssistant.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly ITokenService _tokens;
    private readonly IPasswordHasher _hasher;
    private readonly ISubscriptionService _subscriptions;
    private readonly ILogger<AuthService> _logger;

    private static readonly ConcurrentDictionary<string, (Guid UserId, DateTime Expiry)> _resetTokens = new();

    public AuthService(IUnitOfWork uow, ITokenService tokens, IPasswordHasher hasher, ISubscriptionService subscriptions, ILogger<AuthService> logger)
    {
        _uow = uow;
        _tokens = tokens;
        _hasher = hasher;
        _subscriptions = subscriptions;
        _logger = logger;
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request)
    {
        var existing = await _uow.Users.FirstOrDefaultAsync(u => u.Email == request.Email.Trim().ToLowerInvariant());
        if (existing is not null) throw new AppError("An account with this email already exists", 409, "EMAIL_EXISTS");

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = _hasher.Hash(request.Password),
            Phone = request.Phone ?? string.Empty,
            PreferredLanguage = request.PreferredLanguage ?? "en-IN",
            Timezone = request.Timezone ?? "Asia/Kolkata",
            Role = UserRole.User
        };

        await _uow.Users.AddAsync(user);
        await _uow.SaveChangesAsync();

        await _uow.UserSettings.AddAsync(new UserSettings
        {
            UserId = user.Id,
            Language = user.PreferredLanguage,
            Timezone = user.Timezone
        });
        await _uow.SaveChangesAsync();

        _logger.LogInformation("New user registered: {Email}", user.Email);
        return await BuildAuthResultAsync(user);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        var user = await _uow.Users.FirstOrDefaultAsync(u => u.Email == request.Email.Trim().ToLowerInvariant());
        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash))
            throw new AppError("Invalid email or password", 401, "INVALID_CREDENTIALS");
        if (user.IsSuspended) throw new AppError("This account has been suspended", 403, "ACCOUNT_SUSPENDED");
        if (!user.IsActive) throw new AppError("This account is inactive", 403, "ACCOUNT_INACTIVE");

        return await BuildAuthResultAsync(user);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken)
    {
        var token = await _uow.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken);
        if (token is null || token.IsRevoked || token.ExpiresAt < DateTime.UtcNow)
            throw new AppError("Invalid or expired refresh token", 401, "INVALID_REFRESH_TOKEN");

        var user = await _uow.Users.GetByIdAsync(token.UserId) ?? throw new AppError("User not found", 404, "USER_NOT_FOUND");

        token.IsRevoked = true;
        _uow.RefreshTokens.Update(token);
        await _uow.SaveChangesAsync();

        return await BuildAuthResultAsync(user);
    }

    public async Task LogoutAsync(Guid userId, string refreshToken)
    {
        var token = await _uow.RefreshTokens.FirstOrDefaultAsync(t => t.UserId == userId && t.Token == refreshToken);
        if (token is not null)
        {
            token.IsRevoked = true;
            _uow.RefreshTokens.Update(token);
            await _uow.SaveChangesAsync();
        }
    }

    public async Task ForgotPasswordAsync(string email)
    {
        // Always return success to avoid account enumeration.
        var user = await _uow.Users.FirstOrDefaultAsync(u => u.Email == email.Trim().ToLowerInvariant());
        if (user is not null)
        {
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            _resetTokens[token] = (user.Id, DateTime.UtcNow.AddHours(1));
            _logger.LogInformation("Password reset token generated for {Email}: {Token}", email, token);
        }
    }

    public async Task ResetPasswordAsync(string token, string newPassword)
    {
        if (!_resetTokens.TryRemove(token, out var entry) || entry.Expiry < DateTime.UtcNow)
            throw new AppError("Invalid or expired reset token", 400, "INVALID_RESET_TOKEN");

        var user = await _uow.Users.GetByIdAsync(entry.UserId)
            ?? throw new AppError("User not found", 404, "USER_NOT_FOUND");

        user.PasswordHash = _hasher.Hash(newPassword);
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
        _logger.LogInformation("Password reset completed for {Email}", user.Email);
    }

    public Task<AuthResult> GoogleAuthAsync(string idToken)
    {
        throw new AppError("Google OAuth requires configured client credentials.", 400, "OAUTH_UNAVAILABLE");
    }

    private async Task<AuthResult> BuildAuthResultAsync(User user)
    {
        var access = _tokens.CreateAccessToken(user);
        var refresh = _tokens.GenerateRefreshToken();

        await _uow.RefreshTokens.AddAsync(new RefreshToken
        {
            UserId = user.Id,
            Token = refresh,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        });
        await _uow.SaveChangesAsync();

        var usage = await _subscriptions.GetUsageAsync(user.Id);
        var plan = await GetEffectivePlanAsync(user.Id);

        return new AuthResult(access, refresh, DateTime.UtcNow.AddMinutes(60), BuildProfile(user, plan, usage));
    }

    private async Task<Plan?> GetEffectivePlanAsync(Guid userId)
    {
        var sub = await _uow.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId);
        if (sub is not null)
        {
            var p = await _uow.Plans.GetByIdAsync(sub.PlanId);
            if (p is not null) return p;
        }
        return await _uow.Plans.FirstOrDefaultAsync(p => p.Code == "FREE");
    }

    private UserProfile BuildProfile(User user, Plan? plan, UsageInfo usage)
    {
        return new UserProfile(
            user.Id,
            user.FullName,
            user.Email,
            user.Phone,
            user.PreferredLanguage,
            user.Timezone,
            user.Role.ToString(),
            user.IsActive,
            plan?.Code,
            usage.AiRequests,
            usage.AiLimit == int.MaxValue ? -1 : usage.AiLimit,
            usage.VoiceRequests,
            usage.VoiceLimit == int.MaxValue ? -1 : usage.VoiceLimit);
    }
}