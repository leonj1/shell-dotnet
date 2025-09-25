using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using DotNetShell.Abstractions;

namespace DotNetShell.Core.Plugins;

/// <summary>
/// Main plugin loading orchestrator that coordinates discovery, validation, loading, and initialization of plugins.
/// </summary>
public class PluginLoader : IPluginLoader
{
    private readonly PluginLoaderOptions _options;
    private readonly ILogger<PluginLoader> _logger;
    private readonly IPluginDiscoveryService _discoveryService;
    private readonly IPluginValidator _validator;
    private readonly IPluginMetadataReader _metadataReader;
    private readonly IPluginInitializer _initializer;

    private readonly ConcurrentDictionary<string, LoadedPlugin> _loadedPlugins = new();
    private readonly ConcurrentDictionary<string, InitializedPlugin> _initializedPlugins = new();
    private readonly object _loadLock = new();

    /// <summary>
    /// Gets the loaded plugins.
    /// </summary>
    public IReadOnlyDictionary<string, LoadedPlugin> LoadedPlugins => _loadedPlugins.AsReadOnly();

    /// <summary>
    /// Gets the initialized plugins.
    /// </summary>
    public IReadOnlyDictionary<string, InitializedPlugin> InitializedPlugins => _initializedPlugins.AsReadOnly();

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginLoader"/> class.
    /// </summary>
    /// <param name="options">Plugin loader configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="discoveryService">Plugin discovery service.</param>
    /// <param name="validator">Plugin validator.</param>
    /// <param name="metadataReader">Plugin metadata reader.</param>
    /// <param name="initializer">Plugin initializer.</param>
    public PluginLoader(
        IOptions<PluginLoaderOptions> options,
        ILogger<PluginLoader> logger,
        IPluginDiscoveryService discoveryService,
        IPluginValidator validator,
        IPluginMetadataReader metadataReader,
        IPluginInitializer initializer)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _metadataReader = metadataReader ?? throw new ArgumentNullException(nameof(metadataReader));
        _initializer = initializer ?? throw new ArgumentNullException(nameof(initializer));
    }

    /// <summary>
    /// Discovers and loads all plugins from configured sources.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The plugin loading results.</returns>
    public async Task<PluginLoadingResults> LoadAllPluginsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting plugin loading process");

        var stopwatch = Stopwatch.StartNew();
        var results = new PluginLoadingResults
        {
            StartTime = DateTime.UtcNow
        };

        try
        {
            // Phase 1: Discovery
            var discoveredPlugins = await DiscoverPluginsAsync(results, cancellationToken);
            if (discoveredPlugins.Count == 0)
            {
                _logger.LogInformation("No plugins discovered");
                return results;
            }

            // Phase 2: Validation
            var validPlugins = await ValidatePluginsAsync(discoveredPlugins, results, cancellationToken);
            if (validPlugins.Count == 0)
            {
                _logger.LogWarning("No valid plugins found after validation");
                return results;
            }

            // Phase 3: Dependency Resolution
            var sortedPlugins = await ResolveDependenciesAsync(validPlugins, results, cancellationToken);

            // Phase 4: Loading
            var loadedPlugins = await LoadPluginAssembliesAsync(sortedPlugins, results, cancellationToken);

            // Phase 5: Initialization
            await InitializePluginsAsync(loadedPlugins, results, cancellationToken);

            stopwatch.Stop();
            results.TotalDuration = stopwatch.Elapsed;
            results.IsCompleted = true;

            _logger.LogInformation("Plugin loading completed in {Duration}ms. " +
                                  "Discovered: {Discovered}, Valid: {Valid}, Loaded: {Loaded}, Initialized: {Initialized}, Failed: {Failed}",
                stopwatch.ElapsedMilliseconds,
                results.DiscoveredPlugins.Count,
                results.ValidatedPlugins.Count,
                results.LoadedPlugins.Count,
                results.InitializedPlugins.Count,
                results.FailedPlugins.Count);

            return results;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            results.TotalDuration = stopwatch.Elapsed;
            results.IsCompleted = true;
            results.GlobalErrors.Add($"Plugin loading failed with exception: {ex.Message}");

            _logger.LogError(ex, "Plugin loading process failed");

            return results;
        }
    }

    /// <summary>
    /// Loads a specific plugin by its assembly path.
    /// </summary>
    /// <param name="assemblyPath">The path to the plugin assembly.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The plugin loading result.</returns>
    public async Task<PluginLoadResult> LoadPluginAsync(string assemblyPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyPath);

        _logger.LogInformation("Loading individual plugin from: {AssemblyPath}", assemblyPath);

        var result = new PluginLoadResult
        {
            AssemblyPath = assemblyPath,
            StartTime = DateTime.UtcNow
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Phase 1: Discovery
            var discoveredPlugin = await _discoveryService.DiscoverFromAssemblyAsync(assemblyPath, cancellationToken);
            if (discoveredPlugin == null)
            {
                result.Errors.Add("Failed to discover plugin from assembly");
                return result;
            }

            result.DiscoveredPlugin = discoveredPlugin;

            // Phase 2: Validation
            var validationResult = await _validator.ValidatePluginAsync(discoveredPlugin, cancellationToken);
            result.ValidationResult = validationResult;

            if (!validationResult.IsValid)
            {
                result.Errors.AddRange(validationResult.Errors.Select(e => $"Validation: {e}"));
                return result;
            }

            // Phase 3: Loading
            var loadedPlugin = await LoadPluginAssemblyAsync(discoveredPlugin, cancellationToken);
            if (loadedPlugin == null)
            {
                result.Errors.Add("Failed to load plugin assembly");
                return result;
            }

            result.LoadedPlugin = loadedPlugin;

            // Phase 4: Initialization
            var initResult = await _initializer.InitializePluginAsync(loadedPlugin, cancellationToken);
            result.InitializationResult = initResult;

            if (!initResult.IsSuccessful)
            {
                result.Errors.AddRange(initResult.Errors.Select(e => $"Initialization: {e}"));
                return result;
            }

            // Register as initialized
            var initializedPlugin = new InitializedPlugin
            {
                PluginId = discoveredPlugin.Manifest.Id,
                Version = discoveredPlugin.Manifest.Version,
                ModuleInstance = initResult.ModuleInstance,
                ModuleServiceProvider = initResult.ModuleServiceProvider,
                LoadContext = loadedPlugin.LoadContext,
                InitializationResult = initResult
            };

            _initializedPlugins.TryAdd(initializedPlugin.PluginId, initializedPlugin);
            result.InitializedPlugin = initializedPlugin;
            result.IsSuccessful = true;

            stopwatch.Stop();
            result.LoadDuration = stopwatch.Elapsed;

            _logger.LogInformation("Successfully loaded plugin: {PluginId} v{Version} in {Duration}ms",
                discoveredPlugin.Manifest.Id, discoveredPlugin.Manifest.Version, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.LoadDuration = stopwatch.Elapsed;
            result.Errors.Add($"Loading failed with exception: {ex.Message}");

            _logger.LogError(ex, "Failed to load plugin from: {AssemblyPath}", assemblyPath);

            return result;
        }
    }

    /// <summary>
    /// Unloads a specific plugin.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The plugin unload result.</returns>
    public async Task<PluginUnloadResult> UnloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(pluginId);

        _logger.LogInformation("Unloading plugin: {PluginId}", pluginId);

        var result = new PluginUnloadResult
        {
            PluginId = pluginId,
            StartTime = DateTime.UtcNow
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Find initialized plugin
            if (!_initializedPlugins.TryGetValue(pluginId, out var initializedPlugin))
            {
                result.Warnings.Add("Plugin is not currently initialized");
                return result;
            }

            // Uninitialize plugin
            var uninitResult = await _initializer.UninitializePluginAsync(initializedPlugin, cancellationToken);
            result.UninitializationResult = uninitResult;

            if (uninitResult.Warnings.Count > 0)
            {
                result.Warnings.AddRange(uninitResult.Warnings);
            }

            // Remove from collections
            _initializedPlugins.TryRemove(pluginId, out _);
            _loadedPlugins.TryRemove(pluginId, out _);

            result.IsSuccessful = true;

            stopwatch.Stop();
            result.UnloadDuration = stopwatch.Elapsed;

            _logger.LogInformation("Successfully unloaded plugin: {PluginId} in {Duration}ms",
                pluginId, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.UnloadDuration = stopwatch.Elapsed;
            result.Warnings.Add($"Unloading failed with exception: {ex.Message}");

            _logger.LogError(ex, "Failed to unload plugin: {PluginId}", pluginId);

            return result;
        }
    }

    /// <summary>
    /// Unloads all loaded plugins.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The unload results for all plugins.</returns>
    public async Task<IEnumerable<PluginUnloadResult>> UnloadAllPluginsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Unloading all plugins");

        var results = new List<PluginUnloadResult>();
        var pluginIds = _initializedPlugins.Keys.ToArray();

        foreach (var pluginId in pluginIds)
        {
            var result = await UnloadPluginAsync(pluginId, cancellationToken);
            results.Add(result);
        }

        _logger.LogInformation("Completed unloading {PluginCount} plugins", results.Count);

        return results;
    }

    /// <summary>
    /// Reloads a specific plugin.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The plugin reload result.</returns>
    public async Task<PluginReloadResult> ReloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(pluginId);

        _logger.LogInformation("Reloading plugin: {PluginId}", pluginId);

        var result = new PluginReloadResult
        {
            PluginId = pluginId,
            StartTime = DateTime.UtcNow
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Get current plugin info
            if (!_loadedPlugins.TryGetValue(pluginId, out var loadedPlugin))
            {
                result.Errors.Add("Plugin is not currently loaded");
                return result;
            }

            var assemblyPath = loadedPlugin.LoadContext.PluginPath;

            // Phase 1: Unload current plugin
            var unloadResult = await UnloadPluginAsync(pluginId, cancellationToken);
            result.UnloadResult = unloadResult;

            // Phase 2: Load new version
            var loadResult = await LoadPluginAsync(assemblyPath, cancellationToken);
            result.LoadResult = loadResult;

            result.IsSuccessful = loadResult.IsSuccessful;

            if (!result.IsSuccessful)
            {
                result.Errors.AddRange(loadResult.Errors);
            }

            stopwatch.Stop();
            result.ReloadDuration = stopwatch.Elapsed;

            if (result.IsSuccessful)
            {
                _logger.LogInformation("Successfully reloaded plugin: {PluginId} in {Duration}ms",
                    pluginId, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogError("Failed to reload plugin: {PluginId}. Errors: {Errors}",
                    pluginId, string.Join(", ", result.Errors));
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.ReloadDuration = stopwatch.Elapsed;
            result.Errors.Add($"Reload failed with exception: {ex.Message}");

            _logger.LogError(ex, "Failed to reload plugin: {PluginId}", pluginId);

            return result;
        }
    }

    /// <summary>
    /// Gets the status of a specific plugin.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <returns>The plugin status.</returns>
    public PluginStatus GetPluginStatus(string pluginId)
    {
        ArgumentException.ThrowIfNullOrEmpty(pluginId);

        if (_initializedPlugins.ContainsKey(pluginId))
        {
            return PluginStatus.Initialized;
        }

        if (_loadedPlugins.ContainsKey(pluginId))
        {
            return PluginStatus.Loaded;
        }

        return PluginStatus.NotLoaded;
    }

    /// <summary>
    /// Gets overall plugin loading statistics.
    /// </summary>
    /// <returns>The plugin loading statistics.</returns>
    public PluginLoadingStatistics GetStatistics()
    {
        return new PluginLoadingStatistics
        {
            LoadedPluginsCount = _loadedPlugins.Count,
            InitializedPluginsCount = _initializedPlugins.Count,
            PluginDetails = _initializedPlugins.Values.Select(p => new PluginStatistics
            {
                PluginId = p.PluginId,
                Version = p.Version,
                InitializedAt = p.InitializedAt,
                Status = PluginStatus.Initialized
            }).ToList()
        };
    }

    private async Task<List<DiscoveredPlugin>> DiscoverPluginsAsync(
        PluginLoadingResults results,
        CancellationToken cancellationToken)
    {
        try
        {
            var discoveredPlugins = await _discoveryService.DiscoverPluginsAsync(cancellationToken);
            var pluginList = discoveredPlugins.ToList();

            results.DiscoveredPlugins.AddRange(pluginList);

            _logger.LogDebug("Discovered {PluginCount} plugins", pluginList.Count);

            return pluginList;
        }
        catch (Exception ex)
        {
            results.GlobalErrors.Add($"Plugin discovery failed: {ex.Message}");
            _logger.LogError(ex, "Plugin discovery failed");
            return new List<DiscoveredPlugin>();
        }
    }

    private async Task<List<DiscoveredPlugin>> ValidatePluginsAsync(
        List<DiscoveredPlugin> discoveredPlugins,
        PluginLoadingResults results,
        CancellationToken cancellationToken)
    {
        var validPlugins = new List<DiscoveredPlugin>();

        foreach (var plugin in discoveredPlugins)
        {
            try
            {
                var validationResult = await _validator.ValidatePluginAsync(plugin, cancellationToken);

                if (validationResult.IsValid)
                {
                    validPlugins.Add(plugin);
                    results.ValidatedPlugins.Add(plugin);
                }
                else
                {
                    results.FailedPlugins.Add(new FailedPlugin
                    {
                        PluginId = plugin.Manifest.Id,
                        Version = plugin.Manifest.Version,
                        FailureReason = string.Join(", ", validationResult.Errors),
                        FailurePhase = "Validation"
                    });
                }
            }
            catch (Exception ex)
            {
                results.FailedPlugins.Add(new FailedPlugin
                {
                    PluginId = plugin.Manifest.Id,
                    Version = plugin.Manifest.Version,
                    FailureReason = $"Validation exception: {ex.Message}",
                    FailurePhase = "Validation"
                });
            }
        }

        _logger.LogDebug("Validated plugins: {ValidCount}/{TotalCount}",
            validPlugins.Count, discoveredPlugins.Count);

        return validPlugins;
    }

    private async Task<List<DiscoveredPlugin>> ResolveDependenciesAsync(
        List<DiscoveredPlugin> validPlugins,
        PluginLoadingResults results,
        CancellationToken cancellationToken)
    {
        // Simple dependency resolution - sort by dependencies
        // In a real implementation, this would be more sophisticated
        var sortedPlugins = validPlugins
            .OrderBy(p => p.Manifest.Dependencies.Count)
            .ToList();

        _logger.LogDebug("Resolved plugin dependencies, loading order established");

        return sortedPlugins;
    }

    private async Task<List<LoadedPlugin>> LoadPluginAssembliesAsync(
        List<DiscoveredPlugin> sortedPlugins,
        PluginLoadingResults results,
        CancellationToken cancellationToken)
    {
        var loadedPlugins = new List<LoadedPlugin>();

        foreach (var plugin in sortedPlugins)
        {
            try
            {
                var loadedPlugin = await LoadPluginAssemblyAsync(plugin, cancellationToken);
                if (loadedPlugin != null)
                {
                    loadedPlugins.Add(loadedPlugin);
                    results.LoadedPlugins.Add(loadedPlugin);
                    _loadedPlugins.TryAdd(plugin.Manifest.Id, loadedPlugin);
                }
                else
                {
                    results.FailedPlugins.Add(new FailedPlugin
                    {
                        PluginId = plugin.Manifest.Id,
                        Version = plugin.Manifest.Version,
                        FailureReason = "Failed to load assembly",
                        FailurePhase = "Assembly Loading"
                    });
                }
            }
            catch (Exception ex)
            {
                results.FailedPlugins.Add(new FailedPlugin
                {
                    PluginId = plugin.Manifest.Id,
                    Version = plugin.Manifest.Version,
                    FailureReason = $"Loading exception: {ex.Message}",
                    FailurePhase = "Assembly Loading"
                });
            }
        }

        _logger.LogDebug("Loaded assemblies: {LoadedCount}/{TotalCount}",
            loadedPlugins.Count, sortedPlugins.Count);

        return loadedPlugins;
    }

    private async Task<LoadedPlugin?> LoadPluginAssemblyAsync(
        DiscoveredPlugin discoveredPlugin,
        CancellationToken cancellationToken)
    {
        try
        {
            // Create plugin load context
            var loadContext = new PluginLoadContext(
                discoveredPlugin.AssemblyPath,
                $"Plugin_{discoveredPlugin.Manifest.Id}",
                isCollectible: _options.EnablePluginUnloading,
                _logger);

            // Load the assembly
            var assembly = loadContext.LoadPluginAssembly();

            // Read metadata
            var metadata = await _metadataReader.ReadMetadataAsync(discoveredPlugin.AssemblyPath, cancellationToken);

            var loadedPlugin = new LoadedPlugin
            {
                Manifest = discoveredPlugin.Manifest,
                Assembly = assembly,
                LoadContext = loadContext,
                Metadata = metadata
            };

            return loadedPlugin;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load plugin assembly: {PluginId}", discoveredPlugin.Manifest.Id);
            return null;
        }
    }

    private async Task InitializePluginsAsync(
        List<LoadedPlugin> loadedPlugins,
        PluginLoadingResults results,
        CancellationToken cancellationToken)
    {
        foreach (var loadedPlugin in loadedPlugins)
        {
            try
            {
                var initResult = await _initializer.InitializePluginAsync(loadedPlugin, cancellationToken);

                if (initResult.IsSuccessful)
                {
                    var initializedPlugin = new InitializedPlugin
                    {
                        PluginId = loadedPlugin.Manifest.Id,
                        Version = loadedPlugin.Manifest.Version,
                        ModuleInstance = initResult.ModuleInstance,
                        ModuleServiceProvider = initResult.ModuleServiceProvider,
                        LoadContext = loadedPlugin.LoadContext,
                        InitializationResult = initResult
                    };

                    _initializedPlugins.TryAdd(initializedPlugin.PluginId, initializedPlugin);
                    results.InitializedPlugins.Add(initializedPlugin);
                }
                else
                {
                    results.FailedPlugins.Add(new FailedPlugin
                    {
                        PluginId = loadedPlugin.Manifest.Id,
                        Version = loadedPlugin.Manifest.Version,
                        FailureReason = string.Join(", ", initResult.Errors),
                        FailurePhase = "Initialization"
                    });
                }
            }
            catch (Exception ex)
            {
                results.FailedPlugins.Add(new FailedPlugin
                {
                    PluginId = loadedPlugin.Manifest.Id,
                    Version = loadedPlugin.Manifest.Version,
                    FailureReason = $"Initialization exception: {ex.Message}",
                    FailurePhase = "Initialization"
                });
            }
        }

        _logger.LogDebug("Initialized plugins: {InitializedCount}/{TotalCount}",
            results.InitializedPlugins.Count, loadedPlugins.Count);
    }
}

