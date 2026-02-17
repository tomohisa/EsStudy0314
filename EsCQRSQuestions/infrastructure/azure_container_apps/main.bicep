targetScope = 'resourceGroup'

var suffix = substring(uniqueString(resourceGroup().id), 0, 6)
var acrName = 'acr${uniqueString(resourceGroup().id, 'acr')}'
var logAnalyticsName = 'law-${suffix}'
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
    retentionInDays: 30
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
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
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
          name: 'apiservice'
          image: '${acr.properties.loginServer}/escqrsquestions-backend:initial'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
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
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
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
          image: '${acr.properties.loginServer}/escqrsquestions-frontend:initial'
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
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
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
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
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
          name: 'adminweb'
          image: '${acr.properties.loginServer}/escqrsquestions-adminweb:initial'
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
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
      }
    }
  }
  dependsOn: [
    backendApp
    frontendApp
  ]
}

output acrName string = acr.name
output acrLoginServer string = acr.properties.loginServer
output managedEnvironmentName string = containerEnv.name
output backendAppName string = backendApp.name
output frontendAppName string = frontendApp.name
output adminwebAppName string = adminwebApp.name
output backendFqdn string = backendApp.properties.configuration.ingress.fqdn
output frontendFqdn string = frontendApp.properties.configuration.ingress.fqdn
output adminwebFqdn string = adminwebApp.properties.configuration.ingress.fqdn
