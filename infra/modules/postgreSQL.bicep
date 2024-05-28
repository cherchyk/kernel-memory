// Reused template from https://learn.microsoft.com/azure/postgresql/flexible-server/quickstart-create-server-bicep?tabs=CLI

param location string = resourceGroup().location

@description('Server Name for Azure Database for PostgreSQL')
param serverName string

@description('Database administrator login name')
@minLength(1)
param administratorLogin string

@description('Database administrator password')
@minLength(8)
@secure()
param administratorLoginPassword string

param managedIdentityTenantId string
param managedIdentityPrincipalName string
param managedIdentityPrincipalId string

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

    administratorLogin: administratorLogin
    administratorLoginPassword: administratorLoginPassword
    authConfig: {
      activeDirectoryAuth: 'Enabled'
      passwordAuth: 'Enabled'
    }

    // network: {
    //   publicNetworkAccess: 'Enabled'
    //   delegatedSubnetResourceId: (empty(virtualNetworkExternalId)
    //     ? json('null')
    //     : json('\'${virtualNetworkExternalId}/subnets/${subnetName}\''))
    //   privateDnsZoneArmResourceId: (empty(virtualNetworkExternalId) ? json('null') : privateDnsZoneArmResourceId)
    // }
    // backup: {
    //   backupRetentionDays: 7
    //   geoRedundantBackup: 'Disabled'
    // }
    // availabilityZone: availabilityZone
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

// resource PostgreSQLExtention 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2022-12-01' = {
//   name: 'azure.extensions'
//   parent: postgresServer
//   properties: {
//     value: 'vector'
//     source: 'user-override'
//   }
//   dependsOn: [
//     postgresServer
//   ]
// }

// Microsoft.Resources/deploymentScripts that will call PostgresSQL to execute the SQL script: 'CREATE EXTENSION vector;'
// https://learn.microsoft.com/en-us/cli/azure/postgres/flexible-server?view=azure-cli-latest#az-postgres-flexible-server-execute
resource deploymentScriptCreateExtension 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: 'createExtension'
  location: location
  kind: 'AzureCLI'
  dependsOn: [
    postgresServer
  ]
  properties: {
    azCliVersion: '2.59.0'
    scriptContent: '''
      az extension add -n rdbms-connect
      az postgres flexible-server execute --admin-password '$POSTGRES_PASSWORD' --admin-user '$POSTGRES_USER' --allow-preview true --name '$serverName' --database-name '$dbName'  --querytext 'create extension vector'
    '''
    cleanupPreference: 'OnSuccess'
    arguments: ''
    environmentVariables: [
      {
        name: 'POSTGRES_USER'
        value: administratorLogin
      }
      {
        name: 'POSTGRES_PASSWORD'
        value: administratorLoginPassword
      }
      {
        name: 'POSTGRES_HOST'
        value: postgresServer.properties.fullyQualifiedDomainName
      }
      {
        name: 'serverName'
        value: serverName
      }
      {
        name: 'dbName'
        value: databaseName
      }
    ]
    forceUpdateTag: 'Rerun'
    retentionInterval: 'P1D'
  }
}

output PostgreSQLHost string = postgresServer.properties.fullyQualifiedDomainName