/// <summary>
/// Interface for plugin loader service.
/// </summary>
public interface IPluginLoader
{
    /// <summary>
    /// Gets the loaded plugins.
    /// </summary>
    IReadOnlyDictionary<string, LoadedPlugin> LoadedPlugins { get; }

    /// <summary>
    /// Gets the initialized plugins.
    /// </summary>
    IReadOnlyDictionary<string, InitializedPlugin> InitializedPlugins { get; }

    /// <summary>
    /// Discovers and loads all plugins from configured sources.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The plugin loading results.</returns>
    Task<PluginLoadingResults> LoadAllPluginsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a specific plugin by its assembly path.
    /// </summary>
    /// <param name="assemblyPath">The path to the plugin assembly.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The plugin loading result.</returns>
    Task<PluginLoadResult> LoadPluginAsync(string assemblyPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unloads a specific plugin.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The plugin unload result.</returns>
    Task<PluginUnloadResult> UnloadPluginAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unloads all loaded plugins.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The unload results for all plugins.</returns>
    Task<IEnumerable<PluginUnloadResult>> UnloadAllPluginsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reloads a specific plugin.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The plugin reload result.</returns>
    Task<PluginReloadResult> ReloadPluginAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of a specific plugin.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <returns>The plugin status.</returns>
    PluginStatus GetPluginStatus(string pluginId);

    /// <summary>
    /// Gets overall plugin loading statistics.
    /// </summary>
    /// <returns>The plugin loading statistics.</returns>
    PluginLoadingStatistics GetStatistics();
}

/// <summary>
/// Results of loading all plugins.
/// </summary>
public class PluginLoadingResults
{
    /// <summary>
    /// Gets or sets the loading start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the total duration.
    /// </summary>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    /// Gets or sets whether the loading process is completed.
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// Gets the discovered plugins.
    /// </summary>
    public List<DiscoveredPlugin> DiscoveredPlugins { get; } = new();

