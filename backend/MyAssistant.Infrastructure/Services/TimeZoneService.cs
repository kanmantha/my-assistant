using MyAssistant.Application.Interfaces;

namespace MyAssistant.Infrastructure.Services;

public class TimeZoneService : ITimeZoneService
{
    private static readonly Dictionary<string, TimeZoneInfo> Cache = new();

    public TimeZoneInfo GetTimeZone(string? ianaId = null)
    {
        var id = string.IsNullOrWhiteSpace(ianaId) ? "Asia/Kolkata" : ianaId;
        if (Cache.TryGetValue(id, out var cached)) return cached;

        TimeZoneInfo tz;
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var windowsId = IanaToWindows(id);
                tz = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
            }
            else
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById(id);
            }
        }
        catch (TimeZoneNotFoundException)
        {
            tz = TimeZoneInfo.Utc;
        }

        Cache[id] = tz;
        return tz;
    }

    public DateTime NowInTimeZone(string? ianaId = null)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GetTimeZone(ianaId));

    public DateTime ToUtc(DateTime localDateTime, string? ianaId = null)
    {
        if (localDateTime.Kind == DateTimeKind.Utc) return localDateTime;
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified), GetTimeZone(ianaId));
    }

    public DateTime ToLocal(DateTime utcDateTime, string? ianaId = null)
    {
        if (utcDateTime.Kind != DateTimeKind.Utc) utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, GetTimeZone(ianaId));
    }

    private static string IanaToWindows(string ianaId) => ianaId switch
    {
        "Asia/Kolkata" => "India Standard Time",
        "Asia/Kathmandu" => "Nepal Standard Time",
        "Asia/Dubai" => "Arabian Standard Time",
        "America/New_York" => "Eastern Standard Time",
        "America/Chicago" => "Central Standard Time",
        "America/Denver" => "Mountain Standard Time",
        "America/Los_Angeles" => "Pacific Standard Time",
        "Europe/London" => "GMT Standard Time",
        "Europe/Paris" => "W. Europe Standard Time",
        "Europe/Berlin" => "W. Europe Standard Time",
        "Asia/Singapore" => "Singapore Standard Time",
        "Asia/Tokyo" => "Tokyo Standard Time",
        "Australia/Sydney" => "AUS Eastern Standard Time",
        "UTC" => "UTC",
        _ => "India Standard Time"
    };
}
