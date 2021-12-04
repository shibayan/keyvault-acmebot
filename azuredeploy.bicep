@description('The name of the function app that you wish to create.')
@maxLength(14)
param appNamePrefix string

@description('Email address for ACME account.')
param mailAddress string

@description('Certification authority ACME Endpoint.')
@allowed([
  'https://acme-v02.api.letsencrypt.org/'
  'https://api.buypass.com/acme/'
  'https://acme.zerossl.com/v2/DV90/'
])
param acmeEndpoint string = 'https://acme-v02.api.letsencrypt.org/'

@description('If you choose true, create and configure a key vault at the same time.')
@allowed([
  true
  false
])
param createWithKeyVault bool = true

@description('Specifies whether the key vault is a standard vault or a premium vault.')
@allowed([
  'standard'
  'premium'
])
param keyVaultSkuName string = 'standard'

@description('Enter the base URL of an existing Key Vault. (ex. https://example.vault.azure.net)')
param keyVaultBaseUrl string = ''

@description('A new GUID used to identify the role assignment')
param roleNameGuid string = newGuid()

var functionAppName = 'func-${appNamePrefix}-${substring(uniqueString(resourceGroup().id, deployment().name), 0, 4)}'
var appServicePlanName = 'plan-${appNamePrefix}-${substring(uniqueString(resourceGroup().id, deployment().name), 0, 4)}'
var appInsightsName = 'appi-${appNamePrefix}-${substring(uniqueString(resourceGroup().id, deployment().name), 0, 4)}'
var workspaceName = 'log-${appNamePrefix}-${substring(uniqueString(resourceGroup().id, deployment().name), 0, 4)}'
var storageAccountName = 'st${uniqueString(resourceGroup().id, deployment().name)}func'
var keyVaultName = 'kv-${appNamePrefix}-${substring(uniqueString(resourceGroup().id, deployment().name), 0, 4)}'
var appInsightsEndpoints = {
  AzureCloud: 'applicationinsights.azure.com'
  AzureChinaCloud: 'applicationinsights.azure.cn'
  AzureUSGovernment: 'applicationinsights.us'
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2021-06-01' = {
  name: storageAccountName
  location: resourceGroup().location
  kind: 'Storage'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
  }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2021-02-01' = {
  name: appServicePlanName
  location: resourceGroup().location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
    size: 'Y1'
    family: 'Y'
  }
  properties: {}
}

resource workspace 'Microsoft.OperationalInsights/workspaces@2021-06-01' = {
  name: workspaceName
  location: resourceGroup().location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: resourceGroup().location
  kind: 'web'
  tags: {
    'hidden-link:${resourceGroup().id}/providers/Microsoft.Web/sites/${functionAppName}': 'Resource'
  }
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
  }
}

resource functionApp 'Microsoft.Web/sites@2021-02-01' = {
  name: functionAppName
  location: resourceGroup().location
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    clientAffinityEnabled: false
    httpsOnly: true
    serverFarmId: appServicePlan.id
    siteConfig: {
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: 'InstrumentationKey=${appInsights.properties.InstrumentationKey};EndpointSuffix=${appInsightsEndpoints[environment().name]}'
        }
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};AccountKey=${listKeys(storageAccount.id, storageAccount.apiVersion).keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};AccountKey=${listKeys(storageAccount.id, storageAccount.apiVersion).keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(functionAppName)
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: 'https://shibayan.blob.core.windows.net/azure-keyvault-letsencrypt/v3/latest.zip'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~3'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet'
        }
        {
          name: 'Acmebot:Contacts'
          value: mailAddress
        }
        {
          name: 'Acmebot:Endpoint'
          value: acmeEndpoint
        }
        {
          name: 'Acmebot:VaultBaseUrl'
          value: (createWithKeyVault ? 'https://${keyVaultName}${environment().suffixes.keyvaultDns}' : keyVaultBaseUrl)
        }
        {
          name: 'Acmebot:Environment'
          value: environment().name
        }
      ]
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
    }
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2019-09-01' = if (createWithKeyVault) {
  name: keyVaultName
  location: resourceGroup().location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: keyVaultSkuName
    }
    enableRbacAuthorization: true
  }
}

resource keyVault_roleAssignment 'Microsoft.Authorization/roleAssignments@2021-04-01-preview' = if (createWithKeyVault) {
  scope: keyVault
  name: roleNameGuid
  properties: {
    roleDefinitionId: resourceId('Microsoft.Authorization/roleDefinitions/', 'a4417e6f-fecd-4de8-b567-7b0420556985')
    principalId: functionApp.identity.principalId
  }
}

output functionAppName string = functionApp.name
output identity object = functionApp.identity
output keyVaultName string = createWithKeyVault ? keyVault.name : ''
