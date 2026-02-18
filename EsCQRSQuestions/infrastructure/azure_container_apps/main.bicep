targetScope = 'resourceGroup'

@description('Log Analytics retention days')
@minValue(30)
@maxValue(730)
param logRetentionInDays int = 30

@description('Log Analytics daily ingestion cap in GB (0 = unlimited)')
param logDailyQuotaGb string = '0.1'

@description('Backend min replicas')
@minValue(0)
param backendMinReplicas int = 1

@description('Backend max replicas')
@minValue(1)
param backendMaxReplicas int = 3

@description('Frontend min replicas')
@minValue(0)
param frontendMinReplicas int = 0

@description('Frontend max replicas')
@minValue(1)
param frontendMaxReplicas int = 2

@description('AdminWeb min replicas')
@minValue(0)
param adminwebMinReplicas int = 0

@description('AdminWeb max replicas')
@minValue(1)
param adminwebMaxReplicas int = 2

@description('Backend container image')
param backendImage string = 'mcr.microsoft.com/dotnet/samples:aspnetapp'

@description('Frontend container image')
param frontendImage string = 'mcr.microsoft.com/dotnet/samples:aspnetapp'

@description('AdminWeb container image')
param adminwebImage string = 'mcr.microsoft.com/dotnet/samples:aspnetapp'

@description('Enable Easy Auth (Microsoft Entra ID) for AdminWeb')
param enableAdminEasyAuth bool = false

@description('Tenant ID for AdminWeb Easy Auth')
param adminEasyAuthTenantId string = ''

@description('Client ID (App Registration) for AdminWeb Easy Auth')
param adminEasyAuthClientId string = ''

@secure()
@description('Client secret for AdminWeb Easy Auth app registration')
param adminEasyAuthClientSecret string = ''

var suffix = substring(uniqueString(resourceGroup().id), 0, 6)
var acrName = 'acr${uniqueString(resourceGroup().id, 'acr')}'
var logAnalyticsName = 'law-${suffix}'
var appInsightsName = 'ai-${suffix}'
var signalRName = 'signalr-${suffix}'
var cosmosAccountName = 'cosmos${uniqueString(resourceGroup().id, 'cosmos')}'
var storageAccountName = take('st${replace(uniqueString(resourceGroup().id, 'storage'), '-', '')}', 24)
var containerEnvName = 'cae-${suffix}'
var backendAppName = 'be-${suffix}'
var frontendAppName = 'fe-${suffix}'
var adminwebAppName = 'ad-${suffix}'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: resourceGroup().location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: logRetentionInDays
    workspaceCapping: {
      dailyQuotaGb: json(logDailyQuotaGb)
    }
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: resourceGroup().location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    IngestionMode: 'LogAnalytics'
    WorkspaceResourceId: logAnalytics.id
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: resourceGroup().location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
  }
}

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' = {
  name: cosmosAccountName
  location: resourceGroup().location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: resourceGroup().location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    capabilities: [
      {
        name: 'EnableServerless'
      }
    ]
    enableFreeTier: false
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: false
  }
}

resource sekibanDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2023-04-15' = {
  parent: cosmosAccount
  name: 'SekibanDb'
  properties: {
    resource: {
      id: 'SekibanDb'
    }
  }
}

resource sekibanEventsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  parent: sekibanDatabase
  name: 'events'
  properties: {
    resource: {
      id: 'events'
      partitionKey: {
        paths: [
          '/rootPartitionKey'
          '/aggregateGroup'
          '/partitionKey'
        ]
        kind: 'MultiHash'
        version: 2
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
      }
    }
  }
}

resource orleansDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2023-04-15' = {
  parent: cosmosAccount
  name: 'Orleans'
  properties: {
    resource: {
      id: 'Orleans'
    }
  }
}

