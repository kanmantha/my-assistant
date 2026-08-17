namespace MyAssistant.Application.DTOs.Settings;

public class UserSettingsDto
{
    public string Language { get; set; } = "en";
    public bool AutoDetectLanguage { get; set; } = true;

    public string? AssistantVoice { get; set; }
    public double SpeechSpeed { get; set; } = 1.0;
    public double VoiceVolume { get; set; } = 1.0;
    public bool MuteAssistantVoice { get; set; }

    public bool WakeWordEnabled { get; set; }
    public string WakeWord { get; set; } = "Assistant";

    public bool NotificationsEnabled { get; set; } = true;
    public int DefaultReminderMinutes { get; set; } = 10;

    public string TimeZone { get; set; } = "Asia/Kolkata";
    public string Theme { get; set; } = "System";

    public bool ConfirmationMode { get; set; }
    public bool ReducedMotion { get; set; }
    public bool HighContrast { get; set; }
    public int FontScale { get; set; } = 100;
}

public class UpdateSettingsRequest
{
    public string Language { get; set; } = "en";
    public bool AutoDetectLanguage { get; set; } = true;

    public string? AssistantVoice { get; set; }
    public double SpeechSpeed { get; set; } = 1.0;
    public double VoiceVolume { get; set; } = 1.0;
    public bool MuteAssistantVoice { get; set; }

    public bool WakeWordEnabled { get; set; }
    public string WakeWord { get; set; } = "Assistant";

    public bool NotificationsEnabled { get; set; } = true;
    public int DefaultReminderMinutes { get; set; } = 10;

    public string TimeZone { get; set; } = "Asia/Kolkata";
    public string Theme { get; set; } = "System";

    public bool ConfirmationMode { get; set; }
    public bool ReducedMotion { get; set; }
    public bool HighContrast { get; set; }
    public int FontScale { get; set; } = 100;
}
