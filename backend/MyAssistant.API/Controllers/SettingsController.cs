using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyAssistant.Application.Common;
using MyAssistant.Application.DTOs.Settings;
using MyAssistant.Application.Interfaces;

namespace MyAssistant.API.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settings;
    private readonly ICurrentUserService _currentUser;

    public SettingsController(ISettingsService settings, ICurrentUserService currentUser)
    {
        _settings = settings;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await _settings.GetAsync(_currentUser.UserId!.Value, ct);
        return Ok(ApiResponse<UserSettingsDto>.Ok(result));
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateSettingsRequest request, CancellationToken ct)
    {
        var result = await _settings.UpdateAsync(_currentUser.UserId!.Value, request, ct);
        return Ok(ApiResponse<UserSettingsDto>.Ok(result, "Settings updated."));
    }
}
