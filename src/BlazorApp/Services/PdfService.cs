using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace BlazorApp.Services;

public class PdfService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PdfService> _logger;
    private string? _cachedPdfContent;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public PdfService(
        IConfiguration configuration,
        ILogger<PdfService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GetPdfContentAsync()
    {
        if (_cachedPdfContent != null)
        {
            return _cachedPdfContent;
        }

        await _semaphore.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_cachedPdfContent != null)
            {
                return _cachedPdfContent;
            }

            // Try to load from blob storage first
            var blobContent = await LoadFromBlobStorageAsync();
            if (!string.IsNullOrEmpty(blobContent))
            {
                _cachedPdfContent = blobContent;
                _logger.LogInformation("Loaded PDF content from blob storage");
                return _cachedPdfContent;
            }

            // If not in blob storage, extract from PDF
            _cachedPdfContent = await ExtractPdfTextAsync();
            
            if (string.IsNullOrEmpty(_cachedPdfContent))
            {
                _logger.LogWarning("Failed to extract PDF content, using placeholder");
                _cachedPdfContent = "No PDF content available. Please configure PDF_FILE_URL and AZURE_AI_SERVICES_ENDPOINT.";
            }
            else
            {
                // Save to blob storage for future use
                await SaveToBlobStorageAsync(_cachedPdfContent);
            }

            return _cachedPdfContent;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<string?> LoadFromBlobStorageAsync()
    {
        try
        {
            var storageAccountName = _configuration["Storage:AccountName"] 
                ?? _configuration["STORAGE_ACCOUNT_NAME"];
            var containerName = _configuration["Storage:ContainerName"] 
                ?? _configuration["STORAGE_CONTAINER_NAME"] 
                ?? "pdf-content";
            var blobName = _configuration["Storage:PdfTextBlobName"] ?? "extracted-text.txt";

            if (string.IsNullOrEmpty(storageAccountName))
            {
                _logger.LogWarning("Storage account name not configured");
                return null;
            }

            var blobServiceClient = new BlobServiceClient(
                new Uri($"https://{storageAccountName}.blob.core.windows.net"),
                new DefaultAzureCredential());

            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            if (await blobClient.ExistsAsync())
            {
                _logger.LogInformation("Loading PDF content from blob storage");
                var response = await blobClient.DownloadContentAsync();
                return response.Value.Content.ToString();
            }

            _logger.LogInformation("PDF content blob does not exist yet");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading PDF content from blob storage");
            return null;
        }
    }

    private async Task SaveToBlobStorageAsync(string content)
    {
        try
        {
            var storageAccountName = _configuration["Storage:AccountName"] 
                ?? _configuration["STORAGE_ACCOUNT_NAME"];
            var containerName = _configuration["Storage:ContainerName"] 
                ?? _configuration["STORAGE_CONTAINER_NAME"] 
                ?? "pdf-content";
            var blobName = _configuration["Storage:PdfTextBlobName"] ?? "extracted-text.txt";

            if (string.IsNullOrEmpty(storageAccountName))
            {
                _logger.LogWarning("Storage account name not configured, cannot save PDF content");
                return;
            }

            var blobServiceClient = new BlobServiceClient(
                new Uri($"https://{storageAccountName}.blob.core.windows.net"),
                new DefaultAzureCredential());

            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();

            var blobClient = containerClient.GetBlobClient(blobName);
            await blobClient.UploadAsync(BinaryData.FromString(content), overwrite: true);

            _logger.LogInformation("Saved PDF content to blob storage");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving PDF content to blob storage");
        }
    }

    private async Task<string> ExtractPdfTextAsync()
    {
        try
        {
            var pdfUrl = _configuration["PDF:FileUrl"] 
                ?? _configuration["PDF_FILE_URL"];

            if (string.IsNullOrEmpty(pdfUrl))
            {
                _logger.LogError("PDF file URL not configured");
                return string.Empty;
            }

            _logger.LogInformation($"Downloading PDF from: {pdfUrl}");

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            
            var pdfBytes = await httpClient.GetByteArrayAsync(pdfUrl);
            _logger.LogInformation($"Downloaded PDF, size: {pdfBytes.Length} bytes");

            // Extract text using PdfPig
            _logger.LogInformation("Extracting text from PDF using PdfPig");

            using var pdfStream = new MemoryStream(pdfBytes);
            using var document = PdfDocument.Open(pdfStream);
            
            var extractedText = new System.Text.StringBuilder();
            
            foreach (var page in document.GetPages())
            {
                extractedText.AppendLine(page.Text);
                extractedText.AppendLine(); // Add spacing between pages
            }

            var result = extractedText.ToString();
            _logger.LogInformation("Successfully extracted {CharCount} characters from PDF ({PageCount} pages)", 
                result.Length, document.NumberOfPages);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting PDF text");
            return string.Empty;
        }
    }
}