resource orleansStorageContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  parent: orleansDatabase
  name: 'OrleansStorage'
  properties: {
    resource: {
      id: 'OrleansStorage'
      partitionKey: {
        paths: [
          '/PartitionKey'
        ]
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
      }
    }
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: resourceGroup().location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

var cosmosConnectionString = cosmosAccount.listConnectionStrings().connectionStrings[0].connectionString
var storageAccountKey = storageAccount.listKeys().keys[0].value
var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccountKey};EndpointSuffix=${environment().suffixes.storage}'

resource signalR 'Microsoft.SignalRService/signalR@2022-08-01-preview' = {
  name: signalRName
  location: resourceGroup().location
  sku: {
    name: 'Free_F1'
    tier: 'Free'
    capacity: 1
  }
  kind: 'SignalR'
  properties: {
    cors: {
      allowedOrigins: [
        '*'
      ]
    }
    features: [
      {
        flag: 'ServiceMode'
        value: 'Default'
      }
    ]
  }
}

var signalRConnectionString = signalR.listKeys().primaryConnectionString

resource containerEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: containerEnvName
  location: resourceGroup().location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
}

var acrCredentials = acr.listCredentials()
var acrUsername = acrCredentials.username
var acrPassword = acrCredentials.passwords[0].value

resource backendApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: backendAppName
  location: resourceGroup().location
  properties: {
    managedEnvironmentId: containerEnv.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: false
        targetPort: 8080
        transport: 'http'
        allowInsecure: false
        traffic: [
          {
            weight: 100
            latestRevision: true
          }
        ]
      }
      registries: [
        {
          server: acr.properties.loginServer
          username: acrUsername
          passwordSecretRef: 'acr-pwd'
        }
      ]
      secrets: [
        {
          name: 'acr-pwd'
          value: acrPassword
        }
        {
          name: 'cosmos-connection-string'
          value: cosmosConnectionString
        }
        {
          name: 'storage-connection-string'
          value: storageConnectionString
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'apiservice'
          image: backendImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: appInsights.properties.ConnectionString
            }
            {
              name: 'OTEL_SERVICE_NAME'
              value: 'EsCQRSQuestions.ApiService'
            }
            {
              name: 'Azure__SignalR__ConnectionString'
              value: signalRConnectionString
            }
            {
              name: 'Sekiban__Database'
              value: 'cosmos'
            }
            {
              name: 'ORLEANS_CLUSTERING_TYPE'
              value: 'cosmos'
            }
            {
              name: 'ORLEANS_GRAIN_DEFAULT_TYPE'
              value: 'cosmos'
            }
            {
              name: 'ConnectionStrings__SekibanDcbCosmos'
              secretRef: 'cosmos-connection-string'
            }
            {
              name: 'ConnectionStrings__OrleansCosmos'
              secretRef: 'cosmos-connection-string'
            }
            {
              name: 'ConnectionStrings__OrleansPubSubGrainState'
              secretRef: 'storage-connection-string'
            }
            {
              name: 'ConnectionStrings__OrleansSekibanClustering'
              secretRef: 'storage-connection-string'
            }
            {
              name: 'ConnectionStrings__OrleansSekibanGrainState'
              secretRef: 'storage-connection-string'
            }
            {
              name: 'ConnectionStrings__OrleansSekibanQueue'
              secretRef: 'storage-connection-string'
            }
            {
              name: 'ConnectionStrings__OrleansSekibanTable'
              secretRef: 'storage-connection-string'
            }
          ]
        }
      ]
      scale: {
        minReplicas: backendMinReplicas
        maxReplicas: backendMaxReplicas
      }
    }
  }
}

