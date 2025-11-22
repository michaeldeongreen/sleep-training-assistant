#!/bin/bash
set -e

echo "Running post-provision script..."

# Get environment variables from azd
RESOURCE_GROUP=$(azd env get-value AZURE_RESOURCE_GROUP)
STORAGE_ACCOUNT=$(azd env get-value AZURE_STORAGE_ACCOUNT_NAME)
CONTAINER_NAME=$(azd env get-value AZURE_STORAGE_CONTAINER_NAME)
AI_SERVICES_ENDPOINT=$(azd env get-value AZURE_AI_SERVICES_ENDPOINT)
PDF_FILE_URL=$(azd env get-value PDF_FILE_URL)

echo "Resource Group: $RESOURCE_GROUP"
echo "Storage Account: $STORAGE_ACCOUNT"
echo "Container Name: $CONTAINER_NAME"

# Check if PDF URL is provided
if [ -z "$PDF_FILE_URL" ]; then
    echo "PDF_FILE_URL not set. Skipping PDF extraction."
    echo "You can manually upload the extracted PDF text to the blob storage container later."
    exit 0
fi

# Check if AI Services endpoint is configured
if [ -z "$AI_SERVICES_ENDPOINT" ]; then
    echo "AZURE_AI_SERVICES_ENDPOINT not set. Skipping PDF extraction."
    echo "Please configure AI Services endpoint and run extraction manually."
    exit 0
fi

echo "PDF extraction during deployment is optional."
echo "The application will extract the PDF text on first run if not already present."
echo ""
echo "To manually extract and upload PDF text:"
echo "1. Extract text using Azure AI Document Intelligence"
echo "2. Upload to: https://$STORAGE_ACCOUNT.blob.core.windows.net/$CONTAINER_NAME/extracted-text.txt"
echo ""
echo "Post-provision completed successfully."
