param baseName string = resourceGroup().name
param location string = resourceGroup().location
param environment string = 'dev' // 'dev', 'test', 'prod'

// 環境に基づいた設定
var samplingPercentage = environment == 'prod' ? 50 : 10

// Create Application Insights
// you might need to do folloing command
// az provider register --namespace microsoft.operationalinsights
resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'ai-${baseName}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Request_Source: 'rest'
    IngestionMode: 'ApplicationInsights'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    SamplingPercentage: samplingPercentage // サンプリングを設定（本番環境では50%、それ以外では10%）
  }
}

output instrumentationKey string = applicationInsights.properties.InstrumentationKey
output connectionString string = applicationInsights.properties.ConnectionString
