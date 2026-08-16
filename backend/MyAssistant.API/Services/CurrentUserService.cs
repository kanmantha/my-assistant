using System.Security.Claims;
using MyAssistant.Application.Interfaces;

namespace MyAssistant.API.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _http;

    public CurrentUserService(IHttpContextAccessor http) => _http = http;

    public Guid? UserId
    {
        get
        {
            var id = _http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(id, out var g) ? g : null;
        }
    }

    public string? Role => _http.HttpContext?.User.FindFirstValue(ClaimTypes.Role);
}