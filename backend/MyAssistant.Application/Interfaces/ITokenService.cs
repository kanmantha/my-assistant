using System.Security.Claims;

namespace MyAssistant.Application.Interfaces;

public interface ITokenService
{
    string CreateAccessToken(Guid userId, string email, string? organizationId, IList<string> roles);
    string CreateRefreshToken();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
