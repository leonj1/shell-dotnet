using Microsoft.Extensions.Configuration;

namespace DotNetShell.Abstractions.Services;

/// <summary>
/// Service interface for configuration access supporting dynamic updates and multiple sources.
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Gets a configuration value by key.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <returns>The configuration value, or null if not found.</returns>
    string? GetValue(string key);

    /// <summary>
    /// Gets a configuration value by key with a default value.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <param name="defaultValue">The default value to return if the key is not found.</param>
    /// <returns>The configuration value or the default value.</returns>
    string GetValue(string key, string defaultValue);

    /// <summary>
    /// Gets a strongly-typed configuration value.
    /// </summary>
    /// <typeparam name="T">The type to convert the value to.</typeparam>
    /// <param name="key">The configuration key.</param>
    /// <returns>The configuration value converted to the specified type.</returns>
    T? GetValue<T>(string key);

    /// <summary>
    /// Gets a strongly-typed configuration value with a default.
    /// </summary>
    /// <typeparam name="T">The type to convert the value to.</typeparam>
    /// <param name="key">The configuration key.</param>
    /// <param name="defaultValue">The default value to return if the key is not found.</param>
    /// <returns>The configuration value converted to the specified type or the default value.</returns>
    T GetValue<T>(string key, T defaultValue);

    /// <summary>
    /// Binds a configuration section to an object.
    /// </summary>
    /// <typeparam name="T">The type to bind to.</typeparam>
    /// <param name="key">The configuration section key.</param>
    /// <returns>An instance of T with values from the configuration section.</returns>
    T GetSection<T>(string key) where T : new();

    /// <summary>
    /// Binds a configuration section to an existing object instance.
    /// </summary>
    /// <typeparam name="T">The type of the object to bind to.</typeparam>
    /// <param name="key">The configuration section key.</param>
    /// <param name="instance">The object instance to bind to.</param>
    void BindSection<T>(string key, T instance);

    /// <summary>
    /// Gets all configuration keys that match the specified pattern.
    /// </summary>
    /// <param name="pattern">The pattern to match keys against (supports wildcards).</param>
    /// <returns>An enumerable collection of matching keys.</returns>
    IEnumerable<string> GetKeys(string pattern = "*");

    /// <summary>
    /// Gets all configuration values as key-value pairs.
    /// </summary>
    /// <param name="sectionKey">Optional section key to limit the scope.</param>
    /// <returns>A dictionary of configuration key-value pairs.</returns>
    IDictionary<string, string?> GetAll(string? sectionKey = null);

    /// <summary>
    /// Checks if a configuration key exists.
    /// </summary>
    /// <param name="key">The configuration key to check.</param>
    /// <returns>True if the key exists; otherwise, false.</returns>
    bool Exists(string key);

    /// <summary>
    /// Gets the connection string by name.
    /// </summary>
    /// <param name="name">The connection string name.</param>
    /// <returns>The connection string, or null if not found.</returns>
    string? GetConnectionString(string name);

    /// <summary>
    /// Gets all connection strings.
    /// </summary>
    /// <returns>A dictionary of connection string names and values.</returns>
    IDictionary<string, string?> GetConnectionStrings();

    /// <summary>
    /// Sets a configuration value (if the provider supports writing).
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the set operation.</returns>
    Task SetValueAsync(string key, string? value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets multiple configuration values (if the provider supports writing).
    /// </summary>
    /// <param name="values">The key-value pairs to set.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the set operation.</returns>
    Task SetValuesAsync(IDictionary<string, string?> values, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a configuration value (if the provider supports writing).
    /// </summary>
    /// <param name="key">The configuration key to remove.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the remove operation.</returns>
    Task RemoveValueAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reloads the configuration from all providers.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the reload operation.</returns>
    Task ReloadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to configuration change notifications.
    /// </summary>
    /// <param name="key">The configuration key to watch (supports wildcards).</param>
    /// <param name="callback">The callback to invoke when the configuration changes.</param>
    /// <returns>A disposable subscription that can be disposed to stop watching.</returns>
    IConfigurationSubscription Subscribe(string key, Action<ConfigurationChangeEventArgs> callback);

    /// <summary>
    /// Subscribes to configuration change notifications with async callback.
    /// </summary>
    /// <param name="key">The configuration key to watch (supports wildcards).</param>
    /// <param name="callback">The async callback to invoke when the configuration changes.</param>
    /// <returns>A disposable subscription that can be disposed to stop watching.</returns>
    IConfigurationSubscription Subscribe(string key, Func<ConfigurationChangeEventArgs, Task> callback);

    /// <summary>
    /// Gets configuration metadata for a specific key.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <returns>Metadata about the configuration key.</returns>
    ConfigurationMetadata? GetMetadata(string key);

    /// <summary>
    /// Validates a configuration section against a schema.
    /// </summary>
    /// <param name="sectionKey">The configuration section key.</param>
    /// <param name="schemaType">The type that defines the validation schema.</param>
    /// <returns>A validation result.</returns>
    ConfigurationValidationResult Validate(string sectionKey, Type schemaType);

    /// <summary>
    /// Validates a configuration section against a schema.
    /// </summary>
    /// <typeparam name="T">The type that defines the validation schema.</typeparam>
    /// <param name="sectionKey">The configuration section key.</param>
    /// <returns>A validation result.</returns>
    ConfigurationValidationResult Validate<T>(string sectionKey) where T : new();

    /// <summary>
    /// Gets configuration values with encryption/decryption (if supported).
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <param name="decrypt">Whether to decrypt the value if it's encrypted.</param>
    /// <returns>The configuration value, optionally decrypted.</returns>
    Task<string?> GetSecureValueAsync(string key, bool decrypt = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets an encrypted configuration value (if the provider supports encryption).
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <param name="value">The value to encrypt and set.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the set operation.</returns>
    Task SetSecureValueAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a scoped configuration service for a specific section.
    /// </summary>
    /// <param name="sectionKey">The section key to scope to.</param>
    /// <returns>A scoped configuration service instance.</returns>
    IConfigurationService CreateScope(string sectionKey);

    /// <summary>
    /// Gets the configuration providers information.
    /// </summary>
    /// <returns>Information about all configuration providers.</returns>
    IEnumerable<ConfigurationProviderInfo> GetProviders();

    /// <summary>
    /// Gets the underlying IConfiguration instance.
    /// </summary>
    IConfiguration Configuration { get; }

    /// <summary>
    /// Gets a value indicating whether the configuration service supports writing.
    /// </summary>
    bool SupportsWriting { get; }

    /// <summary>
    /// Gets a value indicating whether the configuration service supports change notifications.
    /// </summary>
    bool SupportsChangeNotifications { get; }

    /// <summary>
    /// Gets a value indicating whether the configuration service supports encryption.
    /// </summary>
    bool SupportsEncryption { get; }

    /// <summary>
    /// Gets a value indicating whether the configuration service supports validation.
    /// </summary>
    bool SupportsValidation { get; }

    /// <summary>
    /// Event raised when configuration changes occur.
    /// </summary>
    event EventHandler<ConfigurationChangeEventArgs>? ConfigurationChanged;
}

/// <summary>
/// Represents a subscription to configuration changes.
/// </summary>
public interface IConfigurationSubscription : IDisposable
{
    /// <summary>
    /// Gets the subscription ID.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the configuration key being watched.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Gets a value indicating whether the subscription is active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Gets the number of change notifications received.
    /// </summary>
    int NotificationCount { get; }

    /// <summary>
    /// Stops watching for configuration changes.
    /// </summary>
    void Unsubscribe();
}

/// <summary>
/// Contains information about a configuration change event.
/// </summary>
public class ConfigurationChangeEventArgs : EventArgs
{
    /// <summary>
    /// Gets the configuration key that changed.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Gets the old configuration value.
    /// </summary>
    public string? OldValue { get; init; }

    /// <summary>
    /// Gets the new configuration value.
    /// </summary>
    public string? NewValue { get; init; }

    /// <summary>
    /// Gets the type of change that occurred.
    /// </summary>
    public ConfigurationChangeType ChangeType { get; init; }

    /// <summary>
    /// Gets the configuration provider that reported the change.
    /// </summary>
    public string? ProviderName { get; init; }

    /// <summary>
    /// Gets the timestamp when the change occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets additional metadata about the change.
    /// </summary>
    public IDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Enumeration of configuration change types.
/// </summary>
public enum ConfigurationChangeType
{
    /// <summary>
    /// A configuration value was added.
    /// </summary>
    Added,

    /// <summary>
    /// A configuration value was updated.
    /// </summary>
    Updated,

    /// <summary>
    /// A configuration value was removed.
    /// </summary>
    Removed,

    /// <summary>
    /// The configuration was reloaded.
    /// </summary>
    Reloaded
}

/// <summary>
/// Contains metadata about a configuration key.
/// </summary>
public class ConfigurationMetadata
{
    /// <summary>
    /// Gets or sets the configuration key.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the configuration provider that provided the value.
    /// </summary>
    public string? ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the source of the configuration value (file path, etc.).
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the value is encrypted.
    /// </summary>
    public bool IsEncrypted { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the value is sensitive.
    /// </summary>
    public bool IsSensitive { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the value can be changed at runtime.
    /// </summary>
    public bool IsMutable { get; set; }

    /// <summary>
    /// Gets or sets the last modified timestamp.
    /// </summary>
    public DateTimeOffset? LastModified { get; set; }

    /// <summary>
    /// Gets or sets the data type of the configuration value.
    /// </summary>
    public Type? ValueType { get; set; }

    /// <summary>
    /// Gets or sets the description of the configuration key.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets additional metadata properties.
    /// </summary>
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Contains information about a configuration provider.
/// </summary>
public class ConfigurationProviderInfo
{
    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider priority/order.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider supports writing.
    /// </summary>
    public bool SupportsWriting { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider supports change notifications.
    /// </summary>
    public bool SupportsChangeNotifications { get; set; }

    /// <summary>
    /// Gets or sets the provider source information.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Gets or sets the provider status.
    /// </summary>
    public ConfigurationProviderStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the last load timestamp.
    /// </summary>
    public DateTimeOffset? LastLoaded { get; set; }

    /// <summary>
    /// Gets or sets the number of configuration keys provided.
    /// </summary>
    public int KeyCount { get; set; }

    /// <summary>
    /// Gets or sets additional provider-specific information.
    /// </summary>
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Enumeration of configuration provider status values.
/// </summary>
public enum ConfigurationProviderStatus
{
    /// <summary>
    /// The provider is healthy and functioning normally.
    /// </summary>
    Healthy,

    /// <summary>
    /// The provider is functioning but with degraded performance.
    /// </summary>
    Degraded,

    /// <summary>
    /// The provider is not functioning properly.
    /// </summary>
    Unhealthy,

    /// <summary>
    /// The provider is disabled.
    /// </summary>
    Disabled
}

/// <summary>
/// Represents the result of configuration validation.
/// </summary>
public class ConfigurationValidationResult
{
    /// <summary>
    /// Gets a value indicating whether the validation was successful.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IList<ConfigurationValidationError> Errors { get; init; } = new List<ConfigurationValidationError>();

    /// <summary>
    /// Gets the validation warnings.
    /// </summary>
    public IList<ConfigurationValidationWarning> Warnings { get; init; } = new List<ConfigurationValidationWarning>();

    /// <summary>
    /// Gets the configuration section that was validated.
    /// </summary>
    public string SectionKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the schema type used for validation.
    /// </summary>
    public Type? SchemaType { get; init; }

    /// <summary>
    /// Gets the validation timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <param name="sectionKey">The configuration section key.</param>
    /// <param name="schemaType">The schema type used.</param>
    /// <param name="warnings">Optional validation warnings.</param>
    /// <returns>A successful validation result.</returns>
    public static ConfigurationValidationResult Success(string sectionKey, Type? schemaType = null, IList<ConfigurationValidationWarning>? warnings = null)
    {
        return new ConfigurationValidationResult
        {
            IsValid = true,
            SectionKey = sectionKey,
            SchemaType = schemaType,
            Warnings = warnings ?? new List<ConfigurationValidationWarning>()
        };
    }

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="sectionKey">The configuration section key.</param>
    /// <param name="errors">The validation errors.</param>
    /// <param name="schemaType">The schema type used.</param>
    /// <param name="warnings">Optional validation warnings.</param>
    /// <returns>A failed validation result.</returns>
    public static ConfigurationValidationResult Failure(string sectionKey, IList<ConfigurationValidationError> errors, Type? schemaType = null, IList<ConfigurationValidationWarning>? warnings = null)
    {
        return new ConfigurationValidationResult
        {
            IsValid = false,
            SectionKey = sectionKey,
            Errors = errors,
            SchemaType = schemaType,
            Warnings = warnings ?? new List<ConfigurationValidationWarning>()
        };
    }
}

/// <summary>
/// Represents a configuration validation error.
/// </summary>
public class ConfigurationValidationError
{
    /// <summary>
    /// Gets or sets the configuration key that failed validation.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the expected value or format.
    /// </summary>
    public string? Expected { get; set; }

    /// <summary>
    /// Gets or sets the actual value that failed validation.
    /// </summary>
    public string? Actual { get; set; }

    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets additional error details.
    /// </summary>
    public IDictionary<string, object> Details { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Represents a configuration validation warning.
/// </summary>
public class ConfigurationValidationWarning
{
    /// <summary>
    /// Gets or sets the configuration key that generated the warning.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the warning message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the warning code.
    /// </summary>
    public string? WarningCode { get; set; }

    /// <summary>
    /// Gets or sets additional warning details.
    /// </summary>
    public IDictionary<string, object> Details { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Extension methods for IConfigurationService.
/// </summary>
public static class ConfigurationServiceExtensions
{
    /// <summary>
    /// Gets a configuration value as a boolean.
    /// </summary>
    /// <param name="config">The configuration service.</param>
    /// <param name="key">The configuration key.</param>
    /// <param name="defaultValue">The default value if the key is not found.</param>
    /// <returns>The configuration value as a boolean.</returns>
    public static bool GetBool(this IConfigurationService config, string key, bool defaultValue = false)
    {
        return config.GetValue(key, defaultValue);
    }

    /// <summary>
    /// Gets a configuration value as an integer.
    /// </summary>
    /// <param name="config">The configuration service.</param>
    /// <param name="key">The configuration key.</param>
    /// <param name="defaultValue">The default value if the key is not found.</param>
    /// <returns>The configuration value as an integer.</returns>
    public static int GetInt(this IConfigurationService config, string key, int defaultValue = 0)
    {
        return config.GetValue(key, defaultValue);
    }

    /// <summary>
    /// Gets a configuration value as a double.
    /// </summary>
    /// <param name="config">The configuration service.</param>
    /// <param name="key">The configuration key.</param>
    /// <param name="defaultValue">The default value if the key is not found.</param>
    /// <returns>The configuration value as a double.</returns>
    public static double GetDouble(this IConfigurationService config, string key, double defaultValue = 0.0)
    {
        return config.GetValue(key, defaultValue);
    }

    /// <summary>
    /// Gets a configuration value as a TimeSpan.
    /// </summary>
    /// <param name="config">The configuration service.</param>
    /// <param name="key">The configuration key.</param>
    /// <param name="defaultValue">The default value if the key is not found.</param>
    /// <returns>The configuration value as a TimeSpan.</returns>
    public static TimeSpan GetTimeSpan(this IConfigurationService config, string key, TimeSpan defaultValue = default)
    {
        return config.GetValue(key, defaultValue);
    }

    /// <summary>
    /// Gets a configuration value as a DateTime.
    /// </summary>
    /// <param name="config">The configuration service.</param>
    /// <param name="key">The configuration key.</param>
    /// <param name="defaultValue">The default value if the key is not found.</param>
    /// <returns>The configuration value as a DateTime.</returns>
    public static DateTime GetDateTime(this IConfigurationService config, string key, DateTime defaultValue = default)
    {
        return config.GetValue(key, defaultValue);
    }

    /// <summary>
    /// Gets a configuration value as an enum.
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="config">The configuration service.</param>
    /// <param name="key">The configuration key.</param>
    /// <param name="defaultValue">The default value if the key is not found.</param>
    /// <returns>The configuration value as an enum.</returns>
    public static TEnum GetEnum<TEnum>(this IConfigurationService config, string key, TEnum defaultValue = default) where TEnum : struct, Enum
    {
        var value = config.GetValue(key);
        if (string.IsNullOrEmpty(value))
            return defaultValue;

        return Enum.TryParse<TEnum>(value, true, out var result) ? result : defaultValue;
    }

    /// <summary>
    /// Gets a configuration value as a list of strings.
    /// </summary>
    /// <param name="config">The configuration service.</param>
    /// <param name="key">The configuration key.</param>
    /// <param name="separator">The separator used to split the value (default is comma).</param>
    /// <returns>The configuration value as a list of strings.</returns>
    public static IList<string> GetStringList(this IConfigurationService config, string key, char separator = ',')
    {
        var value = config.GetValue(key);
        if (string.IsNullOrEmpty(value))
            return new List<string>();

        return value.Split(separator, StringSplitOptions.RemoveEmptyEntries)
                   .Select(s => s.Trim())
                   .ToList();
    }

    /// <summary>
    /// Checks if a feature flag is enabled.
    /// </summary>
    /// <param name="config">The configuration service.</param>
    /// <param name="featureName">The feature flag name.</param>
    /// <param name="defaultValue">The default value if the flag is not found.</param>
    /// <returns>True if the feature is enabled; otherwise, false.</returns>
    public static bool IsFeatureEnabled(this IConfigurationService config, string featureName, bool defaultValue = false)
    {
        return config.GetBool($"Features:{featureName}", defaultValue);
    }

    /// <summary>
    /// Creates a configuration builder from the service configuration.
    /// </summary>
    /// <param name="config">The configuration service.</param>
    /// <returns>A configuration builder with the same values.</returns>
    public static IConfigurationBuilder ToBuilder(this IConfigurationService config)
    {
        var builder = new ConfigurationBuilder();
        var values = config.GetAll();

        builder.AddInMemoryCollection(values.Where(kvp => kvp.Value != null)
                                          .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!));

        return builder;
    }
}