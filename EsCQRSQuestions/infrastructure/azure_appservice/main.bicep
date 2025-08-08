// Main Bicep file to deploy all resources for Sekiban Orleans Aspire on Azure App Service
// This file orchestrates the deployment of various modules.

targetScope = 'resourceGroup'

@description('Orleansクラスターのタイプ指定（cosmos または 他の種類）')
@allowed(['cosmos', 'azuretable'])
param orleansClusterType string = 'cosmos'
param orleansDefaultGrainType string = 'cosmos'


@description('Orleans用のキュータイプ')
param orleansQueueType string = 'eventhub'


// 1. Key Vault
module keyVaultCreate '1.keyvault/create.bicep' = {
  name: 'keyVaultDeployment'
  params: {}
}

// 2. Storages
module storageCreate '2.storages/1.create.bicep' = {
  name: 'storageCreateDeployment'
  params: {}
}

module storageSaveKeyVault '2.storages/2.save-keyvault.bicep' = {
  name: 'storageSaveKeyVaultDeployment'
  params: {}
  dependsOn: [
    keyVaultCreate
    storageCreate
  ]
}

// 3. Cosmos DB
module cosmosCreate '3.cosmos/1.create.bicep' = {
  name: 'cosmosCreateDeployment'
  params: {}
}

module cosmosDatabase '3.cosmos/2.database.bicep' = {
  name: 'cosmosDatabaseDeployment'
  params: {}
  dependsOn: [
    cosmosCreate
  ]
}

module cosmosContainer '3.cosmos/3.container.bicep' = {
  name: 'cosmosContainerDeployment'
  params: {}
  dependsOn: [
    cosmosDatabase
  ]
}

module cosmosOrleansContainer '3.cosmos/4.orleans-cluster-container.bicep' = {
  name: 'cosmosOrleansContainerDeployment'
  params: {
    orleansClusterType: orleansClusterType
  }
  dependsOn: [
    cosmosDatabase
  ]
}

module cosmosOrleansDefaultGrainContainer '3.cosmos/5.orleans-grain-container.bicep' = {
  name: 'cosmosOrleansDefaultGrainDeployment'
  params: {
    orleansClusterType: orleansClusterType
    orleansDefaultGrainType: orleansDefaultGrainType
  }
  dependsOn: [
  cosmosDatabase
  // Ensure Orleans DB is created before creating OrleansStorage container
  cosmosOrleansContainer
  ]
}

module cosmosSaveKeyVault '3.cosmos/6.save-keyvault.bicep' = {
  name: 'cosmosSaveKeyVaultDeployment'
  params: {}
  dependsOn: [
    keyVaultCreate
    cosmosCreate
  ]
}

// 4. VNet
// Note: Excludes add-subnet-general.bicep and deploy-vnet-general.bicep as requested.
module vnetCreate '4.vnet/1.create.bicep' = {
  name: 'vnetCreateDeployment'
  params: {}
}

// 5. Application Insights & Log Analytics
module appInsightsCreate '5.applicationinsights_and_log/1.application-insights.bicep' = {
  name: 'appInsightsCreateDeployment'
  params: {}
}

module logAnalyticsCreate '5.applicationinsights_and_log/2.log-analytics-workspace.bicep' = {
  name: 'logAnalyticsCreateDeployment'
  params: {}
}

module signalrCreate '7.signalr/1.create-signalr.bicep' = {
  name: 'signalrCreateDeployment'
  params: {}
}
module signalrSaveKeyVault '7.signalr/2.save-keyvault.bicep' = {
  name: 'signalrSaveKeyVaultDeployment'
  params: {}
  dependsOn: [
    keyVaultCreate
    signalrCreate
  ]
}

// 6.5. EventHub
module eventHubCreate '6.eventhub/1.create.bicep' = {
  name: 'eventHubCreateDeployment'
  params: {
    orleansQueueType: orleansQueueType
  }
}

module eventHubSaveKeyVault '6.eventhub/2.save-keyvalult.bicep' = {
  name: 'eventHubSaveKeyVaultDeployment'
  params: {
    orleansQueueType: orleansQueueType
  }
  dependsOn: [
    keyVaultCreate
    eventHubCreate
  ]
}

// 6. Backend App Service
module backendPlan '8.backend/1.plan.bicep' = {
  name: 'backendPlanDeployment'
  params: {}
}

