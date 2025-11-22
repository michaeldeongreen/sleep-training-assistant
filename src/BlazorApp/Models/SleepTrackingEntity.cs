using Azure;
using Azure.Data.Tables;

namespace BlazorApp.Models;

public class SleepTrackingEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "Savannah";
    public string RowKey { get; set; } = string.Empty; // yyyyMMdd format
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    // Sleep tracking properties
    public string? WakeUp { get; set; }
    
    // Nap 1
    public string? Nap1TimePutInCrib { get; set; }
    public string? Nap1SleepStart { get; set; }
    public string? Nap1Finish { get; set; }
    
    // Nap 2
    public string? Nap2TimePutInCrib { get; set; }
    public string? Nap2SleepStart { get; set; }
    public string? Nap2Finish { get; set; }
    
    // Nap 3
    public string? Nap3TimePutInCrib { get; set; }
    public string? Nap3SleepStart { get; set; }
    public string? Nap3Finish { get; set; }
    
    // Bedtime
    public string? BedtimeTimePutInCrib { get; set; }
    public string? BedtimeTimeSleepStart { get; set; }
    
    public string? FeedTime { get; set; }
    public string? Notes { get; set; }
}
