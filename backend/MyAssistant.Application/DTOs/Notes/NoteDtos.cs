namespace MyAssistant.Application.DTOs.Notes;

public class NoteDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string OriginalLanguage { get; set; } = "en-IN";
    public List<string> Tags { get; set; } = new();
    public bool IsPinned { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateNoteRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string OriginalLanguage { get; set; } = "en-IN";
    public List<string> Tags { get; set; } = new();
}

public class UpdateNoteRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public bool IsPinned { get; set; }
}
