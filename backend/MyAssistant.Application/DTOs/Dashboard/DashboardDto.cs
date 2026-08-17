using MyAssistant.Application.DTOs.Appointments;
using MyAssistant.Application.DTOs.Notes;
using MyAssistant.Application.DTOs.Reminders;
using MyAssistant.Application.DTOs.Tasks;

namespace MyAssistant.Application.DTOs.Dashboard;

public class DashboardDto
{
    public string Greeting { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public int TasksToday { get; set; }
    public int TasksCompletedToday { get; set; }
    public List<TaskDto> TodayTasks { get; set; } = new();
    public List<AppointmentDto> TodayAppointments { get; set; } = new();
    public List<ReminderDto> UpcomingReminders { get; set; } = new();
    public List<NoteDto> RecentNotes { get; set; } = new();
    public int PendingTasks { get; set; }
    public int UpcomingAppointments { get; set; }
}
