using MyAssistant.Domain.Enums;

namespace MyAssistant.Application.Interfaces;

public interface ITimeZoneService
{
    TimeZoneInfo GetTimeZone(string? ianaId = null);
    DateTime NowInTimeZone(string? ianaId = null);
    DateTime ToUtc(DateTime localDateTime, string? ianaId = null);
    DateTime ToLocal(DateTime utcDateTime, string? ianaId = null);
}
