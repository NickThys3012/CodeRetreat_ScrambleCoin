// Scramblecoin Azure Infrastructure
// Provisions: Log Analytics Workspace, Application Insights, App Service Plan,
//             App Service (.NET 9), Azure SQL Server, Azure SQL Database

@description('Base name used to derive all resource names (e.g. "scramblecoin").')
@minLength(3)
@maxLength(24)
param appName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('SQL Server administrator login name.')
@minLength(1)
@maxLength(128)
param sqlAdminLogin string

@description('SQL Server administrator password.')
@minLength(8)
@secure()
param sqlAdminPassword string

// ---------------------------------------------------------------------------
// Derived names — prefix-first per Microsoft CAF naming convention
// https://learn.microsoft.com/en-us/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations
var planName       = 'asp-${appName}'
var webName        = 'app-${appName}'
var aiName         = 'appi-${appName}'
var lawName        = 'log-${appName}'
var sqlServerName  = 'sql-${appName}'
var sqlDbName      = 'sqldb-${appName}'

// ---------------------------------------------------------------------------
// Log Analytics Workspace (required by workspace-based Application Insights)
// ---------------------------------------------------------------------------
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: lawName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// ---------------------------------------------------------------------------
// Application Insights (workspace-based)
// ---------------------------------------------------------------------------
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: aiName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}

// ---------------------------------------------------------------------------
// App Service Plan (D1 Shared, Windows)
// D1 Shared is the cheapest non-free tier (~€8/mo). F1/B1 are blocked on
// this Visual Studio Professional subscription (quota = 0 for Free/Basic VMs).
// D1 uses the Shared VM pool which has separate quota.
// ---------------------------------------------------------------------------
resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: planName
  location: location
  sku: {
    name: 'D1'
    tier: 'Shared'
  }
  properties: {}
}

// ---------------------------------------------------------------------------
// App Service (.NET 9)
// ---------------------------------------------------------------------------
resource appService 'Microsoft.Web/sites@2022-09-01' = {
  name: webName
  location: location
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v9.0'
      alwaysOn: false
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'ConnectionStrings__DefaultConnection'
          value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDbName};Persist Security Info=False;User ID=${sqlAdminLogin};Password=${sqlAdminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
        }
      ]
    }
  }
}

// ---------------------------------------------------------------------------
// Azure SQL Server
// ---------------------------------------------------------------------------
resource sqlServer 'Microsoft.Sql/servers@2022-11-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
  }
}

// Firewall rule: allow Azure services to reach the SQL Server
resource sqlFirewallAllowAzure 'Microsoft.Sql/servers/firewallRules@2022-11-01-preview' = {
  parent: sqlServer
  name: 'AllowAllAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// ---------------------------------------------------------------------------
// Azure SQL Database (Basic, 5 DTUs)
// ---------------------------------------------------------------------------
resource sqlDatabase 'Microsoft.Sql/servers/databases@2022-11-01-preview' = {
  parent: sqlServer
  name: sqlDbName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------
@description('Default hostname of the deployed App Service.')
output appServiceUrl string = 'https://${appService.properties.defaultHostName}'

@description('Application Insights connection string.')
output appInsightsConnectionString string = appInsights.properties.ConnectionString

@description('Fully qualified domain name of the Azure SQL Server.')
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
