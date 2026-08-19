using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyAssistant.Application.Common;
using MyAssistant.Application.Interfaces;
using MyAssistant.API.Services;

namespace MyAssistant.API.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class ProductivityController : ControllerBase
{
    private readonly IProductivityService _svc;
    private readonly ISubscriptionService _subscriptions;

    public ProductivityController(IProductivityService svc, ISubscriptionService subscriptions)
    {
        _svc = svc;
        _subscriptions = subscriptions;
    }

    private Guid UserId => Guid.Parse(User.Claims.First(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);

    private static string ComputeGreeting()
    {
        var hour = (DateTime.UtcNow + TimeSpan.FromHours(5) + TimeSpan.FromMinutes(30)).Hour;
        if (hour < 12) return "Good Morning";
        if (hour < 17) return "Good Afternoon";
        return "Good Evening";
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var userId = (Guid)CurrentUserServiceExtensions.GetUserId(this);
        var today = DateTime.Today;
        var appts = await _svc.GetAppointmentsAsync(userId, today, today.AddDays(1));
        var tasks = await _svc.GetTasksAsync(userId);
        var reminders = await _svc.GetRemindersAsync(userId, onlyPending: true);
        var usage = await _subscriptions.GetUsageAsync(userId);
        var subscription = await _subscriptions.GetSubscriptionAsync(userId);

        return Ok(ApiResponse<object>.Ok(new
        {
            greeting = ComputeGreeting(),
            todayAppointments = appts.Where(a => a.StartDateTime.Date == today).OrderBy(a => a.StartDateTime),
            todayTasks = tasks.Where(t => t.Status == "Pending").Take(5),
            todayReminders = reminders.Where(r => r.ReminderDateTime.Date <= today.AddDays(1)).OrderBy(r => r.ReminderDateTime).Take(5),
            usage,
            subscription
        }));
    }

    [HttpGet("notes")]
    public async Task<IActionResult> GetNotes(int page = 1, int pageSize = 50)
    {
        var notes = await _svc.GetNotesAsync(UserId, page, pageSize);
        return Ok(ApiResponse<object>.Ok(notes));
    }

    [HttpPost("notes")]
    public async Task<IActionResult> CreateNote(CreateNoteRequest request)
    {
        var note = await _svc.CreateNoteAsync(UserId, request, "Asia/Kolkata");
        return Ok(ApiResponse<object>.Ok(note, "Note created"));
    }

    [HttpPut("notes/{id}")]
    public async Task<IActionResult> UpdateNote(Guid id, UpdateNoteRequest request)
        => Ok(ApiResponse<object>.Ok(await _svc.UpdateNoteAsync(UserId, id, request), "Note updated"));

    [HttpDelete("notes/{id}")]
    public async Task<IActionResult> DeleteNote(Guid id)
    {
        await _svc.DeleteNoteAsync(UserId, id);
        return Ok(ApiResponse<object>.Ok(new { }, "Note deleted"));
    }

    [HttpGet("tasks")]
    public async Task<IActionResult> GetTasks(string? status = null)
        => Ok(ApiResponse<object>.Ok(await _svc.GetTasksAsync(UserId, status)));

    [HttpPost("tasks")]
    public async Task<IActionResult> CreateTask(CreateTaskRequest request)
        => Ok(ApiResponse<object>.Ok(await _svc.CreateTaskAsync(UserId, request, "Asia/Kolkata"), "Task created"));

    [HttpPut("tasks/{id}")]
    public async Task<IActionResult> UpdateTask(Guid id, UpdateTaskRequest request)
        => Ok(ApiResponse<object>.Ok(await _svc.UpdateTaskAsync(UserId, id, request), "Task updated"));

    [HttpDelete("tasks/{id}")]
    public async Task<IActionResult> DeleteTask(Guid id)
    {
        await _svc.DeleteTaskAsync(UserId, id);
        return Ok(ApiResponse<object>.Ok(new { }, "Task deleted"));
    }

    [HttpPost("tasks/{id}/complete")]
    public async Task<IActionResult> CompleteTask(Guid id)
        => Ok(ApiResponse<object>.Ok(await _svc.UpdateTaskAsync(UserId, id, new UpdateTaskRequest(Status: "Completed")), "Task completed"));

    [HttpGet("reminders")]
    public async Task<IActionResult> GetReminders(bool? onlyPending = null)
        => Ok(ApiResponse<object>.Ok(await _svc.GetRemindersAsync(UserId, onlyPending)));

    [HttpPost("reminders")]
    public async Task<IActionResult> CreateReminder(CreateReminderRequest request)
        => Ok(ApiResponse<object>.Ok(await _svc.CreateReminderAsync(UserId, request, "Asia/Kolkata"), "Reminder created"));

    [HttpPut("reminders/{id}")]
    public async Task<IActionResult> UpdateReminder(Guid id, UpdateReminderRequest request)
        => Ok(ApiResponse<object>.Ok(await _svc.UpdateReminderAsync(UserId, id, request), "Reminder updated"));

    [HttpDelete("reminders/{id}")]
    public async Task<IActionResult> DeleteReminder(Guid id)
    {
        await _svc.DeleteReminderAsync(UserId, id);
        return Ok(ApiResponse<object>.Ok(new { }, "Reminder deleted"));
    }

    [HttpGet("appointments")]
    public async Task<IActionResult> GetAppointments(DateTime? start = null, DateTime? end = null)
        => Ok(ApiResponse<object>.Ok(await _svc.GetAppointmentsAsync(UserId, start, end)));

    [HttpPost("appointments")]
    public async Task<IActionResult> CreateAppointment(CreateAppointmentRequest request)
        => Ok(ApiResponse<object>.Ok(await _svc.CreateAppointmentAsync(UserId, request, "Asia/Kolkata"), "Appointment created"));

    [HttpPut("appointments/{id}")]
    public async Task<IActionResult> UpdateAppointment(Guid id, UpdateAppointmentRequest request)
        => Ok(ApiResponse<object>.Ok(await _svc.UpdateAppointmentAsync(UserId, id, request), "Appointment updated"));

    [HttpDelete("appointments/{id}")]
    public async Task<IActionResult> DeleteAppointment(Guid id)
    {
        await _svc.DeleteAppointmentAsync(UserId, id);
        return Ok(ApiResponse<object>.Ok(new { }, "Appointment deleted"));
    }

    [HttpGet("search")]
    public async Task<IActionResult> GlobalSearch(string q)
    {
        var results = new Dictionary<string, object>();

        var notes = await _svc.GetNotesAsync(UserId);
        results["notes"] = q is null ? notes.Take(5) : notes.Where(n => (n.Title + " " + n.Content).ToLowerInvariant().Contains(q.ToLowerInvariant())).Take(5);

        var tasks = await _svc.GetTasksAsync(UserId);
        results["tasks"] = q is null ? tasks.Take(5) : tasks.Where(t => (t.Title + " " + t.Description).ToLowerInvariant().Contains(q.ToLowerInvariant())).Take(5);

        var reminders = await _svc.GetRemindersAsync(UserId);
        results["reminders"] = q is null ? reminders.Take(5) : reminders.Where(r => (r.Title + " " + r.Description).ToLowerInvariant().Contains(q.ToLowerInvariant())).Take(5);

        var appts = await _svc.GetAppointmentsAsync(UserId);
        results["appointments"] = q is null ? appts.Take(5) : appts.Where(a => (a.Title + " " + a.Description).ToLowerInvariant().Contains(q.ToLowerInvariant())).Take(5);

        return Ok(ApiResponse<object>.Ok(results));
    }
}