    /// <summary>
    /// Gets the validated plugins.
    /// </summary>
    public List<DiscoveredPlugin> ValidatedPlugins { get; } = new();

    /// <summary>
    /// Gets the loaded plugins.
    /// </summary>
    public List<LoadedPlugin> LoadedPlugins { get; } = new();

    /// <summary>
    /// Gets the initialized plugins.
    /// </summary>
    public List<InitializedPlugin> InitializedPlugins { get; } = new();

    /// <summary>
    /// Gets the failed plugins.
    /// </summary>
    public List<FailedPlugin> FailedPlugins { get; } = new();

    /// <summary>
    /// Gets global errors that occurred during the loading process.
    /// </summary>
    public List<string> GlobalErrors { get; } = new();
}

/// <summary>
/// Result of loading a single plugin.
/// </summary>
public class PluginLoadResult
{
    /// <summary>
    /// Gets or sets the assembly path.
    /// </summary>
    public string AssemblyPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the loading start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the loading duration.
    /// </summary>
    public TimeSpan LoadDuration { get; set; }

    /// <summary>
    /// Gets or sets whether loading was successful.
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Gets the loading errors.
    /// </summary>
    public List<string> Errors { get; } = new();

    /// <summary>
    /// Gets or sets the discovered plugin.
    /// </summary>
    public DiscoveredPlugin? DiscoveredPlugin { get; set; }

