@description('Event Hub インスタンス名')
param eventHubName string =  'eventhub-${resourceGroup().name}'

@description('リソース配置先リージョン')
param location string = resourceGroup().location

@description('Event Hub Namespaceの名前')
param namespaceName string = 'ehns-${resourceGroup().name}'

@description('Orleans用のキュータイプ')
param orleansQueueType string = 'eventhub'

@description('Event Hubの容量（スループットユニット）')
param skuCapacity int = 1

@description('Event Hub SKU名')
param skuName string = 'Basic'

@description('Event Hub クライアントのための認証ルール名')
param authorizationRuleName string = 'EventHubClientAuthRule'

// Event Hub Namespaceを作成
resource namespace 'Microsoft.EventHub/namespaces@2022-10-01-preview' = if (orleansQueueType == 'eventhub') {
  name: namespaceName
  location: location
  sku: {
    name: skuName
    tier: skuName
    capacity: skuCapacity
  }
  properties: {
    isAutoInflateEnabled: false
    maximumThroughputUnits: 0
  }
}

// Event Hubの作成
resource eventHub 'Microsoft.EventHub/namespaces/eventhubs@2022-10-01-preview' = if (orleansQueueType == 'eventhub') {
  parent: namespace
  name: eventHubName
  properties: {
    messageRetentionInDays: 1
    partitionCount: 2
  }
}

// Event Hubへのアクセス権の作成
resource authorizationRule 'Microsoft.EventHub/namespaces/authorizationRules@2022-10-01-preview' = if (orleansQueueType == 'eventhub') {
  parent: namespace
  name: authorizationRuleName
  properties: {
    rights: [
      'Listen'
      'Send'
      'Manage'
    ]
  }
}

// 出力
output eventHubNamespaceName string = orleansQueueType == 'eventhub' ? namespace.name : ''
output eventHubName string = orleansQueueType == 'eventhub' ? eventHub.name : ''
output eventHubConnectionString string = orleansQueueType == 'eventhub' ? listKeys(authorizationRule.id, authorizationRule.apiVersion).primaryConnectionString : ''
output eventHubPrimaryKey string = orleansQueueType == 'eventhub' ? listKeys(authorizationRule.id, authorizationRule.apiVersion).primaryKey : ''
