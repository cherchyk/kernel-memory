// Copyright (c) Microsoft. All rights reserved.
using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using Azure.Identity;
using Microsoft.KernelMemory.Postgres;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.Diagnostics;

#pragma warning disable IDE0130 // reduce number of "using" statements
// ReSharper disable once CheckNamespace - reduce number of "using" statements
namespace Microsoft.KernelMemory;

/// <summary>
/// Postgres configuration
/// </summary>
public class PostgresConfig
{

    private readonly ILogger<PostgresConfig> _log;

    /// <summary>
    /// Key for the Columns dictionary
    /// </summary>
    public const string ColumnId = "id";

    /// <summary>
    /// Key for the Columns dictionary
    /// </summary>
    public const string ColumnEmbedding = "embedding";

    /// <summary>
    /// Key for the Columns dictionary
    /// </summary>
    public const string ColumnTags = "tags";

    /// <summary>
    /// Key for the Columns dictionary
    /// </summary>
    public const string ColumnContent = "content";

    /// <summary>
    /// Key for the Columns dictionary
    /// </summary>
    public const string ColumnPayload = "payload";

    /// <summary>
    /// Name of the default database
    /// </summary>
    public const string DefaultDatabase = "postgres";

    /// <summary>
    /// Name of the default schema
    /// </summary>
    public const string DefaultSchema = "public";

    /// <summary>
    /// Default prefix used for table names
    /// </summary>
    public const string DefaultTableNamePrefix = "km-";

    /// <summary>
    /// Authentication types for connecting to Postgres.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AuthTypes
    {
        // /// <summary>
        // /// Unknown authentication type.
        // /// </summary>
        // Unknown = -1,

        /// <summary>
        /// Managed Identity authentication type.
        /// </summary>
        AzureIdentity,

        /// <summary>
        /// ConnectionString based authentication type.
        /// </summary>
        ConnectionString
    }

    /// <summary>
    /// Authentication type for connecting to Postgres. Default is ConnectionString.
    /// </summary>
    public AuthTypes Auth { get; set; } = AuthTypes.ConnectionString;


    /// <summary>
    /// Connection string to connect to Postgres
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Host to connect to Postgres. Used with AuthTypes=AzureIdentity. Reduntant with AuthTypes=ConnectionString
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Port to connect to Postgres. Used with AuthTypes=AzureIdentity. Reduntant with AuthTypes=ConnectionString
    /// </summary>
    public int Port { get; set; } = 0;

    /// <summary>
    /// UserName to connect to Postgres. Used with AuthTypes=AzureIdentity. Reduntant with AuthTypes=ConnectionString
    /// </summary>
    public string UserName { get; set; } = string.Empty;


    /// <summary>
    /// Name of the database where to read and write records.
    /// </summary>
    public string Database { get; set; } = DefaultDatabase;


    /// <summary>
    /// Name of the schema where to read and write records.
    /// </summary>
    public string Schema { get; set; } = DefaultSchema;

    /// <summary>
    /// Mandatory prefix to add to tables created by KM.
    /// This is used to distinguish KM tables from others in the same schema.
    /// </summary>
    /// <remarks>Default value is set to "km-" but can be override when creating a config.</remarks>
    public string TableNamePrefix { get; set; } = DefaultTableNamePrefix;

    /// <summary>
    /// Configurable column names used with Postgres
    /// </summary>
    public Dictionary<string, string> Columns { get; set; }

    /// <summary>
    /// Mandatory placeholder required in CreateTableSql
    /// </summary>
    public const string SqlPlaceholdersTableName = "%%table_name%%";

    /// <summary>
    /// Mandatory placeholder required in CreateTableSql
    /// </summary>
    public const string SqlPlaceholdersVectorSize = "%%vector_size%%";

    /// <summary>
    /// Optional placeholder required in CreateTableSql
    /// </summary>
    public const string SqlPlaceholdersLockId = "%%lock_id%%";

