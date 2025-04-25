param baseName string = resourceGroup().name
param location string = resourceGroup().location
param environment string = 'dev' // 'dev', 'test', 'prod'

// 環境に基づいた設定
var logRetention = environment == 'prod' ? 30 : 7

// Create Log Analytics Workspace
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2021-06-01' = {
  name: 'law-${baseName}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: logRetention // 環境に基づいて保持期間を設定
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

// 注: データ上限キャップはBicepの直接のプロパティとしてサポートされていない可能性があります
// 必要に応じてAzure CLIやPortalから設定してください

output workspaceId string = logAnalyticsWorkspace.id
