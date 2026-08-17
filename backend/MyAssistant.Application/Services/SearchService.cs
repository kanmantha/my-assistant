using MyAssistant.Application.DTOs.Search;
using MyAssistant.Application.Interfaces;

namespace MyAssistant.Application.Services;

public class SearchService : ISearchService
{
    private readonly INoteRepository _notes;
    private readonly ITaskRepository _tasks;
    private readonly IAppointmentRepository _appointments;
    private readonly IReminderRepository _reminders;

    public SearchService(INoteRepository notes, ITaskRepository tasks, IAppointmentRepository appointments, IReminderRepository reminders)
    {
        _notes = notes;
        _tasks = tasks;
        _appointments = appointments;
        _reminders = reminders;
    }

    public async Task<SearchResponse> SearchAsync(Guid userId, SearchRequest request, CancellationToken cancellationToken = default)
    {
        var query = request.Query?.Trim() ?? string.Empty;
        var response = new SearchResponse();

        if (query.Length == 0)
        {
            return response;
        }

        if (IsScope(request, "notes"))
        {
            var notes = await _notes.SearchForUserAsync(userId, query, cancellationToken);
            response.Notes.AddRange(notes.Select(n => new SearchResultItem
            {
                Id = n.Id,
                Type = "note",
                Title = n.Title,
                Snippet = Truncate(n.Content, 120),
                Date = n.CreatedAt,
                Metadata = new Dictionary<string, object?> { ["pinned"] = n.IsPinned }
            }));
        }

        if (IsScope(request, "tasks"))
        {
            var tasks = await _tasks.SearchForUserAsync(userId, query, cancellationToken);
            response.Tasks.AddRange(tasks.Select(t => new SearchResultItem
            {
                Id = t.Id,
                Type = "task",
                Title = t.Title,
                Snippet = t.Description,
                Date = t.DueDate.HasValue ? t.DueDate.Value.ToDateTime(TimeOnly.MinValue) : t.CreatedAt,
                Metadata = new Dictionary<string, object?> { ["status"] = t.Status.ToString(), ["priority"] = t.Priority.ToString() }
            }));
        }

        if (IsScope(request, "appointments"))
        {
            var appointments = await _appointments.SearchForUserAsync(userId, query, cancellationToken);
            response.Appointments.AddRange(appointments.Select(a => new SearchResultItem
            {
                Id = a.Id,
                Type = "appointment",
                Title = a.Title,
                Snippet = a.Description,
                Date = a.StartDateTime,
                Metadata = new Dictionary<string, object?> { ["location"] = a.Location }
            }));
        }

        if (IsScope(request, "reminders"))
        {
            var reminders = await _reminders.SearchForUserAsync(userId, query, cancellationToken);
            response.Reminders.AddRange(reminders.Select(r => new SearchResultItem
            {
                Id = r.Id,
                Type = "reminder",
                Title = r.Title,
                Snippet = r.Message,
                Date = r.ReminderAt,
                Metadata = new Dictionary<string, object?> { ["recurrence"] = r.Recurrence.ToString() }
            }));
        }

        return response;
    }

    private static bool IsScope(SearchRequest request, string name)
    {
        return request.Scopes is null || request.Scopes.Length == 0 || request.Scopes.Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    private static string? Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }
}
