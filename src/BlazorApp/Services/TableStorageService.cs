using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using BlazorApp.Models;

namespace BlazorApp.Services;

public class TableStorageService
{
    private readonly TableClient _tableClient;
    private readonly ILogger<TableStorageService> _logger;
    private readonly TimeService _timeService;

    public TableStorageService(
        IConfiguration configuration,
        ILogger<TableStorageService> logger,
        TimeService timeService)
    {
        _logger = logger;
        _timeService = timeService;

        var storageAccountName = configuration["Storage:AccountName"] ?? configuration["STORAGE_ACCOUNT_NAME"];
        var tableName = configuration["Storage:TableName"] ?? configuration["STORAGE_TABLE_NAME"] ?? "SleepTracking";

        if (string.IsNullOrEmpty(storageAccountName))
        {
            _logger.LogWarning("Storage account name not configured");
            _tableClient = null!;
            return;
        }

        var tableServiceClient = new TableServiceClient(
            new Uri($"https://{storageAccountName}.table.core.windows.net"),
            new DefaultAzureCredential());

        _tableClient = tableServiceClient.GetTableClient(tableName);
    }

    public async Task<SleepTrackingEntity> GetOrCreateTodayAsync()
    {
        try
        {
            await _tableClient.CreateIfNotExistsAsync();

            var currentDate = _timeService.GetCurrentCentralTime();
            var rowKey = currentDate.ToString("yyyyMMdd");

            try
            {
                var response = await _tableClient.GetEntityAsync<SleepTrackingEntity>("Savannah", rowKey);
                _logger.LogInformation("Retrieved existing sleep tracking for {Date}", rowKey);
                return response.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Entity doesn't exist, create it
                var newEntity = new SleepTrackingEntity
                {
                    PartitionKey = "Savannah",
                    RowKey = rowKey
                };

                await _tableClient.AddEntityAsync(newEntity);
                _logger.LogInformation("Created new sleep tracking for {Date}", rowKey);
                return newEntity;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accessing table storage");
            throw;
        }
    }

    public async Task<SleepTrackingEntity> UpdateAsync(SleepTrackingEntity entity)
    {
        try
        {
            await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);
            _logger.LogInformation("Updated sleep tracking for {Date}", entity.RowKey);
            return entity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating table storage");
            throw;
        }
    }

    public async Task<string> GetTodayDataAsTextAsync()
    {
        try
        {
            var entity = await GetOrCreateTodayAsync();
            var currentDate = _timeService.GetCurrentCentralTime();

            var result = new System.Text.StringBuilder();
            result.AppendLine($"Sleep Tracking for {currentDate:dddd, MMMM d, yyyy}:");
            result.AppendLine();
            result.AppendLine($"Wake Up: {entity.WakeUp ?? "Not recorded"}");
            result.AppendLine();
            result.AppendLine("Nap 1:");
            result.AppendLine($"  Time Put in Crib: {entity.Nap1TimePutInCrib ?? "Not recorded"}");
            result.AppendLine($"  Sleep Start: {entity.Nap1SleepStart ?? "Not recorded"}");
            result.AppendLine($"  Finish: {entity.Nap1Finish ?? "Not recorded"}");
            result.AppendLine();
            result.AppendLine("Nap 2:");
            result.AppendLine($"  Time Put in Crib: {entity.Nap2TimePutInCrib ?? "Not recorded"}");
            result.AppendLine($"  Sleep Start: {entity.Nap2SleepStart ?? "Not recorded"}");
            result.AppendLine($"  Finish: {entity.Nap2Finish ?? "Not recorded"}");
            result.AppendLine();
            result.AppendLine("Nap 3:");
            result.AppendLine($"  Time Put in Crib: {entity.Nap3TimePutInCrib ?? "Not recorded"}");
            result.AppendLine($"  Sleep Start: {entity.Nap3SleepStart ?? "Not recorded"}");
            result.AppendLine($"  Finish: {entity.Nap3Finish ?? "Not recorded"}");
            result.AppendLine();
            result.AppendLine("Bedtime:");
            result.AppendLine($"  Time Put in Crib: {entity.BedtimeTimePutInCrib ?? "Not recorded"}");
            result.AppendLine($"  Sleep Start: {entity.BedtimeTimeSleepStart ?? "Not recorded"}");
            result.AppendLine();
            result.AppendLine("Bedtime Wake Ups:");
            result.AppendLine($"  Wake 1: {entity.BedtimeWake1StartFinish ?? "Not recorded"}");
            result.AppendLine($"  Wake 2: {entity.BedtimeWake2StartFinish ?? "Not recorded"}");
            result.AppendLine($"  Wake 3: {entity.BedtimeWake3StartFinish ?? "Not recorded"}");
            result.AppendLine($"  Wake 4: {entity.BedtimeWake4StartFinish ?? "Not recorded"}");
            result.AppendLine($"  Wake 5: {entity.BedtimeWake5StartFinish ?? "Not recorded"}");
            result.AppendLine($"  Wake 6: {entity.BedtimeWake6StartFinish ?? "Not recorded"}");
            result.AppendLine();
            result.AppendLine($"Feed Time: {entity.FeedTime ?? "Not recorded"}");
            result.AppendLine($"Notes: {entity.Notes ?? "None"}");

            return result.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting today's data as text");
            return "Error retrieving sleep tracking data.";
        }
    }

