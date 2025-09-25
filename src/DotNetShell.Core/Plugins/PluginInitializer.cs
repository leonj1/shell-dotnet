using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using DotNetShell.Core.DependencyInjection;
using DotNetShell.Abstractions;
using System.Diagnostics;

namespace DotNetShell.Core.Plugins;

/// <summary>
/// Service responsible for initializing loaded plugins through their lifecycle phases.
/// </summary>
public class PluginInitializer : IPluginInitializer
{
    private readonly PluginInitializationOptions _options;
    private readonly ILogger<PluginInitializer> _logger;
    private readonly IServiceProvider _shellServiceProvider;
    private readonly ModuleServiceProviderFactory _serviceProviderFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginInitializer"/> class.
    /// </summary>
    /// <param name="options">Plugin initialization configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="shellServiceProvider">The shell's service provider.</param>
    /// <param name="serviceProviderFactory">Factory for creating module service providers.</param>
    public PluginInitializer(
        IOptions<PluginInitializationOptions> options,
        ILogger<PluginInitializer> logger,
        IServiceProvider shellServiceProvider,
        ModuleServiceProviderFactory serviceProviderFactory)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _shellServiceProvider = shellServiceProvider ?? throw new ArgumentNullException(nameof(shellServiceProvider));
        _serviceProviderFactory = serviceProviderFactory ?? throw new ArgumentNullException(nameof(serviceProviderFactory));
    }

    /// <summary>
    /// Initializes a plugin through all lifecycle phases.
    /// </summary>
    /// <param name="loadedPlugin">The loaded plugin to initialize.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The initialization result.</returns>
    public async Task<PluginInitializationResult> InitializePluginAsync(
        LoadedPlugin loadedPlugin,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loadedPlugin);

        _logger.LogInformation("Starting initialization for plugin: {PluginId} v{Version}",
            loadedPlugin.Manifest.Id, loadedPlugin.Manifest.Version);

        var stopwatch = Stopwatch.StartNew();
        var result = new PluginInitializationResult
        {
            PluginId = loadedPlugin.Manifest.Id,
            Version = loadedPlugin.Manifest.Version,
            StartTime = DateTime.UtcNow
        };

        try
        {
            // Phase 1: Validate plugin before initialization
            await ValidatePluginAsync(loadedPlugin, result, cancellationToken);
            if (!result.IsSuccessful)
            {
                return result;
            }

            // Phase 2: Create module instance
            await CreateModuleInstanceAsync(loadedPlugin, result, cancellationToken);
            if (!result.IsSuccessful || result.ModuleInstance == null)
            {
                return result;
            }

            // Phase 3: Validate module instance
            await ValidateModuleInstanceAsync(loadedPlugin, result, cancellationToken);
            if (!result.IsSuccessful)
            {
                return result;
            }

            // Phase 4: Initialize module services
            await InitializeServicesAsync(loadedPlugin, result, cancellationToken);
            if (!result.IsSuccessful)
            {
                return result;
            }

            // Phase 5: Configure module
            await ConfigureModuleAsync(loadedPlugin, result, cancellationToken);
            if (!result.IsSuccessful)
            {
                return result;
            }

            // Phase 6: Start module
            await StartModuleAsync(loadedPlugin, result, cancellationToken);

            stopwatch.Stop();
            result.InitializationDuration = stopwatch.Elapsed;

            if (result.IsSuccessful)
            {
                _logger.LogInformation("Successfully initialized plugin: {PluginId} v{Version} in {Duration}ms",
                    loadedPlugin.Manifest.Id, loadedPlugin.Manifest.Version, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogError("Failed to initialize plugin: {PluginId} v{Version}. Errors: {Errors}",
                    loadedPlugin.Manifest.Id, loadedPlugin.Manifest.Version, string.Join(", ", result.Errors));
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.IsSuccessful = false;
            result.Errors.Add($"Initialization failed with exception: {ex.Message}");
            result.InitializationDuration = stopwatch.Elapsed;

            _logger.LogError(ex, "Exception during plugin initialization: {PluginId} v{Version}",
                loadedPlugin.Manifest.Id, loadedPlugin.Manifest.Version);

            return result;
        }
    }

    /// <summary>
    /// Uninitializes a plugin, calling its cleanup methods.
    /// </summary>
    /// <param name="initializedPlugin">The initialized plugin to uninitialize.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The uninitialization result.</returns>
    public async Task<PluginUninitializationResult> UninitializePluginAsync(
        InitializedPlugin initializedPlugin,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(initializedPlugin);

        _logger.LogInformation("Starting uninitialization for plugin: {PluginId} v{Version}",
            initializedPlugin.PluginId, initializedPlugin.Version);

        var stopwatch = Stopwatch.StartNew();
        var result = new PluginUninitializationResult
        {
            PluginId = initializedPlugin.PluginId,
            Version = initializedPlugin.Version,
            StartTime = DateTime.UtcNow
        };

        try
        {
            // Phase 1: Stop module
            await StopModuleAsync(initializedPlugin, result, cancellationToken);

            // Phase 2: Unload module
            await UnloadModuleAsync(initializedPlugin, result, cancellationToken);

            // Phase 3: Dispose services
            await DisposeServicesAsync(initializedPlugin, result, cancellationToken);

            // Phase 4: Clean up load context
            await CleanupLoadContextAsync(initializedPlugin, result, cancellationToken);

            stopwatch.Stop();
            result.UninitializationDuration = stopwatch.Elapsed;

            if (result.IsSuccessful)
            {
                _logger.LogInformation("Successfully uninitialized plugin: {PluginId} v{Version} in {Duration}ms",
                    initializedPlugin.PluginId, initializedPlugin.Version, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogWarning("Plugin uninitialization completed with warnings: {PluginId} v{Version}. Warnings: {Warnings}",
                    initializedPlugin.PluginId, initializedPlugin.Version, string.Join(", ", result.Warnings));
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Warnings.Add($"Uninitialization failed with exception: {ex.Message}");
            result.UninitializationDuration = stopwatch.Elapsed;

            _logger.LogError(ex, "Exception during plugin uninitialization: {PluginId} v{Version}",
                initializedPlugin.PluginId, initializedPlugin.Version);

            return result;
        }
    }

    private async Task ValidatePluginAsync(LoadedPlugin loadedPlugin, PluginInitializationResult result, CancellationToken cancellationToken)
    {
        result.CurrentPhase = InitializationPhase.Validation;

        try
        {
            // Validate the plugin is properly loaded
            if (loadedPlugin.LoadContext == null)
            {
                result.Errors.Add("Plugin load context is null");
                result.IsSuccessful = false;
                return;
            }

            if (loadedPlugin.Assembly == null)
            {
                result.Errors.Add("Plugin assembly is null");
                result.IsSuccessful = false;
                return;
            }

            // Validate the manifest
            var manifestErrors = loadedPlugin.Manifest.Validate().ToList();
            if (manifestErrors.Count > 0)
            {
                result.Errors.AddRange(manifestErrors.Select(e => $"Manifest validation: {e}"));
                result.IsSuccessful = false;
                return;
            }

            _logger.LogDebug("Plugin validation passed: {PluginId}", loadedPlugin.Manifest.Id);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Validation phase failed: {ex.Message}");
            result.IsSuccessful = false;
        }
    }

    private async Task CreateModuleInstanceAsync(LoadedPlugin loadedPlugin, PluginInitializationResult result, CancellationToken cancellationToken)
    {
        result.CurrentPhase = InitializationPhase.ModuleCreation;

        try
        {
            // Find the entry point type
            var entryPointType = loadedPlugin.Assembly.GetType(loadedPlugin.Manifest.EntryPoint);
            if (entryPointType == null)
            {
                result.Errors.Add($"Entry point type not found: {loadedPlugin.Manifest.EntryPoint}");
                result.IsSuccessful = false;
                return;
            }

            // Create instance
            var instance = Activator.CreateInstance(entryPointType) as IBusinessLogicModule;
            if (instance == null)
            {
                result.Errors.Add($"Failed to create instance of {loadedPlugin.Manifest.EntryPoint} or it doesn't implement IBusinessLogicModule");
                result.IsSuccessful = false;
                return;
            }

            result.ModuleInstance = instance;

            _logger.LogDebug("Created module instance for plugin: {PluginId} ({TypeName})",
                loadedPlugin.Manifest.Id, entryPointType.FullName);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Module creation failed: {ex.Message}");
            result.IsSuccessful = false;
        }
    }

    private async Task ValidateModuleInstanceAsync(LoadedPlugin loadedPlugin, PluginInitializationResult result, CancellationToken cancellationToken)
    {
        result.CurrentPhase = InitializationPhase.ModuleValidation;

        try
        {
            if (result.ModuleInstance == null)
            {
                result.Errors.Add("Module instance is null");
                result.IsSuccessful = false;
                return;
            }

            // Create validation context
            var validationContext = new ModuleValidationContext
            {
                ShellVersion = _options.ShellVersion,
                Environment = _options.Environment,
                AvailableServices = GetAvailableServiceTypes(),
                Configuration = GetModuleConfiguration(loadedPlugin.Manifest.Id)
            };

            // Call module validation
            using var timeoutCts = new CancellationTokenSource(_options.ValidationTimeoutMs);
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var validationResult = await result.ModuleInstance.ValidateAsync(validationContext, combinedCts.Token);

            if (!validationResult.IsValid)
            {
                result.Errors.AddRange(validationResult.Errors.Select(e => $"Module validation: {e}"));
                result.IsSuccessful = false;
                return;
            }

            if (validationResult.Warnings.Count > 0)
            {
                result.Warnings.AddRange(validationResult.Warnings.Select(w => $"Module validation: {w}"));
            }

            _logger.LogDebug("Module validation passed for plugin: {PluginId}", loadedPlugin.Manifest.Id);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Module validation failed: {ex.Message}");
            result.IsSuccessful = false;
        }
    }

    private async Task InitializeServicesAsync(LoadedPlugin loadedPlugin, PluginInitializationResult result, CancellationToken cancellationToken)
    {
        result.CurrentPhase = InitializationPhase.ServiceInitialization;

        try
        {
            if (result.ModuleInstance == null)
            {
                result.Errors.Add("Module instance is null during service initialization");
                result.IsSuccessful = false;
                return;
            }

            // Create module-specific service collection
            var moduleServices = new ServiceCollection();

            // Call module's service registration
            using var timeoutCts = new CancellationTokenSource(_options.InitializationTimeoutMs);
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await result.ModuleInstance.OnInitializeAsync(moduleServices, combinedCts.Token);

            // Create module service provider
            result.ModuleServiceProvider = _serviceProviderFactory.CreateModuleProvider(
                loadedPlugin.Manifest.Id,
                _shellServiceProvider,
                moduleServices);

            _logger.LogDebug("Initialized services for plugin: {PluginId} ({ServiceCount} services registered)",
                loadedPlugin.Manifest.Id, moduleServices.Count);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Service initialization failed: {ex.Message}");
            result.IsSuccessful = false;
        }
    }

    private async Task ConfigureModuleAsync(LoadedPlugin loadedPlugin, PluginInitializationResult result, CancellationToken cancellationToken)
    {
        result.CurrentPhase = InitializationPhase.ModuleConfiguration;

        try
        {
            if (result.ModuleInstance == null || result.ModuleServiceProvider == null)
            {
                result.Errors.Add("Module instance or service provider is null during configuration");
                result.IsSuccessful = false;
                return;
            }

            // Create application builder for module configuration
            var app = CreateApplicationBuilder(result.ModuleServiceProvider);

            // Call module's configuration
            using var timeoutCts = new CancellationTokenSource(_options.ConfigurationTimeoutMs);
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await result.ModuleInstance.OnConfigureAsync(app, combinedCts.Token);

            _logger.LogDebug("Configured module for plugin: {PluginId}", loadedPlugin.Manifest.Id);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Module configuration failed: {ex.Message}");
            result.IsSuccessful = false;
        }
    }

    private async Task StartModuleAsync(LoadedPlugin loadedPlugin, PluginInitializationResult result, CancellationToken cancellationToken)
    {
        result.CurrentPhase = InitializationPhase.ModuleStart;

        try
        {
            if (result.ModuleInstance == null)
            {
                result.Errors.Add("Module instance is null during start");
                result.IsSuccessful = false;
                return;
            }

            // Call module's start method
            using var timeoutCts = new CancellationTokenSource(_options.StartTimeoutMs);
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await result.ModuleInstance.OnStartAsync(combinedCts.Token);

            result.IsSuccessful = true;
            result.CurrentPhase = InitializationPhase.Completed;

            _logger.LogDebug("Started module for plugin: {PluginId}", loadedPlugin.Manifest.Id);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Module start failed: {ex.Message}");
            result.IsSuccessful = false;
        }
    }

    private async Task StopModuleAsync(InitializedPlugin initializedPlugin, PluginUninitializationResult result, CancellationToken cancellationToken)
    {
        result.CurrentPhase = UninitializationPhase.ModuleStop;

        try
        {
            if (initializedPlugin.ModuleInstance != null)
            {
                using var timeoutCts = new CancellationTokenSource(_options.StopTimeoutMs);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                await initializedPlugin.ModuleInstance.OnStopAsync(combinedCts.Token);

                _logger.LogDebug("Stopped module for plugin: {PluginId}", initializedPlugin.PluginId);
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Module stop failed: {ex.Message}");
        }
    }

    private async Task UnloadModuleAsync(InitializedPlugin initializedPlugin, PluginUninitializationResult result, CancellationToken cancellationToken)
    {
        result.CurrentPhase = UninitializationPhase.ModuleUnload;

        try
        {
            if (initializedPlugin.ModuleInstance != null)
            {
                using var timeoutCts = new CancellationTokenSource(_options.UnloadTimeoutMs);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                await initializedPlugin.ModuleInstance.OnUnloadAsync(combinedCts.Token);

                _logger.LogDebug("Unloaded module for plugin: {PluginId}", initializedPlugin.PluginId);
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Module unload failed: {ex.Message}");
        }
    }

    private async Task DisposeServicesAsync(InitializedPlugin initializedPlugin, PluginUninitializationResult result, CancellationToken cancellationToken)
    {
        result.CurrentPhase = UninitializationPhase.ServiceDisposal;

        try
        {
            // Dispose module service provider
            if (initializedPlugin.ModuleServiceProvider != null)
            {
                await initializedPlugin.ModuleServiceProvider.DisposeAsync();
                _logger.LogDebug("Disposed services for plugin: {PluginId}", initializedPlugin.PluginId);
            }

            // Dispose module instance if it's disposable
            if (initializedPlugin.ModuleInstance is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (initializedPlugin.ModuleInstance is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Service disposal failed: {ex.Message}");
        }
    }

    private async Task CleanupLoadContextAsync(InitializedPlugin initializedPlugin, PluginUninitializationResult result, CancellationToken cancellationToken)
    {
        result.CurrentPhase = UninitializationPhase.ContextCleanup;

        try
        {
            // Clear references to help with unloading
            initializedPlugin.ModuleInstance = null;
            initializedPlugin.ModuleServiceProvider = null;

            // Attempt to unload the load context
            if (initializedPlugin.LoadContext != null)
            {
                var weakRef = initializedPlugin.LoadContext.TryUnload();
                if (weakRef != null)
                {
                    // Trigger garbage collection to help with unloading
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    // Check if unloading was successful
                    result.IsContextUnloaded = !weakRef.IsAlive;

                    _logger.LogDebug("Initiated load context cleanup for plugin: {PluginId} (Unloaded: {Unloaded})",
                        initializedPlugin.PluginId, result.IsContextUnloaded);
                }
            }

            result.IsSuccessful = true;
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Load context cleanup failed: {ex.Message}");
        }
    }

    private IApplicationBuilder CreateApplicationBuilder(ModuleServiceProvider serviceProvider)
    {
        // Create a minimal application builder for plugin configuration
        // This would typically be provided by the host application
        return new ApplicationBuilder(serviceProvider);
    }

    private List<Type> GetAvailableServiceTypes()
    {
        // Return types of services available to plugins
        // This would typically come from the shell's service registry
        return new List<Type>
        {
            typeof(ILogger),
            typeof(ILoggerFactory),
            typeof(IServiceProvider),
            typeof(IServiceScope)
        };
    }

    private IReadOnlyDictionary<string, object> GetModuleConfiguration(string moduleId)
    {
        // Return configuration specific to the module
        // This would typically come from the shell's configuration system
        return new Dictionary<string, object>();
    }
}

/// <summary>
/// Interface for plugin initialization service.
/// </summary>
public interface IPluginInitializer
{
    /// <summary>
    /// Initializes a plugin through all lifecycle phases.
    /// </summary>
    /// <param name="loadedPlugin">The loaded plugin to initialize.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The initialization result.</returns>
    Task<PluginInitializationResult> InitializePluginAsync(LoadedPlugin loadedPlugin, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uninitializes a plugin, calling its cleanup methods.
    /// </summary>
    /// <param name="initializedPlugin">The initialized plugin to uninitialize.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The uninitialization result.</returns>
    Task<PluginUninitializationResult> UninitializePluginAsync(InitializedPlugin initializedPlugin, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a loaded plugin ready for initialization.
/// </summary>
public class LoadedPlugin
{
    /// <summary>
    /// Gets or sets the plugin manifest.
    /// </summary>
    public PluginManifest Manifest { get; set; } = new();

    /// <summary>
    /// Gets or sets the plugin assembly.
    /// </summary>
    public System.Reflection.Assembly Assembly { get; set; } = null!;

    /// <summary>
    /// Gets or sets the plugin load context.
    /// </summary>
    public PluginLoadContext LoadContext { get; set; } = null!;

    /// <summary>
    /// Gets or sets the plugin metadata.
    /// </summary>
    public PluginMetadata? Metadata { get; set; }

    /// <summary>
    /// Gets or sets when the plugin was loaded.
    /// </summary>
    public DateTime LoadedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents an initialized plugin.
/// </summary>
public class InitializedPlugin
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
    /// Gets or sets the module instance.
    /// </summary>
    public IBusinessLogicModule? ModuleInstance { get; set; }

    /// <summary>
    /// Gets or sets the module service provider.
    /// </summary>
    public ModuleServiceProvider? ModuleServiceProvider { get; set; }

    /// <summary>
    /// Gets or sets the plugin load context.
    /// </summary>
    public PluginLoadContext? LoadContext { get; set; }

    /// <summary>
    /// Gets or sets when the plugin was initialized.
    /// </summary>
    public DateTime InitializedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the initialization result.
    /// </summary>
    public PluginInitializationResult? InitializationResult { get; set; }
}

/// <summary>
/// Result of plugin initialization.
/// </summary>
public class PluginInitializationResult
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
    /// Gets or sets whether initialization was successful.
    /// </summary>
    public bool IsSuccessful { get; set; } = true;

    /// <summary>
    /// Gets or sets the current initialization phase.
    /// </summary>
    public InitializationPhase CurrentPhase { get; set; } = InitializationPhase.NotStarted;

    /// <summary>
    /// Gets the initialization errors.
    /// </summary>
    public List<string> Errors { get; } = new();

    /// <summary>
    /// Gets the initialization warnings.
    /// </summary>
    public List<string> Warnings { get; } = new();

    /// <summary>
    /// Gets or sets the module instance.
    /// </summary>
    public IBusinessLogicModule? ModuleInstance { get; set; }

    /// <summary>
    /// Gets or sets the module service provider.
    /// </summary>
    public ModuleServiceProvider? ModuleServiceProvider { get; set; }

    /// <summary>
    /// Gets or sets the initialization start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the initialization duration.
    /// </summary>
    public TimeSpan InitializationDuration { get; set; }

    /// <summary>
    /// Gets or sets additional initialization details.
    /// </summary>
    public Dictionary<string, object> Details { get; } = new();
}

/// <summary>
/// Result of plugin uninitialization.
/// </summary>
public class PluginUninitializationResult
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
    /// Gets or sets whether uninitialization was successful.
    /// </summary>
    public bool IsSuccessful { get; set; } = true;

    /// <summary>
    /// Gets or sets the current uninitialization phase.
    /// </summary>
    public UninitializationPhase CurrentPhase { get; set; } = UninitializationPhase.NotStarted;

    /// <summary>
    /// Gets the uninitialization warnings.
    /// </summary>
    public List<string> Warnings { get; } = new();

    /// <summary>
    /// Gets or sets whether the load context was successfully unloaded.
    /// </summary>
    public bool IsContextUnloaded { get; set; }

    /// <summary>
    /// Gets or sets the uninitialization start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the uninitialization duration.
    /// </summary>
    public TimeSpan UninitializationDuration { get; set; }

    /// <summary>
    /// Gets or sets additional uninitialization details.
    /// </summary>
    public Dictionary<string, object> Details { get; } = new();
}

/// <summary>
/// Enumeration of initialization phases.
/// </summary>
public enum InitializationPhase
{
    NotStarted,
    Validation,
    ModuleCreation,
    ModuleValidation,
    ServiceInitialization,
    ModuleConfiguration,
    ModuleStart,
    Completed,
    Failed
}

/// <summary>
/// Enumeration of uninitialization phases.
/// </summary>
public enum UninitializationPhase
{
    NotStarted,
    ModuleStop,
    ModuleUnload,
    ServiceDisposal,
    ContextCleanup,
    Completed
}

/// <summary>
/// Configuration options for plugin initialization.
/// </summary>
public class PluginInitializationOptions
{
    /// <summary>
    /// Gets or sets the shell version.
    /// </summary>
    public Version ShellVersion { get; set; } = new Version(1, 0, 0);

    /// <summary>
    /// Gets or sets the environment name.
    /// </summary>
    public string Environment { get; set; } = "Development";

    /// <summary>
    /// Gets or sets the validation timeout in milliseconds.
    /// </summary>
    public int ValidationTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the initialization timeout in milliseconds.
    /// </summary>
    public int InitializationTimeoutMs { get; set; } = 60000;

    /// <summary>
    /// Gets or sets the configuration timeout in milliseconds.
    /// </summary>
    public int ConfigurationTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the start timeout in milliseconds.
    /// </summary>
    public int StartTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the stop timeout in milliseconds.
    /// </summary>
    public int StopTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the unload timeout in milliseconds.
    /// </summary>
    public int UnloadTimeoutMs { get; set; } = 10000;

    /// <summary>
    /// Gets or sets whether to enable parallel initialization.
    /// </summary>
    public bool EnableParallelInitialization { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum number of plugins to initialize concurrently.
    /// </summary>
    public int MaxConcurrentInitializations { get; set; } = 3;
}