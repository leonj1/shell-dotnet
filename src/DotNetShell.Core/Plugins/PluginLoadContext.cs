using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;
using DotNetShell.Abstractions;

namespace DotNetShell.Core.Plugins;

/// <summary>
/// Custom AssemblyLoadContext that provides isolated loading of plugin assemblies
/// while allowing controlled access to shared abstractions and infrastructure services.
/// </summary>
public class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _pluginPath;
    private readonly ConcurrentDictionary<string, Assembly> _sharedAssemblies;
    private readonly ConcurrentDictionary<string, Assembly> _loadedAssemblies;
    private readonly HashSet<string> _sharedAssemblyNames;
    private readonly object _lockObject = new();
    private readonly ILogger? _logger;

    /// <summary>
    /// Gets the plugin path for this load context.
    /// </summary>
    public string PluginPath => _pluginPath;

    /// <summary>
    /// Gets the unique identifier for this plugin context.
    /// </summary>
    public string ContextId { get; }

    /// <summary>
    /// Gets the loaded assemblies in this context.
    /// </summary>
    public IReadOnlyDictionary<string, Assembly> LoadedAssemblies => _loadedAssemblies.AsReadOnly();

    /// <summary>
    /// Gets the shared assemblies available to this context.
    /// </summary>
    public IReadOnlyDictionary<string, Assembly> SharedAssemblies => _sharedAssemblies.AsReadOnly();

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginLoadContext"/> class.
    /// </summary>
    /// <param name="pluginPath">The path to the main plugin assembly.</param>
    /// <param name="contextId">The unique identifier for this context.</param>
    /// <param name="isCollectible">Whether this context supports unloading.</param>
    /// <param name="logger">Optional logger instance.</param>
    public PluginLoadContext(
        string pluginPath,
        string? contextId = null,
        bool isCollectible = true,
        ILogger? logger = null)
        : base(contextId ?? $"Plugin_{Path.GetFileNameWithoutExtension(pluginPath)}_{Guid.NewGuid():N}",
               isCollectible: isCollectible)
    {
        _pluginPath = pluginPath ?? throw new ArgumentNullException(nameof(pluginPath));
        ContextId = Name ?? throw new InvalidOperationException("Context name is null");
        _logger = logger;

        if (!File.Exists(_pluginPath))
        {
            throw new FileNotFoundException($"Plugin assembly not found: {_pluginPath}");
        }

        _resolver = new AssemblyDependencyResolver(_pluginPath);
        _sharedAssemblies = new ConcurrentDictionary<string, Assembly>();
        _loadedAssemblies = new ConcurrentDictionary<string, Assembly>();
        _sharedAssemblyNames = new HashSet<string>();

        // Register shared assemblies that plugins can access
        RegisterSharedAssemblies();

        _logger?.LogInformation("Created plugin load context {ContextId} for {PluginPath}", ContextId, _pluginPath);
    }

    /// <summary>
    /// Loads the main plugin assembly.
    /// </summary>
    /// <returns>The loaded plugin assembly.</returns>
    public Assembly LoadPluginAssembly()
    {
        try
        {
            _logger?.LogDebug("Loading plugin assembly from {PluginPath}", _pluginPath);

            var assembly = LoadFromAssemblyPath(_pluginPath);
            _loadedAssemblies.TryAdd(assembly.GetName().Name ?? string.Empty, assembly);

            _logger?.LogInformation("Successfully loaded plugin assembly {AssemblyName}", assembly.GetName().Name);
            return assembly;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load plugin assembly from {PluginPath}", _pluginPath);
            throw new PluginLoadException($"Failed to load plugin assembly from '{_pluginPath}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Resolves an assembly by name within this load context.
    /// </summary>
    /// <param name="assemblyName">The assembly name to resolve.</param>
    /// <returns>The resolved assembly, or null if not found.</returns>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name == null)
        {
            return null;
        }

        try
        {
            // Check if it's a shared assembly first
            if (_sharedAssemblyNames.Contains(assemblyName.Name))
            {
                if (_sharedAssemblies.TryGetValue(assemblyName.Name, out var sharedAssembly))
                {
                    _logger?.LogTrace("Resolved shared assembly {AssemblyName} for plugin {ContextId}",
                        assemblyName.Name, ContextId);
                    return sharedAssembly;
                }
            }

            // Try to resolve using the dependency resolver
            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (!string.IsNullOrEmpty(assemblyPath) && File.Exists(assemblyPath))
            {
                var assembly = LoadFromAssemblyPath(assemblyPath);
                _loadedAssemblies.TryAdd(assemblyName.Name, assembly);

                _logger?.LogTrace("Resolved and loaded assembly {AssemblyName} from {AssemblyPath} for plugin {ContextId}",
                    assemblyName.Name, assemblyPath, ContextId);
                return assembly;
            }

            // Check if already loaded in this context
            if (_loadedAssemblies.TryGetValue(assemblyName.Name, out var loadedAssembly))
            {
                _logger?.LogTrace("Returning cached assembly {AssemblyName} for plugin {ContextId}",
                    assemblyName.Name, ContextId);
                return loadedAssembly;
            }

            _logger?.LogTrace("Could not resolve assembly {AssemblyName} for plugin {ContextId}",
                assemblyName.Name, ContextId);
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error loading assembly {AssemblyName} for plugin {ContextId}",
                assemblyName.Name, ContextId);
            return null;
        }
    }

    /// <summary>
    /// Resolves an unmanaged DLL by name within this load context.
    /// </summary>
    /// <param name="unmanagedDllName">The name of the unmanaged DLL to resolve.</param>
    /// <returns>A handle to the loaded library, or IntPtr.Zero if not found.</returns>
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        try
        {
            string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (!string.IsNullOrEmpty(libraryPath) && File.Exists(libraryPath))
            {
                _logger?.LogTrace("Loading unmanaged library {LibraryName} from {LibraryPath} for plugin {ContextId}",
                    unmanagedDllName, libraryPath, ContextId);
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            _logger?.LogTrace("Could not resolve unmanaged library {LibraryName} for plugin {ContextId}",
                unmanagedDllName, ContextId);
            return IntPtr.Zero;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error loading unmanaged library {LibraryName} for plugin {ContextId}",
                unmanagedDllName, ContextId);
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// Registers a shared assembly that plugins can access.
    /// </summary>
    /// <param name="assembly">The assembly to share.</param>
    public void RegisterSharedAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var name = assembly.GetName().Name;
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Assembly name cannot be null or empty", nameof(assembly));
        }

        lock (_lockObject)
        {
            _sharedAssemblies.TryAdd(name, assembly);
            _sharedAssemblyNames.Add(name);
        }

        _logger?.LogTrace("Registered shared assembly {AssemblyName} for plugin context {ContextId}", name, ContextId);
    }

    /// <summary>
    /// Registers multiple shared assemblies.
    /// </summary>
    /// <param name="assemblies">The assemblies to share.</param>
    public void RegisterSharedAssemblies(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies)
        {
            RegisterSharedAssembly(assembly);
        }
    }

    /// <summary>
    /// Registers shared assemblies by type.
    /// </summary>
    /// <param name="types">Types whose assemblies should be shared.</param>
    public void RegisterSharedAssembliesByType(params Type[] types)
    {
        ArgumentNullException.ThrowIfNull(types);

        foreach (var type in types)
        {
            RegisterSharedAssembly(type.Assembly);
        }
    }

    /// <summary>
    /// Checks if an assembly is shared.
    /// </summary>
    /// <param name="assemblyName">The assembly name to check.</param>
    /// <returns>True if the assembly is shared; otherwise, false.</returns>
    public bool IsSharedAssembly(string assemblyName)
    {
        return !string.IsNullOrEmpty(assemblyName) && _sharedAssemblyNames.Contains(assemblyName);
    }

    /// <summary>
    /// Gets statistics about this load context.
    /// </summary>
    /// <returns>Load context statistics.</returns>
    public PluginLoadContextStatistics GetStatistics()
    {
        return new PluginLoadContextStatistics
        {
            ContextId = ContextId,
            PluginPath = _pluginPath,
            LoadedAssembliesCount = _loadedAssemblies.Count,
            SharedAssembliesCount = _sharedAssemblies.Count,
            IsCollectible = IsCollectible,
            LoadedAssemblyNames = _loadedAssemblies.Keys.ToArray(),
            SharedAssemblyNames = _sharedAssemblyNames.ToArray()
        };
    }

    /// <summary>
    /// Attempts to unload this plugin context and all its assemblies.
    /// </summary>
    /// <returns>A weak reference that can be used to determine when unloading is complete.</returns>
    public WeakReference? TryUnload()
    {
        if (!IsCollectible)
        {
            _logger?.LogWarning("Cannot unload non-collectible plugin context {ContextId}", ContextId);
            return null;
        }

        try
        {
            _logger?.LogInformation("Attempting to unload plugin context {ContextId}", ContextId);

            // Clear references to help with unloading
            _loadedAssemblies.Clear();

            var weakRef = new WeakReference(this);
            Unload();

            _logger?.LogInformation("Initiated unload for plugin context {ContextId}", ContextId);
            return weakRef;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to unload plugin context {ContextId}", ContextId);
            throw new PluginUnloadException($"Failed to unload plugin context '{ContextId}': {ex.Message}", ex);
        }
    }

    private void RegisterSharedAssemblies()
    {
        try
        {
            // Register key shared assemblies that plugins need access to
            var sharedTypes = new[]
            {
                typeof(IBusinessLogicModule), // From DotNetShell.Abstractions
                typeof(IServiceCollection),  // From Microsoft.Extensions.DependencyInjection.Abstractions
                typeof(IApplicationBuilder), // From Microsoft.AspNetCore.Http.Abstractions
                typeof(ILogger),             // From Microsoft.Extensions.Logging.Abstractions
                typeof(CancellationToken),   // From System.Runtime
                typeof(Task),                // From System.Runtime
                typeof(IDisposable),         // From System.Runtime
            };

            foreach (var type in sharedTypes)
            {
                RegisterSharedAssembly(type.Assembly);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error registering default shared assemblies for plugin context {ContextId}", ContextId);
        }
    }

    /// <summary>
    /// Gets information about a loaded assembly.
    /// </summary>
    /// <param name="assemblyName">The assembly name.</param>
    /// <returns>Assembly information, or null if not found.</returns>
    public AssemblyInfo? GetAssemblyInfo(string assemblyName)
    {
        if (string.IsNullOrEmpty(assemblyName))
        {
            return null;
        }

        if (_loadedAssemblies.TryGetValue(assemblyName, out var assembly))
        {
            return new AssemblyInfo
            {
                Name = assembly.GetName().Name ?? string.Empty,
                Version = assembly.GetName().Version?.ToString() ?? "Unknown",
                Location = assembly.Location,
                IsShared = IsSharedAssembly(assemblyName),
                LoadContext = ContextId
            };
        }

        return null;
    }

    /// <summary>
    /// Gets all loaded assemblies information.
    /// </summary>
    /// <returns>Collection of assembly information.</returns>
    public IEnumerable<AssemblyInfo> GetAllAssembliesInfo()
    {
        return _loadedAssemblies.Values.Select(assembly => new AssemblyInfo
        {
            Name = assembly.GetName().Name ?? string.Empty,
            Version = assembly.GetName().Version?.ToString() ?? "Unknown",
            Location = assembly.Location,
            IsShared = IsSharedAssembly(assembly.GetName().Name ?? string.Empty),
            LoadContext = ContextId
        });
    }
}

