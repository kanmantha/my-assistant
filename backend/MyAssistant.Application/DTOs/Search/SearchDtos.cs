namespace MyAssistant.Application.DTOs.Search;

public class SearchRequest
{
    public string Query { get; set; } = string.Empty;
    public string[]? Scopes { get; set; }
}

public class SearchResponse
{
    public List<SearchResultItem> Notes { get; set; } = new();
    public List<SearchResultItem> Tasks { get; set; } = new();
    public List<SearchResultItem> Appointments { get; set; } = new();
    public List<SearchResultItem> Reminders { get; set; } = new();
    public int TotalCount => Notes.Count + Tasks.Count + Appointments.Count + Reminders.Count;
}

public class SearchResultItem
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Snippet { get; set; }
    public DateTime? Date { get; set; }
    public Dictionary<string, object?>? Metadata { get; set; }
}
