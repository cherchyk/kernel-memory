// Reused template from https://learn.microsoft.com/azure/postgresql/flexible-server/quickstart-create-server-bicep?tabs=CLI

param location string = resourceGroup().location
var rgName = resourceGroup().name

@description('Server Name for Azure Database for PostgreSQL')
param serverName string

@description('Database administrator login name')
@minLength(1)
param adminLogin string

@description('Database administrator password')
@minLength(8)
@secure()
param adminPassword string

param suffix string = uniqueString(resourceGroup().id)

param managedIdentityResource object
param managedIdentityTenantId string
param managedIdentityPrincipalName string
param managedIdentityPrincipalId string
param managedIdentityId string
param managedIdentityClientId string

param serverEdition string = 'GeneralPurpose'
param skuSizeGB int = 128
param dbInstanceType string = 'Standard_D4ds_v4'

param databaseName string

@allowed([
  'Disabled'
  'SameZone'
  'ZoneRedundant'
])
param haMode string = 'Disabled'
param availabilityZone string = '1'
param version string = '15'
param virtualNetworkExternalId string = ''
param subnetName string = ''
param privateDnsZoneArmResourceId string = ''

resource postgresServer 'Microsoft.DBforPostgreSQL/flexibleServers@2023-03-01-preview' = {
  name: serverName
  location: location
  sku: {
    name: dbInstanceType
    tier: serverEdition
  }
  properties: {
    version: version
    storage: {
      storageSizeGB: skuSizeGB
    }
    highAvailability: {
      mode: haMode
    }

    administratorLogin: adminLogin
    administratorLoginPassword: adminPassword
    authConfig: {
      activeDirectoryAuth: 'Enabled'
      passwordAuth: 'Enabled'
    }
  }

  resource database 'databases' = {
    name: databaseName
  }

  resource administrator 'administrators' = {
    dependsOn: [
      database
    ]
    name: managedIdentityPrincipalId
    properties: {
      principalType: 'ServicePrincipal'
      principalName: managedIdentityPrincipalName
      tenantId: managedIdentityTenantId
    }
  }

  resource serverFirewallRule 'firewallRules' = {
    dependsOn: [
      administrator
    ]
    name: 'allow-all-azure-internal-IPs'
    properties: {
      startIpAddress: '0.0.0.0'
      endIpAddress: '0.0.0.0'
    }
  }

  resource Extension 'configurations' = {
    dependsOn: [
      serverFirewallRule
    ]
    name: 'azure.extensions'
    properties: {
      value: 'vector'
      source: 'user-override'
    }
  }
}

// Microsoft.Resources/deploymentScripts that will call PostgresSQL to execute the SQL script: 'CREATE EXTENSION vector;'
// https://learn.microsoft.com/en-us/cli/azure/postgres/flexible-server?view=azure-cli-latest#az-postgres-flexible-server-execute

@description('The SQL script to execute.')
param sqlScript string = 'create extension vector'

resource deploymentScript 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: 'executePostgreSqlScript'
  location: location
  kind: 'AzureCLI'
  identity: {
    type: 'userAssigned'
    userAssignedIdentities: {
      '${managedIdentityId}': {}
    }
  }
  dependsOn: [
    postgresServer
  ]
  properties: {
    azCliVersion: '2.42.0'
    scriptContent: '''
      az extension add -n rdbms-connect
      az postgres flexible-server execute --name "$serverName" --admin-user "$adminLogin" --admin-password "$adminPassword" --database-name "$databaseName" --querytext "$sqlScript"
    '''
    environmentVariables: [
      {
        name: 'adminLogin'
        value: adminLogin
      }
      {
        name: 'adminPassword'
        value: adminPassword
      }
      {
        name: 'sqlScript'
        value: sqlScript
      }
      {
        name: 'serverName'
        value: serverName
      }
      {
        name: 'databaseName'
        value: databaseName
      }
    ]
    timeout: 'PT30M'
    cleanupPreference: 'OnSuccess'
    retentionInterval: 'P1D'
  }
}

output PostgreSQLHost string = postgresServer.properties.fullyQualifiedDomainName
