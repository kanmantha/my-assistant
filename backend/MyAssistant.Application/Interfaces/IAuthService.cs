using MyAssistant.Domain.Entities;

namespace MyAssistant.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request);
    Task<AuthResult> LoginAsync(LoginRequest request);
    Task<AuthResult> RefreshAsync(string refreshToken);
    Task LogoutAsync(Guid userId, string refreshToken);
    Task ForgotPasswordAsync(string email);
    Task ResetPasswordAsync(string token, string newPassword);
    Task<AuthResult> GoogleAuthAsync(string idToken);
}

public record RegisterRequest(string FullName, string Email, string Password, string? Phone = null, string? PreferredLanguage = "en-IN", string? Timezone = "Asia/Kolkata");
public record LoginRequest(string Email, string Password);
public record AuthResult(string AccessToken, string RefreshToken, DateTime ExpiresAt, UserProfile Profile);

public record UserProfile(
    Guid Id,
    string FullName,
    string Email,
    string Phone,
    string PreferredLanguage,
    string Timezone,
    string Role,
    bool IsActive,
    string? PlanCode,
    int UsageAi,
    int UsageAiLimit,
    int UsageVoice,
    int UsageVoiceLimit);