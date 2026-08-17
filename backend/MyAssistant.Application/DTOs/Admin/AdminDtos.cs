namespace MyAssistant.Application.DTOs.Admin;

public class UserUsageDto
{
    public int AiCommands { get; set; }
    public int SpeechToText { get; set; }
    public int TextToSpeech { get; set; }
    public int Notes { get; set; }
    public int Tasks { get; set; }
    public int Reminders { get; set; }
    public int Appointments { get; set; }
    public int Searches { get; set; }
}

public class UserAdminDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string[] Roles { get; set; } = Array.Empty<string>();
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? OrganizationId { get; set; }
    public string Tier { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? RenewalAt { get; set; }
    public UserUsageDto UsageThisMonth { get; set; } = new();
}

public class AdminStatsDto
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int NewUsersThisMonth { get; set; }
    public int ActiveUsersThisMonth { get; set; }
    public int FreeUsers { get; set; }
    public int PremiumUsers { get; set; }
    public int TotalNotes { get; set; }
    public int TotalTasks { get; set; }
    public int TotalReminders { get; set; }
    public int TotalAppointments { get; set; }
    public int UsageThisMonth { get; set; }
}
