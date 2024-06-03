// Copyright (c) Microsoft. All rights reserved.

using Microsoft.KernelMemory;

namespace Postgres.UnitTests;

public class PostgresConfigTests
{
    [Fact]
    [Trait("Category", "UnitTest")]
    [Trait("Category", "Postgres")]
    public void ItRequiresOnlyAConnStringToBeValid()
    {
        // Arrange
        var config1 = new PostgresConfig();
        var config2 = new PostgresConfig { ConnectionString = "test string" };

        // Act - Assert exception occurs
        Assert.Throws<ConfigurationException>(() => config1.Validate());

        // Act - Assert no exception occurs
        config2.Validate();
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    [Trait("Category", "Postgres")]
    public void ItDoesntUseConnStringWithAzureIdentity()
    {
        // Arrange
        var config1 = new PostgresConfig() { ConnectionString = "test string", Auth = PostgresConfig.AuthTypes.AzureIdentity };

        // Act - Assert exception occurs
        Assert.Throws<ConfigurationException>(() => config1.Validate());
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    [Trait("Category", "Postgres")]
    public void ItRequiresHostPortAndUsernameToBeValidWithAzureIdentity()
    {
        // Arrange
        var config1 = new PostgresConfig() { Auth = PostgresConfig.AuthTypes.AzureIdentity };

        // Act - Assert exception occurs
        Assert.Throws<ConfigurationException>(() => config1.Validate());
    }


    [Fact]
    [Trait("Category", "UnitTest")]
    [Trait("Category", "Postgres")]
    public void ItRequiresHostToBeValidWithAzureIdentity()
    {
        // Arrange
        var config1 = new PostgresConfig() { Port = 1, UserName = "user", Auth = PostgresConfig.AuthTypes.AzureIdentity };

        // Act - Assert exception occurs
        Assert.Throws<ConfigurationException>(() => config1.Validate());
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    [Trait("Category", "Postgres")]
    public void ItRequiresPortToBeValidWithAzureIdentity()
    {
        // Arrange
        var config1 = new PostgresConfig() { Host = "host", UserName = "user", Auth = PostgresConfig.AuthTypes.AzureIdentity };

        // Act - Assert exception occurs
        Assert.Throws<ConfigurationException>(() => config1.Validate());
    }


    [Fact]
    [Trait("Category", "UnitTest")]
    [Trait("Category", "Postgres")]
    public void ItRequiresUserNameToBeValidWithAzureIdentity()
    {
        // Arrange
        var config1 = new PostgresConfig() { Host = "host", Port = 1, Auth = PostgresConfig.AuthTypes.AzureIdentity };

        // Act - Assert exception occurs
        Assert.Throws<ConfigurationException>(() => config1.Validate());
    }

}