    /// <summary>
    /// Optional, custom SQL statements for creating new tables, in case
    /// you need to add custom columns, indexing, etc.
    /// The SQL must contain two placeholders: %%table_name%% and %%vector_size%%.
    /// You can put the SQL in one line or split it over multiple lines for
    /// readability. Lines are automatically merged with a new line char.
    /// Example:
    ///   BEGIN;
    ///   "SELECT pg_advisory_xact_lock(%%lock_id%%);
    ///   CREATE TABLE IF NOT EXISTS %%table_name%% (
    ///     id           TEXT NOT NULL PRIMARY KEY,
    ///     embedding    vector(%%vector_size%%),
    ///     tags         TEXT[] DEFAULT '{}'::TEXT[] NOT NULL,
    ///     content      TEXT DEFAULT '' NOT NULL,
    ///     payload      JSONB DEFAULT '{}'::JSONB NOT NULL,
    ///     some_text    TEXT DEFAULT '',
    ///     last_update  TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
    ///   );
    ///   CREATE INDEX IF NOT EXISTS idx_tags ON %%table_name%% USING GIN(tags);
    ///   COMMIT;
    /// </summary>
    public List<string> CreateTableSql { get; set; } = new();

    private static readonly string[] s_tokenRequestScopes = new string[] { "https://ossrdbms-aad.database.windows.net/.default" };

    /// <summary>
    /// Create a new instance of the configuration
    /// </summary>
    public PostgresConfig()
    {
        this._log = DefaultLogger<PostgresConfig>.Instance;

        this.Columns = new Dictionary<string, string>
        {
            [ColumnId] = "id",
            [ColumnEmbedding] = "embedding",
            [ColumnTags] = "tags",
            [ColumnContent] = "content",
            [ColumnPayload] = "payload"
        };
    }

    private string BuildConnectinStringUsingManagedIdentity()
    {
        // https://learn.microsoft.com/azure/postgresql/flexible-server/how-to-connect-with-managed-identity

        this._log.LogCritical("Entering BuildConnectinStringUsingManagedIdentityAuthType.");
        try
        {
            // Call managed identities for Azure resources endpoint.
            var tokenProvider = new DefaultAzureCredential();
            string accessToken = (tokenProvider.GetToken(
                new Azure.Core.TokenRequestContext(scopes: s_tokenRequestScopes) { })).Token;


            string connString =
        $"Server={this.Host}; User Id={this.UserName}; Database={this.Database}; Port={this.Port}; Password={accessToken}; SSLMode=Prefer";

            this._log.LogCritical($"connString is {connString}");

            return connString;
        }
        catch (Exception e)
        {
            throw new PostgresException($"{e.Message} \n\n{(e.InnerException != null ? e.InnerException.Message : "Acquire token failed")}");
        }
    }

    /// <summary>
    /// Gets the connection string based on the authentication type.
    /// </summary>
    public string GetConnectionStringByAuth()
    {
        this._log.LogCritical("Postgres: AuthType is {Auth}.");

        if (this.Auth == AuthTypes.ConnectionString)
        {
            return this.ConnectionString;
        }

        if (this.Auth == AuthTypes.AzureIdentity)
        {
            this._log.LogCritical("Postgres: AuthType is {Auth}.");
            return this.BuildConnectinStringUsingManagedIdentity();
        }

        throw new ConfigurationException("Postgres: unknown authentication type.");
    }




