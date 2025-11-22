param name string
param location string = resourceGroup().location
param tags object = {}
param principalId string

resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: false
  }
}

// Assign Key Vault Secrets Officer role to the principal (for azd)
var keyVaultSecretsOfficerRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7')

resource keyVaultSecretsOfficerAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(principalId)) {
  name: guid(keyVault.id, principalId, keyVaultSecretsOfficerRole)
  scope: keyVault
  properties: {
    principalId: principalId
    roleDefinitionId: keyVaultSecretsOfficerRole
    principalType: 'User'
  }
}

output name string = keyVault.name
output id string = keyVault.id
output endpoint string = keyVault.properties.vaultUri
