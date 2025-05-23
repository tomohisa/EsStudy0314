@allowed([ 'dev', 'test', 'prod' ])
param environment string = 'dev'

param baseName string = resourceGroup().name
param location string = resourceGroup().location

// prod は 30 日、それ以外は 7 日
var logRetention = environment == 'prod' ? 90 : 30

@description('Daily ingestion cap in GB (0 で無制限)')
param dailyQuotaGb string = '0.1'

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'law-${baseName}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: logRetention
    workspaceCapping: {
      dailyQuotaGb: json(dailyQuotaGb)
    }
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

output workspaceId string = logAnalyticsWorkspace.id
