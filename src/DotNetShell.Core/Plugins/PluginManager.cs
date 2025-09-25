using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using DotNetShell.Abstractions;

namespace DotNetShell.Core.Plugins;

/// <summary>
/// High-level service that manages the complete lifecycle of plugins including loading,
/// health monitoring, configuration updates, and graceful shutdown.
/// </summary>
public class PluginManager : IPluginManager, IHostedService, IAsyncDisposable
{
    private readonly PluginManagerOptions _options;
    private readonly ILogger<PluginManager> _logger;
    private readonly IPluginLoader _pluginLoader;
    private readonly IServiceProvider _serviceProvider;

    private readonly ConcurrentDictionary<string, PluginRuntimeInfo> _pluginRuntimes = new();
    private readonly Timer? _healthCheckTimer;
    private readonly Timer? _configurationWatchTimer;
    private readonly object _managerLock = new();
    private readonly CancellationTokenSource _shutdownTokenSource = new();

    private bool _isStarted;
    private bool _disposed;

    /// <summary>
    /// Gets the active plugins.
    /// </summary>
    public IReadOnlyDictionary<string, PluginRuntimeInfo> ActivePlugins => _pluginRuntimes.AsReadOnly();

    /// <summary>
    /// Gets whether the plugin manager is started.
    /// </summary>
    public bool IsStarted => _isStarted;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginManager"/> class.
    /// </summary>
    /// <param name="options">Plugin manager configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="pluginLoader">Plugin loader service.</param>
    /// <param name="serviceProvider">Service provider.</param>
    public PluginManager(
        IOptions<PluginManagerOptions> options,
        ILogger<PluginManager> logger,
        IPluginLoader pluginLoader,
        IServiceProvider serviceProvider)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pluginLoader = pluginLoader ?? throw new ArgumentNullException(nameof(pluginLoader));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        // Initialize health check timer
        if (_options.EnableHealthChecks && _options.HealthCheckIntervalMs > 0)
        {
            _healthCheckTimer = new Timer(
                PerformHealthChecks,
                null,
                TimeSpan.FromMilliseconds(_options.HealthCheckIntervalMs),
                TimeSpan.FromMilliseconds(_options.HealthCheckIntervalMs));
        }

