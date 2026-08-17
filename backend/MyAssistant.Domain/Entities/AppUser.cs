using Microsoft.AspNetCore.Identity;

namespace MyAssistant.Domain.Entities;

public class AppUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public UserSettings? Settings { get; set; }
    public ICollection<Note> Notes { get; set; } = new List<Note>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public ICollection<Reminder> Reminders { get; set; } = new List<Reminder>();
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<ConversationHistory> Conversations { get; set; } = new List<ConversationHistory>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public string DisplayName => $"{FirstName} {LastName}".Trim();
}
