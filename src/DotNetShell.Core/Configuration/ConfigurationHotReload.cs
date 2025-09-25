using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System.Collections.Concurrent;

namespace DotNetShell.Core.Configuration;

/// <summary>
/// Service for managing configuration hot-reload capabilities with file watchers
/// and change notification system.
/// </summary>
public interface IConfigurationHotReloadService
{
    /// <summary>
    /// Starts monitoring configuration files for changes.
    /// </summary>
    void StartMonitoring();

    /// <summary>
    /// Stops monitoring configuration files.
    /// </summary>
    void StopMonitoring();

    /// <summary>
    /// Registers a callback for configuration changes.
    /// </summary>
    /// <param name="key">Configuration key pattern to watch.</param>
    /// <param name="callback">Callback to invoke when changes occur.</param>
    /// <returns>A disposable registration.</returns>
    IDisposable RegisterCallback(string key, Action<ConfigurationChangeEventArgs> callback);

    /// <summary>
    /// Forces a reload of all configuration sources.
    /// </summary>
    Task ForceReloadAsync();

    /// <summary>
    /// Event raised when configuration changes are detected.
    /// </summary>
    event EventHandler<ConfigurationChangeEventArgs>? ConfigurationChanged;
}

/// <summary>
/// Implementation of configuration hot-reload service.
/// </summary>
public class ConfigurationHotReloadService : IConfigurationHotReloadService, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigurationHotReloadService> _logger;
    private readonly ConcurrentDictionary<string, CallbackRegistration> _callbacks;
    private readonly ConcurrentDictionary<string, IChangeToken> _changeTokens;
    private readonly ConcurrentDictionary<string, string?> _lastKnownValues;
    private readonly Timer _changeDetectionTimer;
    private readonly object _lock = new();
    private bool _isMonitoring;
    private bool _disposed;

    public ConfigurationHotReloadService(
        IConfiguration configuration,
        ILogger<ConfigurationHotReloadService> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _callbacks = new ConcurrentDictionary<string, CallbackRegistration>();
        _changeTokens = new ConcurrentDictionary<string, IChangeToken>();
        _lastKnownValues = new ConcurrentDictionary<string, string?>();

        // Timer to periodically check for changes
        _changeDetectionTimer = new Timer(DetectChanges, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <inheritdoc />
    public event EventHandler<ConfigurationChangeEventArgs>? ConfigurationChanged;

    /// <inheritdoc />
    public void StartMonitoring()
    {
        lock (_lock)
        {
            if (_isMonitoring) return;

            _logger.LogInformation("Starting configuration hot-reload monitoring");

            SetupChangeTokens();
            _changeDetectionTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(5));
            _isMonitoring = true;

            _logger.LogInformation("Configuration hot-reload monitoring started");
        }
    }

    /// <inheritdoc />
    public void StopMonitoring()
    {
        lock (_lock)
        {
            if (!_isMonitoring) return;

            _logger.LogInformation("Stopping configuration hot-reload monitoring");

            _changeDetectionTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _isMonitoring = false;

            _logger.LogInformation("Configuration hot-reload monitoring stopped");
        }
    }

    /// <inheritdoc />
    public IDisposable RegisterCallback(string key, Action<ConfigurationChangeEventArgs> callback)
    {
        var registration = new CallbackRegistration(key, callback);
        _callbacks[registration.Id] = registration;

        _logger.LogDebug("Registered configuration change callback for key pattern: {Key}", key);

        return registration;
    }

    /// <inheritdoc />
    public async Task ForceReloadAsync()
    {
        _logger.LogInformation("Forcing configuration reload");

        try
        {
            if (_configuration is IConfigurationRoot configRoot)
            {
                configRoot.Reload();
                await CheckForChanges();
            }

            _logger.LogInformation("Configuration force reload completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during forced configuration reload");
            throw;
        }
    }

    private void SetupChangeTokens()
    {
        if (_configuration is IConfigurationRoot configRoot)
        {
            var changeToken = configRoot.GetReloadToken();
            RegisterChangeToken("_root", changeToken);
        }

        // Set up change tokens for specific sections if needed
        var importantSections = new[]
        {
            "Shell",
            "Shell:Services",
            "Shell:Modules",
            "ConnectionStrings",
            "Logging"
        };

        foreach (var section in importantSections)
        {
            var configSection = _configuration.GetSection(section);
            if (configSection.Exists())
            {
                var changeToken = configSection.GetReloadToken();
                RegisterChangeToken(section, changeToken);
            }
        }
    }

    private void RegisterChangeToken(string key, IChangeToken changeToken)
    {
        _changeTokens[key] = changeToken;

        changeToken.RegisterChangeCallback(async _ =>
        {
            if (_isMonitoring)
            {
                _logger.LogDebug("Change token triggered for: {Key}", key);
                await CheckForChanges();
            }
        }, key);
    }

    private void DetectChanges(object? state)
    {
        if (!_isMonitoring || _disposed) return;

        try
        {
            CheckForChanges().Wait();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during change detection");
        }
    }

    private async Task CheckForChanges()
    {
        var detectedChanges = new List<ConfigurationChangeEventArgs>();

        // Check all registered callback keys for changes
        foreach (var callbackGroup in _callbacks.Values.GroupBy(c => c.Key))
        {
            var key = callbackGroup.Key;
            await CheckKeyForChanges(key, detectedChanges);
        }

        // Also check some important keys even if no callbacks are registered
        var importantKeys = new[]
        {
            "Shell:Services:Authentication:Enabled",
            "Shell:Services:Authorization:Enabled",
            "Shell:Modules:AutoLoad",
            "Shell:Modules:ReloadOnChange"
        };

        foreach (var key in importantKeys)
        {
            await CheckKeyForChanges(key, detectedChanges);
        }

        // Notify callbacks of changes
        foreach (var change in detectedChanges)
        {
            await NotifyCallbacks(change);
            OnConfigurationChanged(change);
        }
    }

    private async Task CheckKeyForChanges(string keyPattern, List<ConfigurationChangeEventArgs> changes)
    {
        try
        {
            // Handle wildcard patterns
            var keysToCheck = ResolveKeyPattern(keyPattern);

            foreach (var key in keysToCheck)
            {
                var currentValue = _configuration[key];
                var lastKnownValue = _lastKnownValues.GetValueOrDefault(key);

                if (!string.Equals(currentValue, lastKnownValue, StringComparison.Ordinal))
                {
                    var changeType = DetermineChangeType(lastKnownValue, currentValue);

                    var changeArgs = new ConfigurationChangeEventArgs
                    {
                        Key = key,
                        OldValue = lastKnownValue,
                        NewValue = currentValue,
                        ChangeType = changeType,
                        ProviderName = "HotReload",
                        Timestamp = DateTimeOffset.UtcNow
                    };

                    changes.Add(changeArgs);

                    // Update last known value
                    if (currentValue != null)
                    {
                        _lastKnownValues[key] = currentValue;
                    }
                    else
                    {
                        _lastKnownValues.TryRemove(key, out _);
                    }

                    _logger.LogDebug("Configuration change detected: {Key} = '{NewValue}' (was '{OldValue}')",
                        key, currentValue ?? "<null>", lastKnownValue ?? "<null>");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking key pattern for changes: {KeyPattern}", keyPattern);
        }
    }

    private IEnumerable<string> ResolveKeyPattern(string keyPattern)
    {
        if (!keyPattern.Contains('*') && !keyPattern.Contains('?'))
        {
            // Exact key
            return new[] { keyPattern };
        }

        // Wildcard pattern - get all keys and filter
        var allKeys = GetAllConfigurationKeys();
        var regex = new System.Text.RegularExpressions.Regex(
            "^" + System.Text.RegularExpressions.Regex.Escape(keyPattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return allKeys.Where(key => regex.IsMatch(key));
    }

    private IEnumerable<string> GetAllConfigurationKeys()
    {
        var keys = new HashSet<string>();
        AddKeysRecursive(_configuration, string.Empty, keys);
        return keys;
    }

    private static void AddKeysRecursive(IConfiguration configuration, string parentKey, HashSet<string> keys)
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

    private static ConfigurationChangeType DetermineChangeType(string? oldValue, string? newValue)
    {
        if (oldValue == null && newValue != null)
            return ConfigurationChangeType.Added;

        if (oldValue != null && newValue == null)
            return ConfigurationChangeType.Removed;

        return ConfigurationChangeType.Updated;
    }

    private async Task NotifyCallbacks(ConfigurationChangeEventArgs changeArgs)
    {
        var matchingCallbacks = _callbacks.Values.Where(c => DoesKeyMatchPattern(changeArgs.Key, c.Key));

        var notificationTasks = matchingCallbacks.Select(async callback =>
        {
            try
            {
                callback.Callback(changeArgs);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in configuration change callback for key: {Key}", changeArgs.Key);
            }
        });

        await Task.WhenAll(notificationTasks);
    }

    private static bool DoesKeyMatchPattern(string key, string pattern)
    {
        if (pattern == key) return true;

        if (!pattern.Contains('*') && !pattern.Contains('?'))
            return false;

        var regex = new System.Text.RegularExpressions.Regex(
            "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return regex.IsMatch(key);
    }

    private void OnConfigurationChanged(ConfigurationChangeEventArgs args)
    {
        try
        {
            ConfigurationChanged?.Invoke(this, args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in configuration changed event handler");
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            StopMonitoring();
            _changeDetectionTimer?.Dispose();

            foreach (var callback in _callbacks.Values)
            {
                callback.Dispose();
            }
            _callbacks.Clear();
            _changeTokens.Clear();
            _lastKnownValues.Clear();

            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Represents a registered callback for configuration changes.
    /// </summary>
    private class CallbackRegistration : IDisposable
    {
        public CallbackRegistration(string key, Action<ConfigurationChangeEventArgs> callback)
        {
            Id = Guid.NewGuid().ToString();
            Key = key;
            Callback = callback;
        }

        public string Id { get; }
        public string Key { get; }
        public Action<ConfigurationChangeEventArgs> Callback { get; }

        public void Dispose()
        {
            // Callback registration is managed by the parent service
        }
    }
}

/// <summary>
/// Extension methods for configuration hot-reload service.
/// </summary>
public static class ConfigurationHotReloadExtensions
{
    /// <summary>
    /// Adds configuration hot-reload service to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="autoStart">Whether to automatically start monitoring.</param>
    /// <returns>The service collection.</returns>
    public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddConfigurationHotReload(
        this Microsoft.Extensions.DependencyInjection.IServiceCollection services,
        bool autoStart = true)
    {
        services.AddSingleton<IConfigurationHotReloadService, ConfigurationHotReloadService>();

        if (autoStart)
        {
            services.AddHostedService<ConfigurationHotReloadHostedService>();
        }

        return services;
    }
}

/// <summary>
/// Hosted service for managing the configuration hot-reload service lifecycle.
/// </summary>
public class ConfigurationHotReloadHostedService : Microsoft.Extensions.Hosting.BackgroundService
{
    private readonly IConfigurationHotReloadService _hotReloadService;
    private readonly ILogger<ConfigurationHotReloadHostedService> _logger;

    public ConfigurationHotReloadHostedService(
        IConfigurationHotReloadService hotReloadService,
        ILogger<ConfigurationHotReloadHostedService> logger)
    {
        _hotReloadService = hotReloadService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Configuration hot-reload hosted service starting");

        _hotReloadService.StartMonitoring();

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        finally
        {
            _hotReloadService.StopMonitoring();
            _logger.LogInformation("Configuration hot-reload hosted service stopped");
        }
    }
}