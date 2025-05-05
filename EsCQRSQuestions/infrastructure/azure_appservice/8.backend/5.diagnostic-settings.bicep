param appServiceName string = 'backend-${resourceGroup().name}'
param logAnalyticsWorkspaceName string = 'law-${resourceGroup().name}'
param environment string = 'dev' // 'dev', 'test', 'prod'

// 環境に基づいた設定
var enableDetailedLogs = environment == 'prod' ? true : false

// get existing Log Analytics Workspace
var logAnalyticsWorkspaceId = resourceId('Microsoft.OperationalInsights/workspaces', logAnalyticsWorkspaceName)

// Reference to the existing App Service
resource webApp 'Microsoft.Web/sites@2022-09-01' existing = {
  name: appServiceName
}

// App Service diagnostic settings
resource diagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'diag-${appServiceName}'
  scope: webApp
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'AppServiceHTTPLogs'
        enabled: true // 重要なHTTPログは常に有効
      }
      {
        category: 'AppServiceConsoleLogs'
        enabled: enableDetailedLogs // 詳細ログは本番環境のみで有効
      }
      {
        category: 'AppServiceAppLogs'
        enabled: true // アプリケーションログは常に有効
      }
      {
        category: 'AppServiceAuditLogs'
        enabled: enableDetailedLogs // 監査ログは本番環境のみで有効
      }
      {
        category: 'AppServiceIPSecAuditLogs'
        enabled: false // IPSecログは通常不要なので無効化
      }
      {
        category: 'AppServicePlatformLogs'
        enabled: enableDetailedLogs // プラットフォームログは本番環境のみで有効
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true // メトリクスは常に有効（容量は少ない）
      }
    ]
  }
}