    public async Task<List<SleepTrackingEntity>> GetDateRangeAsync(string startDate, string? endDate = null)
    {
        try
        {
            await _tableClient.CreateIfNotExistsAsync();

            // Build the filter query
            var filter = $"PartitionKey eq 'Savannah' and RowKey ge '{startDate}'";
            if (!string.IsNullOrEmpty(endDate))
            {
                filter += $" and RowKey le '{endDate}'";
            }

            _logger.LogInformation("Querying date range: {StartDate} to {EndDate}", startDate, endDate ?? "present");

            var results = new List<SleepTrackingEntity>();
            await foreach (var entity in _tableClient.QueryAsync<SleepTrackingEntity>(filter))
            {
                results.Add(entity);
            }

            _logger.LogInformation("Retrieved {Count} historical records", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying date range");
            return new List<SleepTrackingEntity>();
        }
    }

    public string FormatHistoricalDataAsText(List<SleepTrackingEntity> entities)
    {
        if (entities.Count == 0)
        {
            return "No sleep tracking data found for the requested date range.";
        }

        var result = new System.Text.StringBuilder();
        result.AppendLine($"Historical Sleep Tracking Data ({entities.Count} day(s)):");
        result.AppendLine();

        foreach (var entity in entities.OrderBy(e => e.RowKey))
        {
            // Parse the date from RowKey (yyyyMMdd format)
            if (DateTime.TryParseExact(entity.RowKey, "yyyyMMdd", null, 
                System.Globalization.DateTimeStyles.None, out var date))
            {
                result.AppendLine($"### {date:dddd, MMMM d, yyyy}");
            }
            else
            {
                result.AppendLine($"### {entity.RowKey}");
            }

            result.AppendLine($"- **Wake Up**: {entity.WakeUp ?? "Not recorded"}");
            
            if (!string.IsNullOrEmpty(entity.Nap1SleepStart) || !string.IsNullOrEmpty(entity.Nap1Finish))
            {
                result.AppendLine($"- **Nap 1**: Put in crib: {entity.Nap1TimePutInCrib ?? "N/A"}, Sleep start: {entity.Nap1SleepStart ?? "N/A"}, Finish: {entity.Nap1Finish ?? "N/A"}");
            }
            
            if (!string.IsNullOrEmpty(entity.Nap2SleepStart) || !string.IsNullOrEmpty(entity.Nap2Finish))
            {
                result.AppendLine($"- **Nap 2**: Put in crib: {entity.Nap2TimePutInCrib ?? "N/A"}, Sleep start: {entity.Nap2SleepStart ?? "N/A"}, Finish: {entity.Nap2Finish ?? "N/A"}");
            }
            
            if (!string.IsNullOrEmpty(entity.Nap3SleepStart) || !string.IsNullOrEmpty(entity.Nap3Finish))
            {
                result.AppendLine($"- **Nap 3**: Put in crib: {entity.Nap3TimePutInCrib ?? "N/A"}, Sleep start: {entity.Nap3SleepStart ?? "N/A"}, Finish: {entity.Nap3Finish ?? "N/A"}");
            }
            
            if (!string.IsNullOrEmpty(entity.BedtimeTimePutInCrib) || !string.IsNullOrEmpty(entity.BedtimeTimeSleepStart))
            {
                result.AppendLine($"- **Bedtime**: Put in crib: {entity.BedtimeTimePutInCrib ?? "N/A"}, Sleep start: {entity.BedtimeTimeSleepStart ?? "N/A"}");
            }

            var wakeUps = new[] { entity.BedtimeWake1StartFinish, entity.BedtimeWake2StartFinish, 
                                  entity.BedtimeWake3StartFinish, entity.BedtimeWake4StartFinish,
                                  entity.BedtimeWake5StartFinish, entity.BedtimeWake6StartFinish }
                .Where(w => !string.IsNullOrEmpty(w))
                .ToList();
            
            if (wakeUps.Any())
            {
                result.AppendLine($"- **Bedtime Wake Ups**: {string.Join(", ", wakeUps)}");
            }

            if (!string.IsNullOrEmpty(entity.FeedTime))
            {
                result.AppendLine($"- **Feed Time**: {entity.FeedTime}");
            }

            if (!string.IsNullOrEmpty(entity.Notes) && entity.Notes != "None")
            {
                result.AppendLine($"- **Notes**: {entity.Notes}");
            }

            result.AppendLine();
        }

        return result.ToString();
    }
}
