@description('Event Hub Namespaceの名前')
param eventHubNamespaceName string = 'ehns-${resourceGroup().name}'

@description('Event Hub クライアントのための認証ルール名')
param authorizationRuleName string = 'EventHubClientAuthRule'

@description('既存のKey Vaultの名前')
param keyVaultName string = 'kv-${resourceGroup().name}'

@description('Orleans用のキュータイプ')
param orleansQueueType string = 'eventhub'

// 既存のEvent Hub Namespaceを参照
resource namespace 'Microsoft.EventHub/namespaces@2022-10-01-preview' existing = if (orleansQueueType == 'eventhub') {
  name: eventHubNamespaceName
}

// 既存の認証ルールを参照
resource authRule 'Microsoft.EventHub/namespaces/authorizationRules@2022-10-01-preview' existing = if (orleansQueueType == 'eventhub') {
  parent: namespace
  name: authorizationRuleName
}

// 既存のKey Vaultを参照
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

// Event Hub接続文字列をKey Vaultに保存
resource eventHubConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (orleansQueueType == 'eventhub') {
  parent: keyVault
  name: 'EventHubConnectionString'
  properties: {
    value: listKeys(authRule.id, authRule.apiVersion).primaryConnectionString
  }
}

// Event Hub主キーをKey Vaultに保存
resource eventHubPrimaryKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (orleansQueueType == 'eventhub') {
  parent: keyVault
  name: 'EventHubPrimaryKey'
  properties: {
    value: listKeys(authRule.id, authRule.apiVersion).primaryKey
  }
}

// Event Hub名前空間名をKey Vaultに保存
resource eventHubNamespaceNameSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (orleansQueueType == 'eventhub') {
  parent: keyVault
  name: 'EventHubNamespaceName'
  properties: {
    value: namespace.name
  }
}

// 出力
output eventHubConnectionStringSecretName string = orleansQueueType == 'eventhub' ? eventHubConnectionStringSecret.name : ''
output eventHubPrimaryKeySecretName string = orleansQueueType == 'eventhub' ? eventHubPrimaryKeySecret.name : ''
output eventHubNamespaceNameSecretName string = orleansQueueType == 'eventhub' ? eventHubNamespaceNameSecret.name : ''
output keyVaultName string = keyVault.name
