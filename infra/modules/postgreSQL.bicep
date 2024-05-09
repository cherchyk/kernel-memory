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

@allowed([
  'Disabled'
  'SameZone'
  'ZoneRedundant'
])
param haMode string = 'Disabled'
param availabilityZone string = '1'
param version string = '12'
param virtualNetworkExternalId string = ''
param subnetName string = ''
param privateDnsZoneArmResourceId string = ''

resource serverName_resource 'Microsoft.DBforPostgreSQL/flexibleServers@2023-06-01-preview' = {
  name: serverName
  location: location
  sku: {
    name: dbInstanceType
    tier: serverEdition
  }
  properties: {
    version: version
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorLoginPassword
    network: {
      publicNetworkAccess: 'Enabled'
      delegatedSubnetResourceId: (empty(virtualNetworkExternalId)
        ? json('null')
        : json('\'${virtualNetworkExternalId}/subnets/${subnetName}\''))
      privateDnsZoneArmResourceId: (empty(virtualNetworkExternalId) ? json('null') : privateDnsZoneArmResourceId)
    }
    highAvailability: {
      mode: haMode
    }
    storage: {
      storageSizeGB: skuSizeGB
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    availabilityZone: availabilityZone
    authConfig: {
      activeDirectoryAuth: 'Enabled'
      passwordAuth: 'Enabled'
    }
  }

  resource administrator 'administrators' = {
    name: managedIdentityPrincipalId
    properties: {
      principalType: 'ServicePrincipal'
      principalName: managedIdentityPrincipalName
      tenantId: managedIdentityTenantId
    }
  }

  resource serverFirewallRule 'firewallRules' = {
    name: 'AllowAllWindowsAzureIps'
    properties: {
      startIpAddress: '0.0.0.0'
      endIpAddress: '0.0.0.0'
    }
  }
}

output PostgreSQLHost string = serverName_resource.properties.fullyQualifiedDomainName
