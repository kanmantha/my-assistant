using MyAssistant.Domain.Enums;

namespace MyAssistant.Application.DTOs.Appointments;

public class AppointmentDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string? Location { get; set; }
    public List<string> Participants { get; set; } = new();
    public int ReminderMinutes { get; set; }
    public AppointmentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateAppointmentRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public string? Location { get; set; }
    public List<string> Participants { get; set; } = new();
    public int ReminderMinutes { get; set; } = 15;
}

public class UpdateAppointmentRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public string? Location { get; set; }
    public List<string> Participants { get; set; } = new();
    public int ReminderMinutes { get; set; } = 15;
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
}

public class RescheduleAppointmentRequest
{
    public DateTime StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
}
