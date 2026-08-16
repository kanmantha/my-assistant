using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyAssistant.API.Services;
using MyAssistant.Application.Common;
using MyAssistant.Application.Interfaces;
using MyAssistant.Domain.Entities;

namespace MyAssistant.API.Controllers;

[ApiController]
[Authorize]
[Route("api/user")]
public class UserController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly ISubscriptionService _subscriptions;

    public UserController(IUnitOfWork uow, ISubscriptionService subscriptions)
    {
        _uow = uow;
        _subscriptions = subscriptions;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> Profile()
    {
        var user = await _uow.Users.GetByIdAsync(this.GetUserId()) ?? throw new AppError("User not found", 404);
        var usage = await _subscriptions.GetUsageAsync(user.Id);
        return Ok(ApiResponse<object>.Ok(new
        {
            user.Id,
            user.FullName,
            user.Email,
            user.Phone,
            user.PreferredLanguage,
            user.Timezone,
            user.Role,
            user.IsActive,
            usage
        }));
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request)
    {
        var user = await _uow.Users.GetByIdAsync(this.GetUserId()) ?? throw new AppError("User not found", 404);
        if (request.FullName is not null) user.FullName = request.FullName;
        if (request.Phone is not null) user.Phone = request.Phone;
        if (request.PreferredLanguage is not null) user.PreferredLanguage = request.PreferredLanguage;
        if (request.Timezone is not null) user.Timezone = request.Timezone;
        user.UpdatedAt = DateTime.UtcNow;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { message = "Profile updated" }));
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var uid = this.GetUserId();
        var settings = await _uow.UserSettings.FirstOrDefaultAsync(s => s.UserId == uid)
                       ?? new UserSettings { UserId = uid };
        return Ok(ApiResponse<object>.Ok(settings));
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(UpdateSettingsRequest request)
    {
        var uid = this.GetUserId();
        var settings = await _uow.UserSettings.FirstOrDefaultAsync(s => s.UserId == uid);
        if (settings is null)
        {
            settings = new UserSettings { UserId = uid };
            await _uow.UserSettings.AddAsync(settings);
        }
        if (request.Language is not null) settings.Language = request.Language;
        if (request.VoiceEnabled is not null) settings.VoiceEnabled = request.VoiceEnabled.Value;
        if (request.WakeWordEnabled is not null) settings.WakeWordEnabled = request.WakeWordEnabled.Value;
        if (request.NotificationsEnabled is not null) settings.NotificationsEnabled = request.NotificationsEnabled.Value;
        if (request.DefaultReminderMinutes is not null) settings.DefaultReminderMinutes = request.DefaultReminderMinutes.Value;
        if (request.Timezone is not null) settings.Timezone = request.Timezone;
        _uow.UserSettings.Update(settings);
        await _uow.SaveChangesAsync();

        // Mirror language into user record
        var user = await _uow.Users.GetByIdAsync(uid);
        if (user is not null && request.Language is not null)
        {
            user.PreferredLanguage = request.Language;
            _uow.Users.Update(user);
            await _uow.SaveChangesAsync();
        }
        return Ok(ApiResponse<object>.Ok(settings, "Settings updated"));
    }

    [HttpDelete("data")]
    public async Task<IActionResult> DeletePersonalData()
    {
        var uid = this.GetUserId();
        var user = await _uow.Users.GetByIdAsync(uid) ?? throw new AppError("User not found", 404);
        foreach (var n in await _uow.Notes.AllAsync(n => n.UserId == uid)) _uow.Notes.Remove(n);
        foreach (var t in await _uow.Tasks.AllAsync(t => t.UserId == uid)) _uow.Tasks.Remove(t);
        foreach (var r in await _uow.Reminders.AllAsync(r => r.UserId == uid)) _uow.Reminders.Remove(r);
        foreach (var a in await _uow.Appointments.AllAsync(a => a.UserId == uid)) _uow.Appointments.Remove(a);
        foreach (var u in await _uow.UsageRecords.AllAsync(x => x.UserId == uid)) _uow.UsageRecords.Remove(u);
        await _uow.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { }, "Personal data deleted"));
    }
}

public class UpdateProfileRequest
{
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? PreferredLanguage { get; set; }
    public string? Timezone { get; set; }
}

public class UpdateSettingsRequest
{
    public string? Language { get; set; }
    public bool? VoiceEnabled { get; set; }
    public bool? WakeWordEnabled { get; set; }
    public bool? NotificationsEnabled { get; set; }
    public int? DefaultReminderMinutes { get; set; }
    public string? Timezone { get; set; }
}