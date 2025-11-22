param name string
param location string = resourceGroup().location
param tags object = {}

param deployments array = []

resource account 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: name
  location: location
  tags: tags
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: name
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: true  // Disable API key authentication, use Entra ID only
  }
}

@batchSize(1)
resource deployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = [for deployment in deployments: {
  parent: account
  name: deployment.name
  sku: deployment.sku
  properties: {
    model: deployment.model
  }
}]

output endpoint string = account.properties.endpoint
output name string = account.name
output id string = account.id
