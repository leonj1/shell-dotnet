using Microsoft.Extensions.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace DotNetShell.Core.Configuration.Providers;

/// <summary>
/// Configuration provider that loads configuration from a database table.
/// </summary>
public class DatabaseConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly string _connectionString;
    private readonly string _tableName;
    private readonly TimeSpan? _refreshInterval;
    private readonly Timer? _refreshTimer;
    private bool _disposed;

    public DatabaseConfigurationProvider(
        string connectionString,
        string tableName = "Configuration",
        TimeSpan? refreshInterval = null)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        _refreshInterval = refreshInterval;

        // Set up periodic refresh if specified
        if (_refreshInterval.HasValue)
        {
            _refreshTimer = new Timer(RefreshConfiguration, null, TimeSpan.Zero, _refreshInterval.Value);
        }
    }

    public override void Load()
    {
        try
        {
            LoadFromDatabase();
        }
        catch (Exception ex)
        {
            // Log error but don't fail startup
            Console.WriteLine($"Warning: Failed to load from database configuration: {ex.Message}");
        }
    }

    private void LoadFromDatabase()
    {
        const string query = @"
            SELECT [Key], [Value], [Environment]
            FROM [{0}]
            WHERE ([Environment] = @environment OR [Environment] IS NULL OR [Environment] = '')
            ORDER BY [Environment] DESC"; // Environment-specific values override general ones

        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = new SqlCommand(string.Format(query, _tableName), connection);
            command.Parameters.AddWithValue("@environment", environmentName);

            using var reader = command.ExecuteReader();

            var newData = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            while (reader.Read())
            {
                var key = reader.GetString("Key");
                var value = reader.IsDBNull("Value") ? null : reader.GetString("Value");
                newData[key] = value;
            }

            Data.Clear();
            foreach (var kvp in newData)
            {
                Data[kvp.Key] = kvp.Value;
            }
        }
        catch (SqlException ex)
        {
            Console.WriteLine($"SQL Error loading configuration: {ex.Message}");
            // Could fall back to cached values or defaults
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Connection error loading configuration: {ex.Message}");
        }
    }

    private void RefreshConfiguration(object? state)
    {
        if (_disposed) return;

        try
        {
            var oldData = new Dictionary<string, string?>(Data);
            LoadFromDatabase();

            // Check for changes and trigger reload token if needed
            if (!DictionariesEqual(oldData, Data))
            {
                OnReload();
            }
        }
        catch
        {
            // Ignore refresh errors to prevent timer from stopping
        }
    }

    private static bool DictionariesEqual(Dictionary<string, string?> dict1, IDictionary<string, string?> dict2)
    {
        if (dict1.Count != dict2.Count)
            return false;

        foreach (var kvp in dict1)
        {
            if (!dict2.TryGetValue(kvp.Key, out var value) || value != kvp.Value)
                return false;
        }

        return true;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _refreshTimer?.Dispose();
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Configuration source for database provider.
/// </summary>
public class DatabaseConfigurationSource : IConfigurationSource
{
    public string ConnectionString { get; set; } = string.Empty;
    public string TableName { get; set; } = "Configuration";
    public TimeSpan? RefreshInterval { get; set; }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new DatabaseConfigurationProvider(ConnectionString, TableName, RefreshInterval);
    }
}

/// <summary>
/// Extension methods for adding database configuration.
/// </summary>
public static class DatabaseConfigurationExtensions
{
    /// <summary>
    /// Adds database configuration provider.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="connectionString">Database connection string.</param>
    /// <param name="tableName">Configuration table name.</param>
    /// <param name="refreshInterval">How often to refresh the configuration.</param>
    /// <returns>The configuration builder.</returns>
    public static IConfigurationBuilder AddDatabase(
        this IConfigurationBuilder builder,
        string connectionString,
        string tableName = "Configuration",
        TimeSpan? refreshInterval = null)
    {
        return builder.Add(new DatabaseConfigurationSource
        {
            ConnectionString = connectionString,
            TableName = tableName,
            RefreshInterval = refreshInterval
        });
    }

    /// <summary>
    /// Creates the configuration table schema if it doesn't exist.
    /// This is a helper method for setup scenarios.
    /// </summary>
    /// <param name="connectionString">Database connection string.</param>
    /// <param name="tableName">Configuration table name.</param>
    public static void EnsureConfigurationTable(string connectionString, string tableName = "Configuration")
    {
        const string createTableScript = @"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='{0}' AND xtype='U')
            BEGIN
                CREATE TABLE [{0}] (
                    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [Key] nvarchar(255) NOT NULL,
                    [Value] nvarchar(max) NULL,
                    [Environment] nvarchar(50) NULL,
                    [Description] nvarchar(500) NULL,
                    [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                    [UpdatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                    CONSTRAINT [UK_{0}_Key_Environment] UNIQUE ([Key], [Environment])
                );

                CREATE INDEX [IX_{0}_Environment] ON [{0}] ([Environment]);
                CREATE INDEX [IX_{0}_Key] ON [{0}] ([Key]);
            END";

        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();

            using var command = new SqlCommand(string.Format(createTableScript, tableName), connection);
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create configuration table: {ex.Message}", ex);
        }
    }
}