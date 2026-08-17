using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MyAssistant.Application.Interfaces;

namespace MyAssistant.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated => UserId.HasValue;

    public string? UserName => _httpContextAccessor.HttpContext?.User.Identity?.Name;

    public string? OrganizationId => _httpContextAccessor.HttpContext?.User.FindFirstValue("organization_id");
}
