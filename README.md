# Sleep Training Assistant

An AI-powered sleep training assistant built with Blazor Server and Azure OpenAI. The application helps users manage sleep training by answering questions from a sleep training guide (PDF), tracking data in Excel, and providing intelligent assistance.

## Features

- 💬 **Chat Interface**: Interactive chat UI powered by Azure OpenAI GPT-4o-mini
- 📄 **PDF Q&A**: Answer questions from a 7-page sleep training guide
- 📊 **Excel Integration**: Read and update Excel files stored in OneDrive
- ⏰ **Time Functions**: Get current time in CDT timezone
- 🔐 **Secure Authentication**: Easy Auth with Entra ID
- ☁️ **Azure Native**: Fully deployed on Azure with infrastructure as code

## Architecture

- **Frontend**: Blazor Server (.NET 8)
- **AI Model**: Azure OpenAI GPT-4o-mini
- **PDF Processing**: Azure AI Document Intelligence
- **Excel Access**: Microsoft Graph API
- **Storage**: Azure Blob Storage
- **Authentication**: Entra ID with Easy Auth
- **Deployment**: Azure Developer CLI (azd)

## Prerequisites

- [Azure Developer CLI (azd)](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure Subscription](https://azure.microsoft.com/free/)
- An existing Azure AI Services resource (or will create new)
- Access to Azure OpenAI Service (requires approval)

## Local Development Setup

### 1. Clone and Initialize

```bash
cd sleep-training-assistant
azd auth login
```

### 2. Configure Environment Variables

Create a new environment:

```bash
azd env new <environment-name>
```

Set required variables:

```bash
# Optional: If you have existing Azure AI Services
azd env set AZURE_AI_SERVICES_ENDPOINT "https://your-ai-services.cognitiveservices.azure.com/"
azd env set AZURE_AI_SERVICES_KEY "your-key"

# Optional: PDF file URL (can be set later)
azd env set PDF_FILE_URL "https://path-to-your-pdf-file.pdf"

# Optional: Excel file URL (can be set later in Azure Portal)
azd env set EXCEL_FILE_URL "https://1drv.ms/x/your-sharing-link"
```

## Deployment

### Deploy Everything with One Command

```bash
azd up
```

This command will:
1. Provision all Azure resources (App Service, OpenAI, Storage, Key Vault)
2. Create an App Registration in Entra ID
3. Deploy the Blazor application
4. Configure Easy Auth

**Deployment time**: ~10-15 minutes

### Post-Deployment Manual Steps

After `azd up` completes, you need to perform these manual steps:

#### Step 1: Grant Admin Consent for Graph API Permissions

1. Go to [Azure Portal](https://portal.azure.com) → **Entra ID** → **App Registrations**
2. Find your app (search for your environment name)
3. Click **API Permissions**
4. Click **Grant admin consent for [Your Organization]**
5. Confirm the consent

**Required Permissions:**
- `Files.Read.All` or `Files.ReadWrite.All` (delegated)
- `User.Read` (delegated)

#### Step 2: Configure Excel File URL

1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to your **App Service** (name will include your environment name)
3. Go to **Configuration** → **Application settings**
4. Update `EXCEL_FILE_URL` with your OneDrive sharing link or file path
5. Click **Save**

**Supported formats:**
- OneDrive sharing link: `https://1drv.ms/x/...`
- SharePoint link: `https://yourorg.sharepoint.com/...`

#### Step 3: Configure PDF File URL (if not set earlier)

1. In the same **App Service Configuration** page
2. Update `PDF_FILE_URL` with your PDF URL
3. Click **Save**

**Or upload PDF to blob storage:**

```bash
# Get storage account name
STORAGE_ACCOUNT=$(azd env get-value AZURE_STORAGE_ACCOUNT_NAME)

# Upload PDF
az storage blob upload \
  --account-name $STORAGE_ACCOUNT \
  --container-name pdf-content \
  --name sleep-training-guide.pdf \
  --file /path/to/your/file.pdf \
  --auth-mode login
```

#### Step 4: Configure Easy Auth with App Registration

The infrastructure creates an App Registration, but Easy Auth needs to be configured manually:

1. Go to your **App Service** → **Authentication**
2. Click **Add identity provider**
3. Select **Microsoft**
4. Choose **Pick an existing app registration**
5. Select the app registration created by azd
6. Set **Issuer URL**: `https://login.microsoftonline.com/{tenant-id}/v2.0`
7. Click **Add**

## Inviting Users

To allow other users to access the application:

### Internal Users (Same Organization)
They can sign in automatically if they're in your Entra ID tenant.

### External Users (B2B Guests)

1. Go to [Azure Portal](https://portal.azure.com) → **Entra ID** → **Users**
2. Click **New user** → **Invite external user**
3. Enter their email address
4. Click **Invite**
5. They'll receive an email to accept the invitation
6. Once accepted, they can sign in to your app

**Note**: Inviting to Entra ID (for app access) is different from inviting to Azure subscription (for resource management).

## Application Usage

### Access the Application

After deployment, get your app URL:

```bash
azd env get-value SERVICE_WEB_URI
```

Visit the URL and sign in with your Entra ID account.

### Chat Examples

**Ask about the PDF:**
```
What are the key principles of sleep training?
```

**Get current time:**
```
What time is it in CDT?
```

**Update Excel:**
```
Update cell A1 to "Sleep Training Log" and A2 to today's date
```

**Read Excel data:**
```
Show me the data from range A1:C10
```

## Project Structure

```
sleep-training-assistant/
├── src/
│   └── BlazorApp/               # Blazor Server application
│       ├── Components/
│       │   ├── Pages/
│       │   │   └── Chat.razor   # Main chat interface
│       │   └── Layout/
│       │       └── MainLayout.razor
│       ├── Services/
│       │   ├── AzureOpenAIService.cs   # AI chat service
│       │   ├── GraphService.cs         # Excel operations
│       │   ├── PdfService.cs           # PDF text loading
│       │   └── TimeService.cs          # Time utilities
│       ├── Models/
│       │   ├── ChatMessage.cs
│       │   └── ExcelData.cs
│       └── Program.cs
├── infra/                       # Infrastructure as Code
│   ├── main.bicep              # Main infrastructure
│   └── modules/
│       ├── app-service.bicep
│       ├── openai.bicep
│       ├── storage.bicep
│       └── keyvault.bicep
├── scripts/
│   ├── postprovision.sh        # Post-deployment script (Linux/Mac)
│   └── postprovision.ps1       # Post-deployment script (Windows)
├── azure.yaml                   # Azure Developer CLI config
└── README.md
```

## Cost Estimate

| Resource | Tier | Monthly Cost |
|----------|------|--------------|
| App Service Plan | B1 | ~$13 |
| Azure OpenAI | GPT-4o-mini usage | ~$5-20 |
| Storage Account | Standard LRS | ~$0.50 |
| Key Vault | Standard | ~$0.10 |
| AI Document Intelligence | One-time extraction | ~$0.01 |
| **Total** | | **~$20-40/month** |

## Configuration Reference

### Environment Variables

Set these with `azd env set <name> <value>`:

| Variable | Required | Description |
|----------|----------|-------------|
| `AZURE_AI_SERVICES_ENDPOINT` | Optional | Existing AI Services endpoint |
| `AZURE_AI_SERVICES_KEY` | Optional | Existing AI Services key |
| `PDF_FILE_URL` | Optional | URL to sleep training PDF |
| `EXCEL_FILE_URL` | Optional | OneDrive URL to Excel file |

### App Settings (in Azure Portal)

Configure these in App Service → Configuration:

- `AZURE_OPENAI_ENDPOINT`: Auto-configured
- `AZURE_OPENAI_DEPLOYMENT_NAME`: Auto-configured (gpt-4o-mini)
- `AZURE_KEY_VAULT_ENDPOINT`: Auto-configured
- `STORAGE_ACCOUNT_NAME`: Auto-configured
- `EXCEL_FILE_URL`: Set manually
- `PDF_FILE_URL`: Set manually

## Troubleshooting

### PDF text not loading

1. Check if `PDF_FILE_URL` is configured in App Service settings
2. Verify Azure AI Services endpoint and key are correct
3. Check application logs for extraction errors
4. Manually upload extracted text to blob storage

### Excel operations failing

1. Verify admin consent was granted for Graph API permissions
2. Check `EXCEL_FILE_URL` format is correct
3. Ensure the signed-in user has access to the Excel file
4. Check application logs for Graph API errors

### Authentication issues

1. Verify Easy Auth is configured with the correct App Registration
2. Check redirect URIs include your app's URL
3. Ensure users are in your Entra ID tenant or invited as guests

### View Logs

```bash
# Stream logs in real-time
az webapp log tail \
  --name $(azd env get-value SERVICE_WEB_NAME) \
  --resource-group $(azd env get-value AZURE_RESOURCE_GROUP)
```

## Clean Up

To delete all resources:

```bash
azd down
```

This will delete the resource group and all resources, but NOT the App Registration (must be deleted manually).

## Security Notes

- All secrets stored in Azure Key Vault
- Managed Identity used for Azure service authentication
- HTTPS enforced for all connections
- Easy Auth provides enterprise-grade authentication
- No secrets in code or configuration files

## Contributing

This is a template project. Feel free to customize for your needs.

## License

MIT License

---

**Need Help?**

- [Azure Developer CLI Documentation](https://learn.microsoft.com/azure/developer/azure-developer-cli/)
- [Azure OpenAI Documentation](https://learn.microsoft.com/azure/ai-services/openai/)
- [Microsoft Graph API Documentation](https://learn.microsoft.com/graph/)
