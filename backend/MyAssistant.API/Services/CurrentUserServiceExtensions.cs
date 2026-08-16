using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MyAssistant.Application.Common;

namespace MyAssistant.API.Services;

public static class CurrentUserServiceExtensions
{
    public static Guid GetUserId(this ControllerBase controller)
    {
        var id = controller.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(id, out var g)) return g;
        throw new AppError("User not authenticated", 401, "UNAUTHENTICATED");
    }
}