using MyAssistant.Application.DTOs.Settings;
using MyAssistant.Application.Interfaces;
using MyAssistant.Domain.Enums;

namespace MyAssistant.Application.Services;

public class SettingsService : ISettingsService
{
    private readonly IUserSettingsRepository _settings;

    public SettingsService(IUserSettingsRepository settings)
    {
        _settings = settings;
    }

    public async Task<UserSettingsDto> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var settings = await _settings.GetOrCreateAsync(userId, cancellationToken);
        return ToDto(settings);
    }

    public async Task<UserSettingsDto> UpdateAsync(Guid userId, UpdateSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await _settings.GetOrCreateAsync(userId, cancellationToken);

        settings.Language = ParseLanguage(request.Language);
        settings.AutoDetectLanguage = request.AutoDetectLanguage;
        settings.AssistantVoice = request.AssistantVoice;
        settings.SpeechSpeed = request.SpeechSpeed is >= 0.5 and <= 2.0 ? request.SpeechSpeed : 1.0;
        settings.VoiceVolume = request.VoiceVolume is >= 0.0 and <= 1.0 ? request.VoiceVolume : 1.0;
        settings.MuteAssistantVoice = request.MuteAssistantVoice;
        settings.WakeWordEnabled = request.WakeWordEnabled;
        settings.WakeWord = string.IsNullOrWhiteSpace(request.WakeWord) ? "Assistant" : request.WakeWord;
        settings.NotificationsEnabled = request.NotificationsEnabled;
        settings.DefaultReminderMinutes = request.DefaultReminderMinutes;
        settings.TimeZone = string.IsNullOrWhiteSpace(request.TimeZone) ? "Asia/Kolkata" : request.TimeZone;
        settings.Theme = ParseTheme(request.Theme);
        settings.ConfirmationMode = request.ConfirmationMode;
        settings.ReducedMotion = request.ReducedMotion;
        settings.HighContrast = request.HighContrast;
        settings.FontScale = request.FontScale is >= 80 and <= 160 ? request.FontScale : 100;
        settings.UpdatedAt = DateTime.UtcNow;

        await _settings.UpdateAsync(settings, cancellationToken);
        return ToDto(settings);
    }

    private static AppLanguage ParseLanguage(string? lang) => lang?.ToLowerInvariant() switch
    {
        "en" or "en-in" => AppLanguage.en,
        "hi" or "hi-in" => AppLanguage.hi,
        "te" or "te-in" => AppLanguage.te,
        "auto" => AppLanguage.Auto,
        _ => AppLanguage.en
    };

    private static ThemePreference ParseTheme(string? theme) => theme?.ToLowerInvariant() switch
    {
        "light" => ThemePreference.Light,
        "dark" => ThemePreference.Dark,
        _ => ThemePreference.System
    };

    internal static UserSettingsDto ToDto(Domain.Entities.UserSettings s) => new()
    {
        Language = s.Language.ToString(),
        AutoDetectLanguage = s.AutoDetectLanguage,
        AssistantVoice = s.AssistantVoice,
        SpeechSpeed = s.SpeechSpeed,
        VoiceVolume = s.VoiceVolume,
        MuteAssistantVoice = s.MuteAssistantVoice,
        WakeWordEnabled = s.WakeWordEnabled,
        WakeWord = s.WakeWord,
        NotificationsEnabled = s.NotificationsEnabled,
        DefaultReminderMinutes = s.DefaultReminderMinutes,
        TimeZone = s.TimeZone,
        Theme = s.Theme.ToString(),
        ConfirmationMode = s.ConfirmationMode,
        ReducedMotion = s.ReducedMotion,
        HighContrast = s.HighContrast,
        FontScale = s.FontScale
    };
}