resource frontendApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: frontendAppName
  location: resourceGroup().location
  properties: {
    managedEnvironmentId: containerEnv.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
        allowInsecure: false
        traffic: [
          {
            weight: 100
            latestRevision: true
          }
        ]
      }
      registries: [
        {
          server: acr.properties.loginServer
          username: acrUsername
          passwordSecretRef: 'acr-pwd'
        }
      ]
      secrets: [
        {
          name: 'acr-pwd'
          value: acrPassword
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'webfrontend'
          image: frontendImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'services__apiservice__http__0'
              value: 'http://${backendAppName}'
            }
            {
              name: 'services__apiservice__https__0'
              value: 'http://${backendAppName}'
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: appInsights.properties.ConnectionString
            }
            {
              name: 'OTEL_SERVICE_NAME'
              value: 'EsCQRSQuestions.Web'
            }
          ]
        }
      ]
      scale: {
        minReplicas: frontendMinReplicas
        maxReplicas: frontendMaxReplicas
      }
    }
  }
  dependsOn: [
    backendApp
  ]
}

resource adminwebApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: adminwebAppName
  location: resourceGroup().location
  properties: {
    managedEnvironmentId: containerEnv.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
        allowInsecure: false
        traffic: [
          {
            weight: 100
            latestRevision: true
          }
        ]
      }
      registries: [
        {
          server: acr.properties.loginServer
          username: acrUsername
          passwordSecretRef: 'acr-pwd'
        }
      ]
      secrets: concat(
        [
          {
            name: 'acr-pwd'
            value: acrPassword
          }
        ],
        enableAdminEasyAuth && !empty(adminEasyAuthClientSecret)
          ? [
              {
                name: 'microsoft-provider-authentication-secret'
                value: adminEasyAuthClientSecret
              }
            ]
          : []
      )
    }
    template: {
      containers: [
        {
          name: 'adminweb'
          image: adminwebImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'services__apiservice__http__0'
              value: 'http://${backendAppName}'
            }
            {
              name: 'services__apiservice__https__0'
              value: 'http://${backendAppName}'
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: appInsights.properties.ConnectionString
            }
            {
              name: 'OTEL_SERVICE_NAME'
              value: 'EsCQRSQuestions.AdminWeb'
            }
          ]
        }
      ]
      scale: {
        minReplicas: adminwebMinReplicas
        maxReplicas: adminwebMaxReplicas
      }
    }
  }
  dependsOn: [
    backendApp
    frontendApp
  ]
}

var adminOpenIdIssuer = 'https://login.microsoftonline.com/${adminEasyAuthTenantId}/v2.0'

resource adminwebAuthConfig 'Microsoft.App/containerApps/authConfigs@2023-05-01' = if (enableAdminEasyAuth && !empty(adminEasyAuthTenantId) && !empty(adminEasyAuthClientId)) {
  parent: adminwebApp
  name: 'current'
  properties: {
    platform: {
      enabled: true
    }
    globalValidation: {
      unauthenticatedClientAction: 'RedirectToLoginPage'
      redirectToProvider: 'azureactivedirectory'
    }
    identityProviders: {
      azureActiveDirectory: {
        enabled: true
        registration: {
          clientId: adminEasyAuthClientId
          clientSecretSettingName: 'microsoft-provider-authentication-secret'
          openIdIssuer: adminOpenIdIssuer
        }
        login: {
          loginParameters: [
            'scope=openid profile email'
          ]
        }
      }
    }
  }
}

output acrName string = acr.name
output acrLoginServer string = acr.properties.loginServer
output managedEnvironmentName string = containerEnv.name
output applicationInsightsName string = appInsights.name
output applicationInsightsConnectionString string = appInsights.properties.ConnectionString
output signalRName string = signalR.name
output cosmosAccountName string = cosmosAccount.name
output storageAccountName string = storageAccount.name
output backendAppName string = backendApp.name
output frontendAppName string = frontendApp.name
output adminwebAppName string = adminwebApp.name
output backendFqdn string = backendApp.properties.configuration.ingress.fqdn
output frontendFqdn string = frontendApp.properties.configuration.ingress.fqdn
output adminwebFqdn string = adminwebApp.properties.configuration.ingress.fqdn
output adminEasyAuthConfigured bool = enableAdminEasyAuth && !empty(adminEasyAuthTenantId) && !empty(adminEasyAuthClientId)
