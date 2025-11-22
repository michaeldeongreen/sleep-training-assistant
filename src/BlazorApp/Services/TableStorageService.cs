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
}
