targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that will be used to name resources')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string

@description('Id of the user or app to assign application roles')
param principalId string = ''

@description('PDF file URL for the sleep training document')
param pdfFileUrl string = ''

@description('Client IP address for storage firewall access')
param clientIpAddress string = ''

@description('Azure AD Tenant ID for authentication')
param azureAdTenantId string = ''

@description('Azure AD Client ID for authentication')
param azureAdClientId string = ''

// Tags for all resources
var tags = {
  'azd-env-name': environmentName
  'application': 'sleep-training-assistant'
}

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))

// Resource group
resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: '${abbrs.resourcesResourceGroups}${environmentName}'
  location: location
  tags: tags
}

// Azure OpenAI
module openAI './modules/openai.bicep' = {
  name: 'openai'
  scope: rg
  params: {
    name: '${abbrs.cognitiveServicesAccounts}${resourceToken}'
    location: location
    tags: tags
    deployments: [
      {
        name: '4o-mini'
        model: {
          format: 'OpenAI'
          name: 'gpt-4o-mini'
          version: '2024-07-18'
        }
        sku: {
          name: 'Standard'
          capacity: 25
        }
      }
    ]
  }
}

// App Service
module appService './modules/app-service.bicep' = {
  name: 'appservice'
  scope: rg
  params: {
    name: '${abbrs.webSitesAppService}${resourceToken}'
    location: location
    tags: tags
    appSettings: {
      AZURE_OPENAI_ENDPOINT: openAI.outputs.endpoint
      AZURE_OPENAI_DEPLOYMENT_NAME: '4o-mini'
      STORAGE_ACCOUNT_NAME: '${abbrs.storageStorageAccounts}${resourceToken}'
      STORAGE_CONTAINER_NAME: 'pdf-content'
      STORAGE_TABLE_NAME: 'SleepTracking'
      PDF_FILE_URL: pdfFileUrl
      AzureAd__TenantId: azureAdTenantId
      AzureAd__ClientId: azureAdClientId
    }
  }
}

// Storage account for PDF text (deployed after app service to get managed identity)
module storage './modules/storage.bicep' = {
  name: 'storage'
  scope: rg
  params: {
    name: '${abbrs.storageStorageAccounts}${resourceToken}'
    location: location
    tags: tags
    principalId: appService.outputs.principalId
    principalType: 'ServicePrincipal'
    userPrincipalId: principalId
    clientIpAddress: clientIpAddress
  }
}

// Grant App Service Managed Identity access to Azure OpenAI
module openAIRoleAssignment './modules/openai-role.bicep' = {
  name: 'openai-role'
  scope: rg
  params: {
    openAIAccountName: openAI.outputs.name
    principalId: appService.outputs.principalId
  }
}

// Outputs
output AZURE_LOCATION string = location
output AZURE_TENANT_ID string = tenant().tenantId
output AZURE_RESOURCE_GROUP string = rg.name

output AZURE_OPENAI_ENDPOINT string = openAI.outputs.endpoint
output AZURE_OPENAI_DEPLOYMENT_NAME string = '4o-mini'

output AZURE_STORAGE_ACCOUNT_NAME string = storage.outputs.name
output AZURE_STORAGE_CONTAINER_NAME string = 'pdf-content'

output SERVICE_WEB_NAME string = appService.outputs.name
output SERVICE_WEB_URI string = appService.outputs.uri