    /// <summary>
    /// Verify that the current state is valid.
    /// </summary>
    public void Validate()
    {
        // ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
        this.TableNamePrefix = this.TableNamePrefix?.Trim() ?? string.Empty;
        this.ConnectionString = this.ConnectionString?.Trim() ?? string.Empty;

        if (this.Auth == AuthTypes.ConnectionString)
        {
            if (string.IsNullOrWhiteSpace(this.ConnectionString))
            {
                throw new ConfigurationException($"Postgres: {nameof(this.ConnectionString)} is empty.");
            }

            if (!string.IsNullOrWhiteSpace(this.Host))
            {
                throw new ConfigurationException($"Postgres: {nameof(this.Host)} should not be used when {nameof(this.Auth)} is 'ConnectionString'.");
            }
            if (this.Port > 0)
            {
                throw new ConfigurationException($"Postgres: {nameof(this.Port)} should not be used when {nameof(this.Auth)} is 'ConnectionString'.");
            }
            if (!string.IsNullOrWhiteSpace(this.UserName))
            {
                throw new ConfigurationException($"Postgres: {nameof(this.UserName)} should not be used when {nameof(this.Auth)} is 'ConnectionString'.");
            }

        }

        if (this.Auth == AuthTypes.AzureIdentity)
        {
            if (!string.IsNullOrWhiteSpace(this.ConnectionString))
            {
                throw new ConfigurationException($"Postgres: {nameof(this.ConnectionString)} should not be used when {nameof(this.Auth)} is 'AzureIdentity'.");
            }

            if (string.IsNullOrWhiteSpace(this.Host))
            {
                throw new ConfigurationException($"Postgres: {nameof(this.Host)} is empty.");
            }
            if (this.Port < 1)
            {
                throw new ConfigurationException($"Postgres: {nameof(this.Port)} is empty.");
            }
            if (string.IsNullOrWhiteSpace(this.UserName))
            {
                throw new ConfigurationException($"Postgres: {nameof(this.UserName)} is empty.");
            }
        }

        if (string.IsNullOrWhiteSpace(this.TableNamePrefix))
        {
            throw new ConfigurationException($"Postgres: {nameof(this.TableNamePrefix)} is empty.");
        }

        // ID

        if (!this.Columns.TryGetValue(ColumnId, out var columnName))
        {
            throw new ConfigurationException("Postgres: the name of the Id column is not defined.");
        }

        if (string.IsNullOrWhiteSpace(columnName))
        {
            throw new ConfigurationException("Postgres: the name of the Id column is empty.");
        }

        // Embedding

        if (!this.Columns.TryGetValue(ColumnEmbedding, out columnName))
        {
            throw new ConfigurationException("Postgres: the name of the Embedding column is not defined.");
        }

        if (string.IsNullOrWhiteSpace(columnName))
        {
            throw new ConfigurationException("Postgres: the name of the Embedding column is empty.");
        }

        // Tags

        if (!this.Columns.TryGetValue(ColumnTags, out columnName))
        {
            throw new ConfigurationException("Postgres: the name of the Tags column is not defined.");
        }

        if (string.IsNullOrWhiteSpace(columnName))
        {
            throw new ConfigurationException("Postgres: the name of the Tags column is empty.");
        }

        // Content

        if (!this.Columns.TryGetValue(ColumnContent, out columnName))
        {
            throw new ConfigurationException("Postgres: the name of the Content column is not defined.");
        }

        if (string.IsNullOrWhiteSpace(columnName))
        {
            throw new ConfigurationException("Postgres: the name of the Content column is empty.");
        }

        // Payload

        if (!this.Columns.TryGetValue(ColumnPayload, out columnName))
        {
            throw new ConfigurationException("Postgres: the name of the Payload column is not defined.");
        }

        if (string.IsNullOrWhiteSpace(columnName))
        {
            throw new ConfigurationException("Postgres: the name of the Payload column is empty.");
        }

        // Custom schema

        if (this.CreateTableSql?.Count > 0)
        {
            var sql = string.Join('\n', this.CreateTableSql).Trim();
            if (!sql.Contains(SqlPlaceholdersTableName, StringComparison.Ordinal))
            {
                throw new ConfigurationException(
                    "Postgres: the custom SQL to create tables is not valid, " +
                    $"it should contain a {SqlPlaceholdersTableName} placeholder.");
            }

            if (!sql.Contains(SqlPlaceholdersVectorSize, StringComparison.Ordinal))
            {
                throw new ConfigurationException(
                    "Postgres: the custom SQL to create tables is not valid, " +
                    $"it should contain a {SqlPlaceholdersVectorSize} placeholder.");
            }
        }

        this.Columns[ColumnId] = this.Columns[ColumnId].Trim();
        this.Columns[ColumnEmbedding] = this.Columns[ColumnEmbedding].Trim();
        this.Columns[ColumnTags] = this.Columns[ColumnTags].Trim();
        this.Columns[ColumnContent] = this.Columns[ColumnContent].Trim();
        this.Columns[ColumnPayload] = this.Columns[ColumnPayload].Trim();
    }
}
