@description('SignalR インスタンス名')
param signalrName string =  'signalr-${resourceGroup().name}'

@description('リソース配置先リージョン')
param location string = resourceGroup().location

// 1. Azure SignalR サービス作成
resource signalr 'Microsoft.SignalRService/signalR@2022-08-01-preview' = {
  name: signalrName
  location: location
  sku: {
    name: 'Standard_S1'
    tier: 'Standard'
    capacity: 1
  }
  kind: 'SignalR'
  properties: {
    cors: {
      allowedOrigins: [
        '*' 
      ]
    }
    // 必要に応じた追加設定…
  }
}
output signalrId string = signalr.id
output signalrApiVersion string = signalr.apiVersion