module backendAppServiceCreate '8.backend/2.app-service-create.bicep' = {
  name: 'backendAppServiceCreateDeployment'
  params: {}
  dependsOn: [
    backendPlan
  ]
}

module backendKeyVaultAccess '8.backend/3.key-vault-access.bicep' = {
  name: 'backendKeyVaultAccessDeployment'
  params: {}
  dependsOn: [
    keyVaultCreate
    backendAppServiceCreate
  ]
}

module backendConnectionStrings '8.backend/4.connection-strings.bicep' = {
  name: 'backendConnectionStringsDeployment'
  params: {
    orleansQueueType: orleansQueueType
  }
  dependsOn: [
    keyVaultCreate // Needs Key Vault URI
    backendAppServiceCreate
  ]
}

module backendDiagnosticSettings '8.backend/5.diagnostic-settings.bicep' = {
  name: 'backendDiagnosticSettingsDeployment'
  params: {}
  dependsOn: [
    logAnalyticsCreate
    backendAppServiceCreate
  ]
}

module backendAppSettings '8.backend/6.app-settings.bicep' = {
  name: 'backendAppSettingsDeployment'
  params: {
    orleansClusterType: orleansClusterType
    orleansDefaultGrainType: orleansDefaultGrainType
    orleansQueueType: orleansQueueType
  }
  dependsOn: [
    keyVaultCreate
    storageCreate
    cosmosCreate
    appInsightsCreate
    backendAppServiceCreate
    eventHubCreate
  ]
}

module backendVnetIntegration '8.backend/7.vnet-integration.bicep' = {
  name: 'backendVnetIntegrationDeployment'
  params: {}
  dependsOn: [
    vnetCreate
    backendAppServiceCreate
  ]
}

module backendIpAccess '8.backend/8.ipaccess.bicep' = {
  name: 'backendIpAccessDeployment'
  params: {}
  dependsOn: [
    backendAppServiceCreate
  ]
}

// 7. Blazor Frontend App Service
module blazorPlan '10.blazor/1.plan.bicep' = {
  name: 'blazorPlanDeployment'
  params: {}
}

module blazorAppService '10.blazor/2.app-service.bicep' = {
  name: 'blazorAppServiceDeployment'
  params: {}
  dependsOn: [
    blazorPlan
  ]
}

module blazorDiagnosticSettings '10.blazor/3.diagnositic-settings.bicep' = {
  name: 'blazorDiagnosticSettingsDeployment'
  params: {}
  dependsOn: [
    logAnalyticsCreate
    blazorAppService
  ]
}

module blazorAppSettings '10.blazor/4.app-settings.bicep' = {
  name: 'blazorAppSettingsDeployment'
  params: {}
  dependsOn: [
    appInsightsCreate
    backendAppServiceCreate // Depends on backend URL output from its creation module
    blazorAppService
  ]
}

module blazorVnetIntegration '10.blazor/5.vnet-integration.bicep' = {
  name: 'blazorVnetIntegrationDeployment'
  params: {}
  dependsOn: [
    vnetCreate
    blazorAppService
  ]
}

// 8. Admin Web App Service
module adminWebPlan '9.adminweb/1.plan.bicep' = {
  name: 'adminWebPlanDeployment'
  params: {}
}

module adminWebAppService '9.adminweb/2.app-service.bicep' = {
  name: 'adminWebAppServiceDeployment'
  params: {}
  dependsOn: [
    adminWebPlan
  ]
}

module adminWebDiagnosticSettings '9.adminweb/3.diagnositic-settings.bicep' = {
  name: 'adminWebDiagnosticSettingsDeployment'
  params: {}
  dependsOn: [
    logAnalyticsCreate
    adminWebAppService
  ]
}

module adminWebAppSettings '9.adminweb/4.app-settings.bicep' = {
  name: 'adminWebAppSettingsDeployment'
  params: {}
  dependsOn: [
    appInsightsCreate
    backendAppServiceCreate // Depends on backend URL output from its creation module
    adminWebAppService
  ]
}

module adminWebVnetIntegration '9.adminweb/5.vnet-integration.bicep' = {
  name: 'adminWebVnetIntegrationDeployment'
  params: {}
  dependsOn: [
    vnetCreate
    adminWebAppService
  ]
}

// Outputs can be added here if needed, for example:
// output backendHostName string = backendAppServiceCreate.outputs.hostName
// output frontendHostName string = blazorAppService.outputs.hostName
