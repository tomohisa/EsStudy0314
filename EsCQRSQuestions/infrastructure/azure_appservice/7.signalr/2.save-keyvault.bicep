@description('SignalR インスタンス名')
param signalrName string =  'signalr-${resourceGroup().name}'

@description('The name of the existing Key Vault to store secrets')
param keyVaultName string = 'kv-${resourceGroup().name}'

// 既存のSignalRリソースへの参照
resource signalr 'Microsoft.SignalRService/signalR@2022-08-01-preview' existing = {
  name: signalrName
}

// Reference to existing Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

// SignalR 接続文字列をシークレットとして格納
resource signalrSecret 'Microsoft.KeyVault/vaults/secrets@2021-11-01-preview' = {
  parent: keyVault
  name: 'SignalRConnectionString'
  properties: {
    value: signalr.listKeys().primaryConnectionString
  }
}
