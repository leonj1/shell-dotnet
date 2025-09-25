using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using DotNetShell.Abstractions.Services;
using System.Collections.Concurrent;
using System.Text.Json;

namespace DotNetShell.Core.Configuration;

/// <summary>
/// Implementation of IConfigurationService providing comprehensive configuration management
/// with support for multiple sources, validation, secret resolution, and change notifications.
/// </summary>
public class ConfigurationService : IConfigurationService, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ISecretResolver _secretResolver;
    private readonly IConfigurationValidator _validator;
    private readonly ConcurrentDictionary<string, IConfigurationSubscription> _subscriptions;
    private readonly ConcurrentDictionary<string, IChangeToken> _changeTokens;
    private readonly object _lock = new();
    private bool _disposed;

    public ConfigurationService(
        IConfiguration configuration,
        ISecretResolver secretResolver,
        IConfigurationValidator validator)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _secretResolver = secretResolver ?? throw new ArgumentNullException(nameof(secretResolver));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _subscriptions = new ConcurrentDictionary<string, IConfigurationSubscription>();
        _changeTokens = new ConcurrentDictionary<string, IChangeToken>();
    }

    /// <inheritdoc />
    public IConfiguration Configuration => _configuration;

    /// <inheritdoc />
    public bool SupportsWriting => false; // Most providers are read-only

    /// <inheritdoc />
    public bool SupportsChangeNotifications => true;

    /// <inheritdoc />
    public bool SupportsEncryption => _secretResolver.SupportsEncryption;

    /// <inheritdoc />
    public bool SupportsValidation => true;

    /// <inheritdoc />
    public event EventHandler<ConfigurationChangeEventArgs>? ConfigurationChanged;

    /// <inheritdoc />
    public string? GetValue(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty.", nameof(key));

        var value = _configuration[key];
        return _secretResolver.ResolveSecrets(value);
    }

    /// <inheritdoc />
    public string GetValue(string key, string defaultValue)
    {
        return GetValue(key) ?? defaultValue;
    }

    /// <inheritdoc />
    public T? GetValue<T>(string key)
    {
        var value = GetValue(key);
        if (value == null) return default(T);

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default(T);
        }
    }

    /// <inheritdoc />
    public T GetValue<T>(string key, T defaultValue)
    {
        return GetValue<T>(key) ?? defaultValue;
    }

    /// <inheritdoc />
    public T GetSection<T>(string key) where T : new()
    {
        var section = _configuration.GetSection(key);
        var instance = new T();
        section.Bind(instance);

        // Resolve secrets in the bound object
        _secretResolver.ResolveSecretsInObject(instance);

        return instance;
    }

    /// <inheritdoc />
    public void BindSection<T>(string key, T instance)
    {
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        var section = _configuration.GetSection(key);
        section.Bind(instance);

        // Resolve secrets in the bound object
        _secretResolver.ResolveSecretsInObject(instance);
    }

    /// <inheritdoc />
    public IEnumerable<string> GetKeys(string pattern = "*")
    {
        var allKeys = GetAllKeysRecursive(_configuration);

        if (pattern == "*")
            return allKeys;

        // Simple wildcard pattern matching
        var regex = new System.Text.RegularExpressions.Regex(
            "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return allKeys.Where(key => regex.IsMatch(key));
    }

    /// <inheritdoc />
    public IDictionary<string, string?> GetAll(string? sectionKey = null)
    {
        var result = new Dictionary<string, string?>();

        var configSection = string.IsNullOrEmpty(sectionKey)
            ? _configuration
            : _configuration.GetSection(sectionKey);

        foreach (var kvp in configSection.AsEnumerable(true))
        {
            if (kvp.Value != null)
            {
                result[kvp.Key] = _secretResolver.ResolveSecrets(kvp.Value);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public bool Exists(string key)
    {
        return _configuration.GetSection(key).Exists();
    }

    /// <inheritdoc />
    public string? GetConnectionString(string name)
    {
        var connectionString = _configuration.GetConnectionString(name);
        return _secretResolver.ResolveSecrets(connectionString);
    }

    /// <inheritdoc />
    public IDictionary<string, string?> GetConnectionStrings()
    {
        var result = new Dictionary<string, string?>();
        var connectionStrings = _configuration.GetSection("ConnectionStrings");

        foreach (var kvp in connectionStrings.AsEnumerable(true))
        {
            if (kvp.Value != null)
            {
                result[kvp.Key] = _secretResolver.ResolveSecrets(kvp.Value);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public Task SetValueAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("This configuration service is read-only.");
    }

    /// <inheritdoc />
    public Task SetValuesAsync(IDictionary<string, string?> values, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("This configuration service is read-only.");
    }

    /// <inheritdoc />
    public Task RemoveValueAsync(string key, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("This configuration service is read-only.");
    }

    /// <inheritdoc />
    public Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        if (_configuration is IConfigurationRoot configRoot)
        {
            configRoot.Reload();
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IConfigurationSubscription Subscribe(string key, Action<ConfigurationChangeEventArgs> callback)
    {
        return Subscribe(key, args =>
        {
            callback(args);
            return Task.CompletedTask;
        });
    }

    /// <inheritdoc />
    public IConfigurationSubscription Subscribe(string key, Func<ConfigurationChangeEventArgs, Task> callback)
    {
        var subscription = new ConfigurationSubscription(key, callback);
        _subscriptions[subscription.Id] = subscription;

        // Set up change token monitoring
        var changeToken = _configuration.GetReloadToken();
        _changeTokens[subscription.Id] = changeToken;

        changeToken.RegisterChangeCallback(async _ =>
        {
            if (subscription.IsActive)
            {
                var oldValue = GetValue(key);
                // For simplicity, we don't track the old value in this implementation
                var eventArgs = new ConfigurationChangeEventArgs
                {
                    Key = key,
                    OldValue = null, // Would need additional tracking
                    NewValue = GetValue(key),
                    ChangeType = ConfigurationChangeType.Updated,
                    ProviderName = "Unknown", // Would need provider tracking
                    Timestamp = DateTimeOffset.UtcNow
                };

                await callback(eventArgs);
                OnConfigurationChanged(eventArgs);
            }
        }, subscription);

        return subscription;
    }

    /// <inheritdoc />
    public ConfigurationMetadata? GetMetadata(string key)
    {
        if (!Exists(key))
            return null;

        return new ConfigurationMetadata
        {
            Key = key,
            ProviderName = "ConfigurationService",
            Source = "Multiple Sources",
            IsEncrypted = _secretResolver.ContainsSecretPlaceholders(GetValue(key)),
            IsSensitive = IsSensitiveKey(key),
            IsMutable = false,
            LastModified = DateTimeOffset.UtcNow,
            ValueType = InferValueType(GetValue(key)),
            Description = GetKeyDescription(key)
        };
    }

    /// <inheritdoc />
    public ConfigurationValidationResult Validate(string sectionKey, Type schemaType)
    {
        return _validator.Validate(_configuration.GetSection(sectionKey), schemaType);
    }

    /// <inheritdoc />
    public ConfigurationValidationResult Validate<T>(string sectionKey) where T : new()
    {
        return Validate(sectionKey, typeof(T));
    }

    /// <inheritdoc />
    public async Task<string?> GetSecureValueAsync(string key, bool decrypt = true, CancellationToken cancellationToken = default)
    {
        var value = GetValue(key);
        if (value == null || !decrypt)
            return value;

        return await _secretResolver.ResolveSecretsAsync(value, cancellationToken);
    }

    /// <inheritdoc />
    public Task SetSecureValueAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("This configuration service is read-only.");
    }

    /// <inheritdoc />
    public IConfigurationService CreateScope(string sectionKey)
    {
        var scopedConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(GetAll(sectionKey))
            .Build();

        return new ConfigurationService(scopedConfiguration, _secretResolver, _validator);
    }

    /// <inheritdoc />
    public IEnumerable<ConfigurationProviderInfo> GetProviders()
    {
        if (_configuration is IConfigurationRoot configRoot)
        {
            return configRoot.Providers.Select((provider, index) => new ConfigurationProviderInfo
            {
                Name = provider.GetType().Name,
                Type = provider.GetType().FullName ?? "Unknown",
                Priority = index,
                SupportsWriting = false, // Most providers are read-only
                SupportsChangeNotifications = provider is IConfigurationProvider,
                Source = GetProviderSource(provider),
                Status = ConfigurationProviderStatus.Healthy, // Simplified status
                LastLoaded = DateTimeOffset.UtcNow,
                KeyCount = GetProviderKeyCount(provider)
            });
        }

        return Enumerable.Empty<ConfigurationProviderInfo>();
    }

    private static IEnumerable<string> GetAllKeysRecursive(IConfiguration configuration)
    {
        var keys = new List<string>();
        AddKeysRecursive(configuration, "", keys);
        return keys;
    }

    private static void AddKeysRecursive(IConfiguration configuration, string parentKey, List<string> keys)
    {
        foreach (var child in configuration.GetChildren())
        {
            var key = string.IsNullOrEmpty(parentKey) ? child.Key : $"{parentKey}:{child.Key}";

            if (child.Value != null)
            {
                keys.Add(key);
            }

            AddKeysRecursive(child, key, keys);
        }
    }

    private static bool IsSensitiveKey(string key)
    {
        var sensitivePatterns = new[] { "password", "secret", "token", "key", "connectionstring" };
        return sensitivePatterns.Any(pattern =>
            key.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static Type? InferValueType(string? value)
    {
        if (value == null) return typeof(string);

        if (bool.TryParse(value, out _)) return typeof(bool);
        if (int.TryParse(value, out _)) return typeof(int);
        if (double.TryParse(value, out _)) return typeof(double);
        if (DateTime.TryParse(value, out _)) return typeof(DateTime);
        if (TimeSpan.TryParse(value, out _)) return typeof(TimeSpan);

        return typeof(string);
    }

    private static string? GetKeyDescription(string key)
    {
        // This could be enhanced with metadata from attributes or external documentation
        var parts = key.Split(':');
        return $"Configuration setting for {string.Join(" -> ", parts)}";
    }

    private static string? GetProviderSource(IConfigurationProvider provider)
    {
        var providerType = provider.GetType();

        // Extract source information based on provider type
        if (providerType.Name.Contains("File"))
        {
            return "File System";
        }
        else if (providerType.Name.Contains("Environment"))
        {
            return "Environment Variables";
        }
        else if (providerType.Name.Contains("CommandLine"))
        {
            return "Command Line Arguments";
        }
        else if (providerType.Name.Contains("Memory"))
        {
            return "In Memory";
        }

        return providerType.Name;
    }

    private static int GetProviderKeyCount(IConfigurationProvider provider)
    {
        // This is a simplified implementation
        // In a real scenario, you might need to enumerate the provider's data
        return 0; // Placeholder
    }

    private void OnConfigurationChanged(ConfigurationChangeEventArgs args)
    {
        ConfigurationChanged?.Invoke(this, args);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            // Dispose all subscriptions
            foreach (var subscription in _subscriptions.Values)
            {
                subscription.Dispose();
            }
            _subscriptions.Clear();
            _changeTokens.Clear();

            if (_secretResolver is IDisposable disposableResolver)
            {
                disposableResolver.Dispose();
            }

            if (_validator is IDisposable disposableValidator)
            {
                disposableValidator.Dispose();
            }

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
/// Implementation of IConfigurationSubscription for tracking configuration change subscriptions.
/// </summary>
internal class ConfigurationSubscription : IConfigurationSubscription
{
    private readonly Func<ConfigurationChangeEventArgs, Task> _callback;
    private bool _disposed;

    public ConfigurationSubscription(string key, Func<ConfigurationChangeEventArgs, Task> callback)
    {
        Id = Guid.NewGuid().ToString();
        Key = key ?? throw new ArgumentNullException(nameof(key));
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        IsActive = true;
        NotificationCount = 0;
    }

    public string Id { get; }
    public string Key { get; }
    public bool IsActive { get; private set; }
    public int NotificationCount { get; private set; }

    internal async Task NotifyAsync(ConfigurationChangeEventArgs args)
    {
        if (IsActive && !_disposed)
        {
            await _callback(args);
            NotificationCount++;
        }
    }

    public void Unsubscribe()
    {
        IsActive = false;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Unsubscribe();
            _disposed = true;
        }
    }
}