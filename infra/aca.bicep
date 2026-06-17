// ---------------------------------------------------------------------------
// ScrambleCoin — Azure Container Apps (ACA) stack (issue #132)
//
// Provisions: Azure Container Registry, a Log Analytics workspace, a Container
// Apps managed environment, and five Container Apps (web, api, grafana, loki,
// prometheus). Azure SQL is provisioned separately by infra/main.bicep and is
// reused here via the sqlServerFqdn parameter.
//
// This file is intentionally separate from main.bicep (App Service stack) and
// does NOT modify it. Validate with:  az bicep build --file infra/aca.bicep
// ---------------------------------------------------------------------------

@description('Base name for all resources.')
param appName string = 'scramblecoin'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Azure SQL administrator login (matches main.bicep).')
param sqlAdminLogin string

@description('Azure SQL administrator password (matches main.bicep).')
@secure()
param sqlAdminPassword string

@description('Fully qualified domain name of the Azure SQL Server provisioned by main.bicep, e.g. sql-scramblecoin.database.windows.net')
param sqlServerFqdn string

@description('Grafana admin password (set via ACA secret, never baked into the image).')
@secure()
param grafanaAdminPassword string

@description('Container image tag to deploy (typically the git SHA). Defaults to latest.')
param imageTag string = 'latest'

@description('Azure Container Registry name. ACR names are globally unique, alphanumeric only, 5-50 chars. Override if the derived default is taken.')
param acrName string = 'acr${appName}'

@description('Optional Application Insights connection string for the API (leave empty to disable).')
param appInsightsConnectionString string = ''

// ---------------------------------------------------------------------------
// Derived values
// ---------------------------------------------------------------------------
var sqlDbName = 'sqldb-${appName}'
var sqlConnectionString = 'Server=tcp:${sqlServerFqdn},1433;Initial Catalog=${sqlDbName};Persist Security Info=False;User ID=${sqlAdminLogin};Password=${sqlAdminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
var acrLoginServer = acr.properties.loginServer

// Shared registry block (ACR admin credentials) for image pull.
var registries = [
  {
    server: acrLoginServer
    username: acr.listCredentials().username
    passwordSecretRef: 'acr-password'
  }
]
var acrPasswordSecret = {
  name: 'acr-password'
  value: acr.listCredentials().passwords[0].value
}

// ---------------------------------------------------------------------------
// Azure Container Registry (admin-enabled — simplest auth for the event)
// ---------------------------------------------------------------------------
resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
  }
}

// ---------------------------------------------------------------------------
// Log Analytics workspace for the Container Apps environment
// ---------------------------------------------------------------------------
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: 'log-${appName}-aca'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// ---------------------------------------------------------------------------
// Container Apps managed environment
// ---------------------------------------------------------------------------
resource managedEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-${appName}'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// ---------------------------------------------------------------------------
// Loki — internal ingress (observability log store, ephemeral, single replica)
// ---------------------------------------------------------------------------
resource loki 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'loki'
  location: location
  properties: {
    managedEnvironmentId: managedEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: false
        targetPort: 3100
        transport: 'http'
      }
      registries: registries
      secrets: [
        acrPasswordSecret
      ]
    }
    template: {
      containers: [
        {
          name: 'loki'
          image: '${acrLoginServer}/loki:${imageTag}'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

// ---------------------------------------------------------------------------
// Prometheus — internal ingress (metrics store, ephemeral, single replica)
// ---------------------------------------------------------------------------
resource prometheus 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'prometheus'
  location: location
  properties: {
    managedEnvironmentId: managedEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: false
        targetPort: 9090
        transport: 'http'
      }
      registries: registries
      secrets: [
        acrPasswordSecret
      ]
    }
    template: {
      containers: [
        {
          name: 'prometheus'
          image: '${acrLoginServer}/prometheus:${imageTag}'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

// ---------------------------------------------------------------------------
// Grafana — external ingress (dashboards)
// ---------------------------------------------------------------------------
resource grafana 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'grafana'
  location: location
  properties: {
    managedEnvironmentId: managedEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 3000
        transport: 'http'
      }
      registries: registries
      secrets: [
        acrPasswordSecret
        {
          name: 'grafana-admin-password'
          value: grafanaAdminPassword
        }
        {
          name: 'azure-sql-password'
          value: sqlAdminPassword
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'grafana'
          image: '${acrLoginServer}/grafana:${imageTag}'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'GF_SECURITY_ADMIN_PASSWORD'
              secretRef: 'grafana-admin-password'
            }
            {
              name: 'LOKI_URL'
              value: 'http://loki:3100'
            }
            {
              name: 'PROMETHEUS_URL'
              value: 'http://prometheus:9090'
            }
            {
              name: 'AZURE_SQL_HOST'
              value: '${sqlServerFqdn}:1433'
            }
            {
              name: 'AZURE_SQL_DB'
              value: sqlDbName
            }
            {
              name: 'AZURE_SQL_USER'
              value: sqlAdminLogin
            }
            {
              name: 'AZURE_SQL_PASSWORD'
              secretRef: 'azure-sql-password'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

// ---------------------------------------------------------------------------
// ScrambleCoin API — external ingress (bot REST API + Prometheus /metrics)
//
// ASPNETCORE_ENVIRONMENT is set to 'Docker' (not 'Production') because
// Program.cs only maps the Prometheus /metrics endpoint when NOT Production.
// Using 'Docker' keeps /metrics exposed so Prometheus can scrape it (matching
// docker-compose.yml's api service).
// ---------------------------------------------------------------------------
resource api 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'scramblecoin-api'
  location: location
  properties: {
    managedEnvironmentId: managedEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 5001
        transport: 'http'
      }
      registries: registries
      secrets: [
        acrPasswordSecret
        {
          name: 'sql-connection-string'
          value: sqlConnectionString
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'scramblecoin-api'
          image: '${acrLoginServer}/scramblecoin-api:${imageTag}'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:5001'
            }
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Docker'
            }
            {
              name: 'ConnectionStrings__DefaultConnection'
              secretRef: 'sql-connection-string'
            }
            {
              name: 'Loki__Url'
              value: 'http://loki:3100'
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: appInsightsConnectionString
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

// ---------------------------------------------------------------------------
// ScrambleCoin Web — external ingress (Blazor Server)
// ---------------------------------------------------------------------------
resource web 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'scramblecoin-web'
  location: location
  properties: {
    managedEnvironmentId: managedEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
      }
      registries: registries
      secrets: [
        acrPasswordSecret
        {
          name: 'sql-connection-string'
          value: sqlConnectionString
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'scramblecoin-web'
          image: '${acrLoginServer}/scramblecoin-web:${imageTag}'
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
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ConnectionStrings__DefaultConnection'
              secretRef: 'sql-connection-string'
            }
            {
              name: 'Loki__Url'
              value: 'http://loki:3100'
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

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------
@description('ACR login server (e.g. acrscramblecoin.azurecr.io).')
output acrLoginServer string = acrLoginServer

@description('Public HTTPS URL of the Blazor Web app.')
output webUrl string = 'https://${web.properties.configuration.ingress.fqdn}'

@description('Public HTTPS URL of the bot REST API.')
output apiUrl string = 'https://${api.properties.configuration.ingress.fqdn}'

@description('Public HTTPS URL of Grafana.')
output grafanaUrl string = 'https://${grafana.properties.configuration.ingress.fqdn}'