    /// <summary>
    /// Gets or sets the validation result.
    /// </summary>
    public PluginValidationResult? ValidationResult { get; set; }

    /// <summary>
    /// Gets or sets the loaded plugin.
    /// </summary>
    public LoadedPlugin? LoadedPlugin { get; set; }

    /// <summary>
    /// Gets or sets the initialization result.
    /// </summary>
    public PluginInitializationResult? InitializationResult { get; set; }

    /// <summary>
    /// Gets or sets the initialized plugin.
    /// </summary>
    public InitializedPlugin? InitializedPlugin { get; set; }
}

/// <summary>
/// Result of unloading a plugin.
/// </summary>
public class PluginUnloadResult
{
    /// <summary>
    /// Gets or sets the plugin identifier.
    /// </summary>
    public string PluginId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unloading start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the unloading duration.
    /// </summary>
    public TimeSpan UnloadDuration { get; set; }

    /// <summary>
    /// Gets or sets whether unloading was successful.
    /// </summary>
    public bool IsSuccessful { get; set; } = true;

    /// <summary>
    /// Gets the unloading warnings.
    /// </summary>
    public List<string> Warnings { get; } = new();

    /// <summary>
    /// Gets or sets the uninitialization result.
    /// </summary>
    public PluginUninitializationResult? UninitializationResult { get; set; }
}

/// <summary>
/// Result of reloading a plugin.
/// </summary>
public class PluginReloadResult
{
    /// <summary>
    /// Gets or sets the plugin identifier.
    /// </summary>
    public string PluginId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the reloading start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the reloading duration.
    /// </summary>
    public TimeSpan ReloadDuration { get; set; }

