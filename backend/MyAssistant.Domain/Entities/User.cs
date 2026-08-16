namespace MyAssistant.Domain.Entities;

public enum UserRole
{
    User = 0,
    Admin = 1
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.User;
    public bool IsActive { get; set; } = true;
    public bool IsSuspended { get; set; }
    public string PreferredLanguage { get; set; } = "en-IN";
    public string Timezone { get; set; } = "Asia/Kolkata";
    public Guid? OrganizationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Organization? Organization { get; set; }
    public UserSettings? Settings { get; set; }
    public Subscription? Subscription { get; set; }
    public ICollection<Device>? Devices { get; set; }
    public ICollection<RefreshToken>? RefreshTokens { get; set; }
}

public class UserSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Language { get; set; } = "en-IN";
    public bool VoiceEnabled { get; set; } = true;
    public bool WakeWordEnabled { get; set; } = false;
    public bool NotificationsEnabled { get; set; } = true;
    public int DefaultReminderMinutes { get; set; } = 15;
    public string Timezone { get; set; } = "Asia/Kolkata";

    public User? User { get; set; }
}

public class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string DeviceToken { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty; // android | ios
    public string DeviceName { get; set; } = string.Empty;
    public string? FcmToken { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenAt { get; set; }

    public User? User { get; set; }
}

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRevoked { get; set; }
    public string? ReplacedByToken { get; set; }

    public User? User { get; set; }
}
