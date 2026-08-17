using MyAssistant.Domain.Common;

namespace MyAssistant.Domain.Entities;

public class Note : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string OriginalLanguage { get; set; } = "en-IN";
    public List<string> Tags { get; set; } = new();
    public bool IsPinned { get; set; }
}
