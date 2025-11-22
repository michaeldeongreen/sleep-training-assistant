# Post-provision script for Windows
Write-Host "Running post-provision script..." -ForegroundColor Green

# Get environment variables from azd
$RESOURCE_GROUP = azd env get-value AZURE_RESOURCE_GROUP
$STORAGE_ACCOUNT = azd env get-value AZURE_STORAGE_ACCOUNT_NAME
$CONTAINER_NAME = azd env get-value AZURE_STORAGE_CONTAINER_NAME
$AI_SERVICES_ENDPOINT = azd env get-value AZURE_AI_SERVICES_ENDPOINT
$PDF_FILE_URL = azd env get-value PDF_FILE_URL

Write-Host "Resource Group: $RESOURCE_GROUP"
Write-Host "Storage Account: $STORAGE_ACCOUNT"
Write-Host "Container Name: $CONTAINER_NAME"

# Check if PDF URL is provided
if ([string]::IsNullOrEmpty($PDF_FILE_URL)) {
    Write-Host "PDF_FILE_URL not set. Skipping PDF extraction." -ForegroundColor Yellow
    Write-Host "You can manually upload the extracted PDF text to the blob storage container later."
    exit 0
}

# Check if AI Services endpoint is configured
if ([string]::IsNullOrEmpty($AI_SERVICES_ENDPOINT)) {
    Write-Host "AZURE_AI_SERVICES_ENDPOINT not set. Skipping PDF extraction." -ForegroundColor Yellow
    Write-Host "Please configure AI Services endpoint and run extraction manually."
    exit 0
}

Write-Host ""
Write-Host "PDF extraction during deployment is optional." -ForegroundColor Cyan
Write-Host "The application will extract the PDF text on first run if not already present."
Write-Host ""
Write-Host "To manually extract and upload PDF text:" -ForegroundColor Cyan
Write-Host "1. Extract text using Azure AI Document Intelligence"
Write-Host "2. Upload to: https://$STORAGE_ACCOUNT.blob.core.windows.net/$CONTAINER_NAME/extracted-text.txt"
Write-Host ""
Write-Host "Post-provision completed successfully." -ForegroundColor Green
