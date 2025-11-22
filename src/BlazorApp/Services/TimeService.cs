namespace BlazorApp.Services;

public class TimeService
{
    public string GetCurrentTimeCDT()
    {
        // CDT is UTC-5 (Central Daylight Time)
        // Note: This is a simplified version. In production, consider using TimeZoneInfo for proper DST handling
        var centralTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        var centralTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, centralTimeZone);
        
        return centralTime.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public DateTime GetCurrentDateTimeCDT()
    {
        var centralTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, centralTimeZone);
    }

    public DateTime GetCurrentCentralTime()
    {
        var centralTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, centralTimeZone);
    }
}
