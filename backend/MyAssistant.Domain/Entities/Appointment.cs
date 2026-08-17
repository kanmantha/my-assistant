using MyAssistant.Domain.Common;
using MyAssistant.Domain.Enums;

namespace MyAssistant.Domain.Entities;

public class Appointment : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string? Location { get; set; }
    public List<string> Participants { get; set; } = new();
    public int ReminderMinutes { get; set; } = 15;
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
}