        // Initialize configuration watch timer
        if (_options.EnableConfigurationWatch && _options.ConfigurationWatchIntervalMs > 0)
        {
            _configurationWatchTimer = new Timer(
                CheckConfigurationChanges,
                null,
                TimeSpan.FromMilliseconds(_options.ConfigurationWatchIntervalMs),
                TimeSpan.FromMilliseconds(_options.ConfigurationWatchIntervalMs));
        }
    }

    /// <summary>
    /// Starts the plugin manager and loads all configured plugins.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the start operation.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_isStarted)
        {
            return;
        }

        lock (_managerLock)
        {
            if (_isStarted)
            {
                return;
            }
            _isStarted = true;
        }

        _logger.LogInformation("Starting plugin manager");

        try
        {
            // Load all plugins
            var loadingResults = await _pluginLoader.LoadAllPluginsAsync(cancellationToken);

            // Create runtime info for each initialized plugin
            foreach (var initializedPlugin in loadingResults.InitializedPlugins)
            {
                var runtimeInfo = new PluginRuntimeInfo
                {
                    PluginId = initializedPlugin.PluginId,
                    Version = initializedPlugin.Version,
                    InitializedPlugin = initializedPlugin,
                    Status = PluginRuntimeStatus.Running,
                    StartedAt = DateTime.UtcNow,
                    HealthStatus = PluginHealthStatus.Unknown,
                    ConfigurationVersion = 1
                };

                _pluginRuntimes.TryAdd(initializedPlugin.PluginId, runtimeInfo);
            }

            _logger.LogInformation("Plugin manager started successfully. " +
                                  "Loaded {LoadedCount} plugins, Failed {FailedCount} plugins",
                loadingResults.InitializedPlugins.Count, loadingResults.FailedPlugins.Count);

            // Start health checks
            if (_options.EnableHealthChecks)
            {
                _ = Task.Run(() => PerformInitialHealthChecks(cancellationToken), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start plugin manager");
            _isStarted = false;
            throw;
        }
    }

    /// <summary>
    /// Stops the plugin manager and unloads all plugins gracefully.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the stop operation.</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_isStarted)
        {
            return;
        }

        _logger.LogInformation("Stopping plugin manager");

        // Signal shutdown to all background operations
        _shutdownTokenSource.Cancel();

        try
        {
            // Stop all plugins gracefully
            var stopTasks = _pluginRuntimes.Values
                .Where(runtime => runtime.Status == PluginRuntimeStatus.Running)
                .Select(runtime => StopPluginAsync(runtime, cancellationToken))
                .ToArray();

            await Task.WhenAll(stopTasks);

            // Unload all plugins
            await _pluginLoader.UnloadAllPluginsAsync(cancellationToken);

            _pluginRuntimes.Clear();
            _isStarted = false;

            _logger.LogInformation("Plugin manager stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping plugin manager");
            throw;
        }
    }

    /// <summary>
    /// Gets the current status of all managed plugins.
    /// </summary>
    /// <returns>The overall plugin status.</returns>
    public PluginManagerStatus GetStatus()
    {
        var activePlugins = _pluginRuntimes.Values.ToList();

        return new PluginManagerStatus
        {
            IsStarted = _isStarted,
            TotalPlugins = activePlugins.Count,
            RunningPlugins = activePlugins.Count(p => p.Status == PluginRuntimeStatus.Running),
            FailedPlugins = activePlugins.Count(p => p.Status == PluginRuntimeStatus.Failed),
            StoppedPlugins = activePlugins.Count(p => p.Status == PluginRuntimeStatus.Stopped),
            HealthyPlugins = activePlugins.Count(p => p.HealthStatus == PluginHealthStatus.Healthy),
            UnhealthyPlugins = activePlugins.Count(p => p.HealthStatus == PluginHealthStatus.Unhealthy),
            LastHealthCheck = activePlugins.Max(p => p.LastHealthCheck),
            Plugins = activePlugins.ToList()
        };
    }

    /// <summary>
    /// Gets the runtime information for a specific plugin.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <returns>The plugin runtime information, or null if not found.</returns>
    public PluginRuntimeInfo? GetPluginInfo(string pluginId)
    {
        ArgumentException.ThrowIfNullOrEmpty(pluginId);

        _pluginRuntimes.TryGetValue(pluginId, out var runtimeInfo);
        return runtimeInfo;
    }

    /// <summary>
    /// Loads a specific plugin at runtime.
    /// </summary>
    /// <param name="assemblyPath">The path to the plugin assembly.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The plugin load result.</returns>
    public async Task<PluginLoadResult> LoadPluginAsync(string assemblyPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyPath);

        _logger.LogInformation("Loading plugin at runtime: {AssemblyPath}", assemblyPath);

        try
        {
            var loadResult = await _pluginLoader.LoadPluginAsync(assemblyPath, cancellationToken);

            if (loadResult.IsSuccessful && loadResult.InitializedPlugin != null)
            {
                var runtimeInfo = new PluginRuntimeInfo
                {
                    PluginId = loadResult.InitializedPlugin.PluginId,
                    Version = loadResult.InitializedPlugin.Version,
                    InitializedPlugin = loadResult.InitializedPlugin,
                    Status = PluginRuntimeStatus.Running,
                    StartedAt = DateTime.UtcNow,
                    HealthStatus = PluginHealthStatus.Unknown,
                    ConfigurationVersion = 1
                };

                _pluginRuntimes.TryAdd(loadResult.InitializedPlugin.PluginId, runtimeInfo);

                _logger.LogInformation("Successfully loaded plugin at runtime: {PluginId} v{Version}",
                    loadResult.InitializedPlugin.PluginId, loadResult.InitializedPlugin.Version);
            }

            return loadResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load plugin at runtime: {AssemblyPath}", assemblyPath);
            throw;
        }
    }

    /// <summary>
    /// Unloads a specific plugin at runtime.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The plugin unload result.</returns>
    public async Task<PluginUnloadResult> UnloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(pluginId);

        _logger.LogInformation("Unloading plugin at runtime: {PluginId}", pluginId);

        try
        {
            // Update runtime status
            if (_pluginRuntimes.TryGetValue(pluginId, out var runtimeInfo))
            {
                runtimeInfo.Status = PluginRuntimeStatus.Stopping;
            }

            var unloadResult = await _pluginLoader.UnloadPluginAsync(pluginId, cancellationToken);

            // Remove from runtime tracking
            _pluginRuntimes.TryRemove(pluginId, out _);

            _logger.LogInformation("Successfully unloaded plugin at runtime: {PluginId}", pluginId);

            return unloadResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unload plugin at runtime: {PluginId}", pluginId);

            // Update status to failed
            if (_pluginRuntimes.TryGetValue(pluginId, out var runtimeInfo))
            {
                runtimeInfo.Status = PluginRuntimeStatus.Failed;
                runtimeInfo.LastError = ex.Message;
                runtimeInfo.LastErrorAt = DateTime.UtcNow;
            }

            throw;
        }
    }

    /// <summary>
    /// Reloads a specific plugin at runtime.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The plugin reload result.</returns>
    public async Task<PluginReloadResult> ReloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(pluginId);

        _logger.LogInformation("Reloading plugin at runtime: {PluginId}", pluginId);

        try
        {
            // Update runtime status
            if (_pluginRuntimes.TryGetValue(pluginId, out var runtimeInfo))
            {
                runtimeInfo.Status = PluginRuntimeStatus.Reloading;
            }

            var reloadResult = await _pluginLoader.ReloadPluginAsync(pluginId, cancellationToken);

            if (reloadResult.IsSuccessful && reloadResult.LoadResult?.InitializedPlugin != null)
            {
                // Update runtime info with new plugin instance
                var newRuntimeInfo = new PluginRuntimeInfo
                {
                    PluginId = reloadResult.LoadResult.InitializedPlugin.PluginId,
                    Version = reloadResult.LoadResult.InitializedPlugin.Version,
                    InitializedPlugin = reloadResult.LoadResult.InitializedPlugin,
                    Status = PluginRuntimeStatus.Running,
                    StartedAt = DateTime.UtcNow,
                    HealthStatus = PluginHealthStatus.Unknown,
                    ConfigurationVersion = (runtimeInfo?.ConfigurationVersion ?? 0) + 1,
                    ReloadCount = (runtimeInfo?.ReloadCount ?? 0) + 1
                };

                _pluginRuntimes.AddOrUpdate(pluginId, newRuntimeInfo, (key, old) => newRuntimeInfo);

                _logger.LogInformation("Successfully reloaded plugin at runtime: {PluginId} v{Version}",
                    reloadResult.LoadResult.InitializedPlugin.PluginId,
                    reloadResult.LoadResult.InitializedPlugin.Version);
            }
            else
            {
                // Mark as failed
                if (_pluginRuntimes.TryGetValue(pluginId, out runtimeInfo))
                {
                    runtimeInfo.Status = PluginRuntimeStatus.Failed;
                    runtimeInfo.LastError = string.Join(", ", reloadResult.Errors);
                    runtimeInfo.LastErrorAt = DateTime.UtcNow;
                }
            }

            return reloadResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload plugin at runtime: {PluginId}", pluginId);

            // Update status to failed
            if (_pluginRuntimes.TryGetValue(pluginId, out var runtimeInfo))
            {
                runtimeInfo.Status = PluginRuntimeStatus.Failed;
                runtimeInfo.LastError = ex.Message;
                runtimeInfo.LastErrorAt = DateTime.UtcNow;
            }

            throw;
        }
    }

    /// <summary>
    /// Updates the configuration for a specific plugin.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="newConfiguration">The new configuration values.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the configuration update operation.</returns>
    public async Task UpdatePluginConfigurationAsync(
        string pluginId,
        IReadOnlyDictionary<string, object> newConfiguration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(pluginId);
        ArgumentNullException.ThrowIfNull(newConfiguration);

        if (!_pluginRuntimes.TryGetValue(pluginId, out var runtimeInfo))
        {
            throw new ArgumentException($"Plugin '{pluginId}' is not currently managed", nameof(pluginId));
        }

        _logger.LogInformation("Updating configuration for plugin: {PluginId}", pluginId);

        try
        {
            var moduleInstance = runtimeInfo.InitializedPlugin?.ModuleInstance;
            if (moduleInstance != null)
            {
                await moduleInstance.OnConfigurationChangedAsync(newConfiguration, cancellationToken);

                runtimeInfo.ConfigurationVersion++;
                runtimeInfo.LastConfigurationUpdate = DateTime.UtcNow;

                _logger.LogDebug("Successfully updated configuration for plugin: {PluginId}", pluginId);
            }
        }
        catch (Exception ex)
        {
            runtimeInfo.LastError = $"Configuration update failed: {ex.Message}";
            runtimeInfo.LastErrorAt = DateTime.UtcNow;

            _logger.LogError(ex, "Failed to update configuration for plugin: {PluginId}", pluginId);
            throw;
        }
    }

    /// <summary>
    /// Performs a health check on a specific plugin.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The health check result.</returns>
    public async Task<ModuleHealthResult> CheckPluginHealthAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(pluginId);

        if (!_pluginRuntimes.TryGetValue(pluginId, out var runtimeInfo))
        {
            throw new ArgumentException($"Plugin '{pluginId}' is not currently managed", nameof(pluginId));
        }

        try
        {
            var moduleInstance = runtimeInfo.InitializedPlugin?.ModuleInstance;
            if (moduleInstance != null)
            {
                var healthResult = await moduleInstance.CheckHealthAsync(cancellationToken);

                // Update runtime info
                runtimeInfo.HealthStatus = healthResult.Status switch
                {
                    ModuleHealthStatus.Healthy => PluginHealthStatus.Healthy,
                    ModuleHealthStatus.Degraded => PluginHealthStatus.Degraded,
                    ModuleHealthStatus.Unhealthy => PluginHealthStatus.Unhealthy,
                    _ => PluginHealthStatus.Unknown
                };

                runtimeInfo.LastHealthCheck = DateTime.UtcNow;
                runtimeInfo.LastHealthResult = healthResult;

                return healthResult;
            }

            return ModuleHealthResult.Unhealthy("Plugin module instance is null");
        }
        catch (Exception ex)
        {
            runtimeInfo.HealthStatus = PluginHealthStatus.Unhealthy;
            runtimeInfo.LastHealthCheck = DateTime.UtcNow;
            runtimeInfo.LastError = $"Health check failed: {ex.Message}";
            runtimeInfo.LastErrorAt = DateTime.UtcNow;

            var errorResult = ModuleHealthResult.Unhealthy($"Health check exception: {ex.Message}");
            runtimeInfo.LastHealthResult = errorResult;

            return errorResult;
        }
    }

    private async Task StopPluginAsync(PluginRuntimeInfo runtimeInfo, CancellationToken cancellationToken)
    {
        try
        {
            runtimeInfo.Status = PluginRuntimeStatus.Stopping;

            var moduleInstance = runtimeInfo.InitializedPlugin?.ModuleInstance;
            if (moduleInstance != null)
            {
                using var timeoutCts = new CancellationTokenSource(_options.PluginStopTimeoutMs);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                await moduleInstance.OnStopAsync(combinedCts.Token);
            }

            runtimeInfo.Status = PluginRuntimeStatus.Stopped;
            runtimeInfo.StoppedAt = DateTime.UtcNow;

            _logger.LogDebug("Successfully stopped plugin: {PluginId}", runtimeInfo.PluginId);
        }
        catch (Exception ex)
        {
            runtimeInfo.Status = PluginRuntimeStatus.Failed;
            runtimeInfo.LastError = $"Stop failed: {ex.Message}";
            runtimeInfo.LastErrorAt = DateTime.UtcNow;

            _logger.LogError(ex, "Failed to stop plugin: {PluginId}", runtimeInfo.PluginId);
        }
    }

    private async void PerformHealthChecks(object? state)
    {
        if (_shutdownTokenSource.Token.IsCancellationRequested || !_isStarted)
        {
            return;
        }

        try
        {
            var healthCheckTasks = _pluginRuntimes.Values
                .Where(runtime => runtime.Status == PluginRuntimeStatus.Running)
                .Select(runtime => CheckPluginHealthAsync(runtime.PluginId, _shutdownTokenSource.Token))
                .ToArray();

            if (healthCheckTasks.Length > 0)
            {
                await Task.WhenAll(healthCheckTasks);
                _logger.LogTrace("Completed health checks for {PluginCount} plugins", healthCheckTasks.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during health check cycle");
        }
    }

    private async Task PerformInitialHealthChecks(CancellationToken cancellationToken)
    {
        try
        {
            // Wait a bit for plugins to stabilize
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

            // Perform initial health checks
            var healthCheckTasks = _pluginRuntimes.Values
                .Select(runtime => CheckPluginHealthAsync(runtime.PluginId, cancellationToken))
                .ToArray();

            if (healthCheckTasks.Length > 0)
            {
                await Task.WhenAll(healthCheckTasks);
                _logger.LogDebug("Completed initial health checks for {PluginCount} plugins", healthCheckTasks.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during initial health checks");
        }
    }

    private async void CheckConfigurationChanges(object? state)
    {
        if (_shutdownTokenSource.Token.IsCancellationRequested || !_isStarted)
        {
            return;
        }

        try
        {
            // TODO: Implement configuration change detection
            // This would typically watch configuration files or external configuration sources
            _logger.LogTrace("Configuration change check completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during configuration change check");
        }
    }

    /// <summary>
    /// Disposes the plugin manager and all managed resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            // Stop if not already stopped
            if (_isStarted)
            {
                await StopAsync(CancellationToken.None);
            }

            // Dispose timers
            _healthCheckTimer?.Dispose();
            _configurationWatchTimer?.Dispose();

            // Dispose cancellation token source
            _shutdownTokenSource.Dispose();

            _logger.LogDebug("Plugin manager disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing plugin manager");
        }
    }
}

/// <summary>
/// Interface for plugin manager service.
/// </summary>
public interface IPluginManager
{
    /// <summary>
    /// Gets the active plugins.
    /// </summary>
    IReadOnlyDictionary<string, PluginRuntimeInfo> ActivePlugins { get; }

    /// <summary>
    /// Gets whether the plugin manager is started.
    /// </summary>
    bool IsStarted { get; }

    /// <summary>
    /// Gets the current status of all managed plugins.
    /// </summary>
    /// <returns>The overall plugin status.</returns>
    PluginManagerStatus GetStatus();

    /// <summary>
    /// Gets the runtime information for a specific plugin.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <returns>The plugin runtime information, or null if not found.</returns>
    PluginRuntimeInfo? GetPluginInfo(string pluginId);

    /// <summary>
    /// Loads a specific plugin at runtime.
    /// </summary>
    /// <param name="assemblyPath">The path to the plugin assembly.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The plugin load result.</returns>
    Task<PluginLoadResult> LoadPluginAsync(string assemblyPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unloads a specific plugin at runtime.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The plugin unload result.</returns>
    Task<PluginUnloadResult> UnloadPluginAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reloads a specific plugin at runtime.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The plugin reload result.</returns>
    Task<PluginReloadResult> ReloadPluginAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the configuration for a specific plugin.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="newConfiguration">The new configuration values.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the configuration update operation.</returns>
    Task UpdatePluginConfigurationAsync(string pluginId, IReadOnlyDictionary<string, object> newConfiguration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a health check on a specific plugin.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The health check result.</returns>
    Task<ModuleHealthResult> CheckPluginHealthAsync(string pluginId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Runtime information for a plugin.
/// </summary>
public class PluginRuntimeInfo
{
    /// <summary>
    /// Gets or sets the plugin identifier.
    /// </summary>
    public string PluginId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the plugin version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the initialized plugin instance.
    /// </summary>
    public InitializedPlugin? InitializedPlugin { get; set; }

    /// <summary>
    /// Gets or sets the runtime status.
    /// </summary>
    public PluginRuntimeStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when the plugin was started.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Gets or sets when the plugin was stopped.
    /// </summary>
    public DateTime? StoppedAt { get; set; }

    /// <summary>
    /// Gets or sets the health status.
    /// </summary>
    public PluginHealthStatus HealthStatus { get; set; }

    /// <summary>
    /// Gets or sets when the last health check was performed.
    /// </summary>
    public DateTime LastHealthCheck { get; set; }

    /// <summary>
    /// Gets or sets the last health check result.
    /// </summary>
    public ModuleHealthResult? LastHealthResult { get; set; }

    /// <summary>
    /// Gets or sets the configuration version.
    /// </summary>
    public int ConfigurationVersion { get; set; }

    /// <summary>
    /// Gets or sets when the configuration was last updated.
    /// </summary>
    public DateTime? LastConfigurationUpdate { get; set; }

    /// <summary>
    /// Gets or sets the reload count.
    /// </summary>
    public int ReloadCount { get; set; }

    /// <summary>
    /// Gets or sets the last error message.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Gets or sets when the last error occurred.
    /// </summary>
    public DateTime? LastErrorAt { get; set; }

    /// <summary>
    /// Gets or sets additional runtime metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Overall status of the plugin manager.
/// </summary>
public class PluginManagerStatus
{
    /// <summary>
    /// Gets or sets whether the plugin manager is started.
    /// </summary>
    public bool IsStarted { get; set; }

    /// <summary>
    /// Gets or sets the total number of plugins.
    /// </summary>
    public int TotalPlugins { get; set; }

    /// <summary>
    /// Gets or sets the number of running plugins.
    /// </summary>
    public int RunningPlugins { get; set; }

    /// <summary>
    /// Gets or sets the number of failed plugins.
    /// </summary>
    public int FailedPlugins { get; set; }

    /// <summary>
    /// Gets or sets the number of stopped plugins.
    /// </summary>
    public int StoppedPlugins { get; set; }

    /// <summary>
    /// Gets or sets the number of healthy plugins.
    /// </summary>
    public int HealthyPlugins { get; set; }

    /// <summary>
    /// Gets or sets the number of unhealthy plugins.
    /// </summary>
    public int UnhealthyPlugins { get; set; }

    /// <summary>
    /// Gets or sets when the last health check was performed.
    /// </summary>
    public DateTime LastHealthCheck { get; set; }

    /// <summary>
    /// Gets or sets the plugin runtime information.
    /// </summary>
    public List<PluginRuntimeInfo> Plugins { get; set; } = new();
}

/// <summary>
/// Plugin runtime status enumeration.
/// </summary>
public enum PluginRuntimeStatus
{
    Unknown,
    Starting,
    Running,
    Stopping,
    Stopped,
    Failed,
    Reloading
}

/// <summary>
/// Plugin health status enumeration.
/// </summary>
public enum PluginHealthStatus
{
    Unknown,
    Healthy,
    Degraded,
    Unhealthy
}

/// <summary>
/// Configuration options for plugin manager.
/// </summary>
public class PluginManagerOptions
{
    /// <summary>
    /// Gets or sets whether to enable health checks.
    /// </summary>
    public bool EnableHealthChecks { get; set; } = true;

    /// <summary>
    /// Gets or sets the health check interval in milliseconds.
    /// </summary>
    public int HealthCheckIntervalMs { get; set; } = 30000;

    /// <summary>
    /// Gets or sets whether to enable configuration watching.
    /// </summary>
    public bool EnableConfigurationWatch { get; set; } = true;

    /// <summary>
    /// Gets or sets the configuration watch interval in milliseconds.
    /// </summary>
    public int ConfigurationWatchIntervalMs { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the plugin stop timeout in milliseconds.
    /// </summary>
    public int PluginStopTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Gets or sets whether to auto-restart failed plugins.
    /// </summary>
    public bool AutoRestartFailedPlugins { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum number of restart attempts.
    /// </summary>
    public int MaxRestartAttempts { get; set; } = 3;
}