/// <summary>
/// Statistics about a plugin load context.
/// </summary>
public class PluginLoadContextStatistics
{
    /// <summary>
    /// Gets or sets the context identifier.
    /// </summary>
    public string ContextId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the plugin path.
    /// </summary>
    public string PluginPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of loaded assemblies.
    /// </summary>
    public int LoadedAssembliesCount { get; set; }

    /// <summary>
    /// Gets or sets the number of shared assemblies.
    /// </summary>
    public int SharedAssembliesCount { get; set; }

    /// <summary>
    /// Gets or sets whether this context is collectible.
    /// </summary>
    public bool IsCollectible { get; set; }

    /// <summary>
    /// Gets or sets the names of loaded assemblies.
    /// </summary>
    public string[] LoadedAssemblyNames { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the names of shared assemblies.
    /// </summary>
    public string[] SharedAssemblyNames { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Information about an assembly in a plugin context.
/// </summary>
public class AssemblyInfo
{
    /// <summary>
    /// Gets or sets the assembly name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the assembly version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the assembly location.
    /// </summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this is a shared assembly.
    /// </summary>
    public bool IsShared { get; set; }

    /// <summary>
    /// Gets or sets the load context identifier.
    /// </summary>
    public string LoadContext { get; set; } = string.Empty;
}