using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MyAssistant.Application.Interfaces;
using MyAssistant.Domain.Entities;

namespace MyAssistant.Infrastructure.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config) => _config = config;

    public string CreateAccessToken(User user)
    {
        var secret = _config["JWT_SECRET"] ?? _config["Jwt:Secret"] ?? "dev-secret-key-change-me-in-production-0123456789";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(GetInt("JWT_ACCESS_TOKEN_MINUTES") ?? GetInt("Jwt:AccessTokenMinutes") ?? 60);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("plan", user.Subscription?.Plan?.Code ?? "FREE")
        };

        var token = new JwtSecurityToken(
            issuer: _config["JWT_ISSUER"] ?? _config["Jwt:Issuer"] ?? "MyAssistant",
            audience: _config["JWT_AUDIENCE"] ?? _config["Jwt:Audience"] ?? "MyAssistant.Mobile",
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    private int? GetInt(string key)
        => int.TryParse(_config[key], out var v) ? v : (int?)null;
}

public class PasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);
    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}