    /// <summary>
    /// Gets or sets whether reloading was successful.
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Gets the reloading errors.
    /// </summary>
    public List<string> Errors { get; } = new();

    /// <summary>
    /// Gets or sets the unload result.
    /// </summary>
    public PluginUnloadResult? UnloadResult { get; set; }

    /// <summary>
    /// Gets or sets the load result.
    /// </summary>
    public PluginLoadResult? LoadResult { get; set; }
}

/// <summary>
/// Information about a failed plugin.
/// </summary>
public class FailedPlugin
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
    /// Gets or sets the failure reason.
    /// </summary>
    public string FailureReason { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the phase where the failure occurred.
    /// </summary>
    public string FailurePhase { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the failure timestamp.
    /// </summary>
    public DateTime FailedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Plugin status enumeration.
/// </summary>
public enum PluginStatus
{
    NotLoaded,
    Discovered,
    Validated,
    Loaded,
    Initialized,
    Failed
}

/// <summary>
/// Plugin loading statistics.
/// </summary>
public class PluginLoadingStatistics
{
    /// <summary>
    /// Gets or sets the number of loaded plugins.
    /// </summary>
    public int LoadedPluginsCount { get; set; }

    /// <summary>
    /// Gets or sets the number of initialized plugins.
    /// </summary>
    public int InitializedPluginsCount { get; set; }

    /// <summary>
    /// Gets or sets the plugin details.
    /// </summary>
    public List<PluginStatistics> PluginDetails { get; set; } = new();
}

/// <summary>
/// Statistics for a specific plugin.
/// </summary>
public class PluginStatistics
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
    /// Gets or sets when the plugin was initialized.
    /// </summary>
    public DateTime InitializedAt { get; set; }

    /// <summary>
    /// Gets or sets the plugin status.
    /// </summary>
    public PluginStatus Status { get; set; }
}

/// <summary>
/// Configuration options for plugin loader.
/// </summary>
public class PluginLoaderOptions
{
    /// <summary>
    /// Gets or sets whether to enable plugin unloading.
    /// </summary>
    public bool EnablePluginUnloading { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to continue loading other plugins if one fails.
    /// </summary>
    public bool ContinueOnFailure { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of plugins to load concurrently.
    /// </summary>
    public int MaxConcurrentLoads { get; set; } = 5;

    /// <summary>
    /// Gets or sets the plugin loading timeout in milliseconds.
    /// </summary>
    public int LoadingTimeoutMs { get; set; } = 120000;
}