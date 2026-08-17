namespace MyAssistant.Application.Interfaces;

public interface IDateTimeParser
{
    Task<ParsedDateTimeResult> ParseAsync(string text, string language, string timeZone, CancellationToken cancellationToken = default);
}

public class ParsedDateTimeResult
{
    public DateTime? DateTime { get; set; }
    public int? DurationMinutes { get; set; }
    public bool HasDate { get; set; }
    public bool HasTime { get; set; }
    public string? RawExpression { get; set; }
}
