using MyAssistant.Domain.Common;
using MyAssistant.Domain.Enums;

namespace MyAssistant.Domain.Entities;

public class UserSettings : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }

    public AppLanguage Language { get; set; } = AppLanguage.en;
    public bool AutoDetectLanguage { get; set; } = true;

    public string? AssistantVoice { get; set; }
    public double SpeechSpeed { get; set; } = 1.0;
    public double VoiceVolume { get; set; } = 1.0;
    public bool MuteAssistantVoice { get; set; }

    public bool WakeWordEnabled { get; set; } = true;
    public string WakeWord { get; set; } = "Assistant";

    public bool NotificationsEnabled { get; set; } = true;
    public int DefaultReminderMinutes { get; set; } = 10;

    public string TimeZone { get; set; } = "Asia/Kolkata";

    public ThemePreference Theme { get; set; } = ThemePreference.System;

    public bool ConfirmationMode { get; set; }
    public bool ReducedMotion { get; set; }
    public bool HighContrast { get; set; }
    public int FontScale { get; set; } = 100;
}
