namespace MyAssistant.Domain.Entities;

public enum NoteTag
{
    General = 0,
    Work = 1,
    Personal = 2,
    Ideas = 3,
    Important = 4,
    Shopping = 5
}

public class Note
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Language { get; set; } = "en-IN";
    public List<string> Tags { get; set; } = new();
    public Guid? OrganizationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}

public enum TaskPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Urgent = 3
}

public enum TaskStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3
}

public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    public DateTime? DueDate { get; set; }
    public string? DueTime { get; set; }
    public string? DueDateTime { get; set; }
    public Guid? OrganizationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public User? User { get; set; }
}

public enum ReminderRecurrence
{
    Once = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Yearly = 4,
    Custom = 5
}

public class Reminder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime ReminderDateTime { get; set; }
    public string Timezone { get; set; } = "Asia/Kolkata";
    public ReminderRecurrence Recurrence { get; set; } = ReminderRecurrence.Once;
    public string? RecurrenceRule { get; set; } // iCal RRULE for custom
    public bool IsCompleted { get; set; }
    public Guid? OrganizationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}

public enum AppointmentStatus
{
    Scheduled = 0,
    Cancelled = 1,
    Completed = 2
}

public class Appointment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string Location { get; set; } = string.Empty;
    public List<string> Participants { get; set; } = new();
    public string Timezone { get; set; } = "Asia/Kolkata";
    public int ReminderMinutes { get; set; } = 15;
    public DateTime? ReminderAt { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
    public Guid? OrganizationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}