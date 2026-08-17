using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyAssistant.Application.Common;
using MyAssistant.Application.DTOs.Auth;
using MyAssistant.Application.Interfaces;
using MyAssistant.Domain.Entities;
using MyAssistant.Infrastructure.Auth;
using MyAssistant.Infrastructure.Data;
using MyAssistant.Infrastructure.Email;

namespace MyAssistant.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly JwtOptions _jwtOptions;
    private readonly AppDbContext _context;
    private readonly ILogger<AuthService> _logger;
    private readonly IEmailSender _emailSender;
    private readonly EmailOptions _emailOptions;

    public AuthService(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        ITokenService tokenService,
        IOptions<JwtOptions> jwtOptions,
        AppDbContext context,
        ILogger<AuthService> logger,
        IEmailSender emailSender,
        IOptions<EmailOptions> emailOptions)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _jwtOptions = jwtOptions.Value;
        _context = context;
        _logger = logger;
        _emailSender = emailSender;
        _emailOptions = emailOptions.Value;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null)
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var user = new AppUser
        {
            Email = request.Email,
            UserName = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            throw new ValidationException(result.Errors.Select(e => e.Description).ToList());
        }

        await _userManager.AddToRoleAsync(user, "User");

        _context.UserSettings.Add(new UserSettings { UserId = user.Id, WakeWordEnabled = true });
        _context.Subscriptions.Add(new Subscription { UserId = user.Id });
        await _context.SaveChangesAsync(cancellationToken);

        var roles = await _userManager.GetRolesAsync(user);
        return BuildAuthResponse(user, roles);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email)
                   ?? throw new UnauthorizedException("Invalid email or password.");

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            throw new UnauthorizedException("Invalid email or password.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        return BuildAuthResponse(user, roles);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var principal = _tokenService.GetPrincipalFromExpiredToken(request.AccessToken)
                        ?? throw new UnauthorizedException("Invalid access token.");
        var userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedException("Invalid token payload.");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString())
                   ?? throw new UnauthorizedException("User no longer exists.");

        if (user.RefreshToken != request.RefreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            throw new UnauthorizedException("Invalid or expired refresh token.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        user.RefreshToken = _tokenService.CreateRefreshToken();
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpiryDays);
        await _userManager.UpdateAsync(user);
        return BuildAuthResponse(user, roles);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null) return;

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var frontendUrl = _emailOptions.FrontendUrl.TrimEnd('/');
        var resetLink = $"{frontendUrl}/reset-password?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}";
        var body = string.Join("\n",
            $"Hello {user.FirstName},",
            "",
            "We received a request to reset your MyAssistant password. Open the link below to choose a new one:",
            "",
            resetLink,
            "",
            "If you did not request this, you can safely ignore this email.",
            "This link is valid for a limited time.");
        await _emailSender.SendAsync(new EmailMessage(
            user.Email!,
            "Reset your MyAssistant password",
            body), cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email)
                   ?? throw new NotFoundException("User not found.");

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            throw new ValidationException(result.Errors.Select(e => e.Description).ToList());
        }
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
                   ?? throw new NotFoundException("User not found.");

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            throw new ValidationException(result.Errors.Select(e => e.Description).ToList());
        }
    }

    public async Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
                   ?? throw new NotFoundException("User not found.");
        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        await _userManager.UpdateAsync(user);
        var roles = await _userManager.GetRolesAsync(user);
        return ToUserDto(user, roles);
    }

    public async Task<UserDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
                   ?? throw new NotFoundException("User not found.");
        var roles = await _userManager.GetRolesAsync(user);
        return ToUserDto(user, roles);
    }

    private AuthResponse BuildAuthResponse(AppUser user, IList<string> roles)
    {
        var accessToken = _tokenService.CreateAccessToken(user.Id, user.Email!, user.OrganizationId?.ToString(), roles);
        var refreshToken = _tokenService.CreateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpiryDays);
        _userManager.UpdateAsync(user).GetAwaiter().GetResult();

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes),
            User = ToUserDto(user, roles)
        };
    }

    private static UserDto ToUserDto(AppUser user, IList<string> roles) => new()
    {
        Id = user.Id,
        Email = user.Email ?? string.Empty,
        FirstName = user.FirstName,
        LastName = user.LastName,
        DisplayName = user.DisplayName,
        OrganizationId = user.OrganizationId?.ToString(),
        Roles = roles.ToArray()
    };
}
