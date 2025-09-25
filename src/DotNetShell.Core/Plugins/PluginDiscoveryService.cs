using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using DotNetShell.Abstractions;

namespace DotNetShell.Core.Plugins;

/// <summary>
/// Service responsible for discovering plugins from various sources including directories,
/// NuGet packages, and plugin manifests.
/// </summary>
public class PluginDiscoveryService : IPluginDiscoveryService
{
    private readonly PluginDiscoveryOptions _options;
    private readonly ILogger<PluginDiscoveryService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginDiscoveryService"/> class.
    /// </summary>
    /// <param name="options">Plugin discovery configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    public PluginDiscoveryService(
        IOptions<PluginDiscoveryOptions> options,
        ILogger<PluginDiscoveryService> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
    }

    /// <summary>
    /// Discovers all plugins from configured sources.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A collection of discovered plugin information.</returns>
    public async Task<IEnumerable<DiscoveredPlugin>> DiscoverPluginsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting plugin discovery process");

        var discoveredPlugins = new List<DiscoveredPlugin>();

        try
        {
            // Discover from configured plugin directories
            foreach (var directory in _options.PluginDirectories)
            {
                _logger.LogDebug("Discovering plugins from directory: {Directory}", directory);
                var plugins = await DiscoverFromDirectoryAsync(directory, cancellationToken);
                discoveredPlugins.AddRange(plugins);
            }

            // Discover from NuGet packages if enabled
            if (_options.EnableNuGetDiscovery)
            {
                _logger.LogDebug("Discovering plugins from NuGet sources");
                var nugetPlugins = await DiscoverFromNuGetAsync(cancellationToken);
                discoveredPlugins.AddRange(nugetPlugins);
            }

            // Discover from manifest files
            foreach (var manifestPath in _options.ManifestFiles)
            {
                _logger.LogDebug("Discovering plugin from manifest: {ManifestPath}", manifestPath);
                var plugin = await DiscoverFromManifestAsync(manifestPath, cancellationToken);
                if (plugin != null)
                {
                    discoveredPlugins.Add(plugin);
                }
            }

            // Remove duplicates based on plugin ID
            var uniquePlugins = discoveredPlugins
                .GroupBy(p => p.Manifest.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(p => Version.Parse(p.Manifest.Version)).First())
                .ToList();

            _logger.LogInformation("Plugin discovery completed. Found {PluginCount} unique plugins", uniquePlugins.Count);

            return uniquePlugins;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during plugin discovery");
            throw new PluginDiscoveryException("Plugin discovery failed", ex);
        }
    }

    /// <summary>
    /// Discovers plugins from a specific directory.
    /// </summary>
    /// <param name="directory">The directory path to search.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A collection of discovered plugins from the directory.</returns>
    public async Task<IEnumerable<DiscoveredPlugin>> DiscoverFromDirectoryAsync(
        string directory,
        CancellationToken cancellationToken = default)
    {
        var discoveredPlugins = new List<DiscoveredPlugin>();

        try
        {
            if (!Directory.Exists(directory))
            {
                _logger.LogWarning("Plugin directory does not exist: {Directory}", directory);
                return discoveredPlugins;
            }

            _logger.LogDebug("Scanning directory for plugins: {Directory}", directory);

            // Look for manifest files first (preferred)
            var manifestFiles = Directory.GetFiles(
                directory,
                _options.ManifestFileName,
                SearchOption.AllDirectories);

            foreach (var manifestFile in manifestFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var plugin = await DiscoverFromManifestAsync(manifestFile, cancellationToken);
                if (plugin != null)
                {
                    discoveredPlugins.Add(plugin);
                }
            }

            // If no manifests found, look for assembly files
            if (discoveredPlugins.Count == 0)
            {
                var assemblyFiles = Directory.GetFiles(
                    directory,
                    "*.dll",
                    SearchOption.AllDirectories)
                    .Where(f => !IsSystemAssembly(f))
                    .ToArray();

                foreach (var assemblyFile in assemblyFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var plugin = await DiscoverFromAssemblyAsync(assemblyFile, cancellationToken);
                    if (plugin != null)
                    {
                        discoveredPlugins.Add(plugin);
                    }
                }
            }

            _logger.LogDebug("Found {PluginCount} plugins in directory: {Directory}",
                discoveredPlugins.Count, directory);

            return discoveredPlugins;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering plugins from directory: {Directory}", directory);
            throw new PluginDiscoveryException($"Failed to discover plugins from directory '{directory}'", directory, ex);
        }
    }

    /// <summary>
    /// Discovers a plugin from a manifest file.
    /// </summary>
    /// <param name="manifestPath">The path to the manifest file.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The discovered plugin, or null if discovery fails.</returns>
    public async Task<DiscoveredPlugin?> DiscoverFromManifestAsync(
        string manifestPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(manifestPath))
            {
                _logger.LogWarning("Manifest file does not exist: {ManifestPath}", manifestPath);
                return null;
            }

            _logger.LogTrace("Reading manifest file: {ManifestPath}", manifestPath);

            var manifestContent = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            var manifest = JsonSerializer.Deserialize<PluginManifest>(manifestContent, _jsonOptions);

            if (manifest == null)
            {
                _logger.LogWarning("Failed to deserialize manifest file: {ManifestPath}", manifestPath);
                return null;
            }

            // Validate manifest
            var validationErrors = manifest.Validate().ToList();
            if (validationErrors.Count > 0)
            {
                _logger.LogWarning("Manifest validation failed for {ManifestPath}: {Errors}",
                    manifestPath, string.Join(", ", validationErrors));
                return null;
            }

            // Resolve assembly path
            var manifestDirectory = Path.GetDirectoryName(manifestPath) ?? string.Empty;
            var assemblyPath = Path.IsPathRooted(manifest.MainAssembly)
                ? manifest.MainAssembly
                : Path.Combine(manifestDirectory, manifest.MainAssembly);

            if (!File.Exists(assemblyPath))
            {
                _logger.LogWarning("Main assembly not found for plugin {PluginId}: {AssemblyPath}",
                    manifest.Id, assemblyPath);
                return null;
            }

            var discoveredPlugin = new DiscoveredPlugin
            {
                Manifest = manifest,
                AssemblyPath = assemblyPath,
                ManifestPath = manifestPath,
                DiscoverySource = PluginDiscoverySource.Manifest,
                DiscoveredAt = DateTime.UtcNow
            };

            _logger.LogDebug("Successfully discovered plugin from manifest: {PluginId} v{Version}",
                manifest.Id, manifest.Version);

            return discoveredPlugin;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering plugin from manifest: {ManifestPath}", manifestPath);
            return null;
        }
    }

    /// <summary>
    /// Discovers a plugin from an assembly file.
    /// </summary>
    /// <param name="assemblyPath">The path to the assembly file.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The discovered plugin, or null if discovery fails.</returns>
    public async Task<DiscoveredPlugin?> DiscoverFromAssemblyAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(assemblyPath))
            {
                _logger.LogWarning("Assembly file does not exist: {AssemblyPath}", assemblyPath);
                return null;
            }

            _logger.LogTrace("Analyzing assembly for plugin: {AssemblyPath}", assemblyPath);

            // Use a temporary load context to inspect the assembly
            using var tempContext = new PluginLoadContext(assemblyPath, isCollectible: true);
            var assembly = tempContext.LoadPluginAssembly();

            // Look for types implementing IBusinessLogicModule
            var moduleTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract &&
                           typeof(IBusinessLogicModule).IsAssignableFrom(t))
                .ToList();

            if (moduleTypes.Count == 0)
            {
                _logger.LogTrace("No IBusinessLogicModule implementations found in assembly: {AssemblyPath}",
                    assemblyPath);
                return null;
            }

            if (moduleTypes.Count > 1)
            {
                _logger.LogWarning("Multiple IBusinessLogicModule implementations found in assembly: {AssemblyPath}. Using first one.",
                    assemblyPath);
            }

            var moduleType = moduleTypes.First();

            // Create a basic manifest from assembly metadata
            var assemblyName = assembly.GetName();
            var manifest = new PluginManifest
            {
                Id = assemblyName.Name ?? Path.GetFileNameWithoutExtension(assemblyPath),
                Name = assemblyName.Name ?? Path.GetFileNameWithoutExtension(assemblyPath),
                Version = assemblyName.Version?.ToString() ?? "1.0.0",
                MainAssembly = Path.GetFileName(assemblyPath),
                EntryPoint = moduleType.FullName ?? moduleType.Name,
                Description = GetAssemblyDescription(assembly),
                Author = GetAssemblyAuthor(assembly)
            };

            var discoveredPlugin = new DiscoveredPlugin
            {
                Manifest = manifest,
                AssemblyPath = assemblyPath,
                ManifestPath = null,
                DiscoverySource = PluginDiscoverySource.Assembly,
                DiscoveredAt = DateTime.UtcNow
            };

            _logger.LogDebug("Successfully discovered plugin from assembly: {PluginId} v{Version}",
                manifest.Id, manifest.Version);

            return discoveredPlugin;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering plugin from assembly: {AssemblyPath}", assemblyPath);
            return null;
        }
    }

    /// <summary>
    /// Discovers plugins from NuGet packages.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A collection of discovered plugins from NuGet sources.</returns>
    public async Task<IEnumerable<DiscoveredPlugin>> DiscoverFromNuGetAsync(CancellationToken cancellationToken = default)
    {
        var discoveredPlugins = new List<DiscoveredPlugin>();

        try
        {
            _logger.LogDebug("Starting NuGet plugin discovery");

            // TODO: Implement NuGet package discovery
            // This would involve:
            // 1. Querying configured NuGet feeds
            // 2. Looking for packages with specific tags (e.g., "dotnet-shell-plugin")
            // 3. Downloading and extracting packages
            // 4. Scanning extracted content for plugins

            _logger.LogInformation("NuGet plugin discovery is not yet implemented");

            return discoveredPlugins;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during NuGet plugin discovery");
            throw new PluginDiscoveryException("NuGet plugin discovery failed", ex);
        }
    }

    /// <summary>
    /// Checks if a plugin is already loaded.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="version">The plugin version.</param>
    /// <returns>True if the plugin is loaded; otherwise, false.</returns>
    public Task<bool> IsPluginLoadedAsync(string pluginId, string version)
    {
        // This would check against the plugin manager or registry
        // For now, return false as implementation is pending
        return Task.FromResult(false);
    }

    private bool IsSystemAssembly(string assemblyPath)
    {
        var fileName = Path.GetFileName(assemblyPath);

        // Skip system assemblies and common framework assemblies
        var systemPrefixes = new[]
        {
            "System.",
            "Microsoft.",
            "netstandard",
            "mscorlib",
            "WindowsBase",
            "PresentationCore",
            "PresentationFramework"
        };

        return systemPrefixes.Any(prefix => fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private string? GetAssemblyDescription(System.Reflection.Assembly assembly)
    {
        try
        {
            var descriptionAttribute = assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyDescriptionAttribute), false)
                .OfType<System.Reflection.AssemblyDescriptionAttribute>()
                .FirstOrDefault();

            return descriptionAttribute?.Description;
        }
        catch
        {
            return null;
        }
    }

    private string? GetAssemblyAuthor(System.Reflection.Assembly assembly)
    {
        try
        {
            var companyAttribute = assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyCompanyAttribute), false)
                .OfType<System.Reflection.AssemblyCompanyAttribute>()
                .FirstOrDefault();

            return companyAttribute?.Company;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Interface for plugin discovery service.
/// </summary>
public interface IPluginDiscoveryService
{
    /// <summary>
    /// Discovers all plugins from configured sources.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A collection of discovered plugin information.</returns>
    Task<IEnumerable<DiscoveredPlugin>> DiscoverPluginsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers plugins from a specific directory.
    /// </summary>
    /// <param name="directory">The directory path to search.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A collection of discovered plugins from the directory.</returns>
    Task<IEnumerable<DiscoveredPlugin>> DiscoverFromDirectoryAsync(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers a plugin from a manifest file.
    /// </summary>
    /// <param name="manifestPath">The path to the manifest file.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The discovered plugin, or null if discovery fails.</returns>
    Task<DiscoveredPlugin?> DiscoverFromManifestAsync(string manifestPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers a plugin from an assembly file.
    /// </summary>
    /// <param name="assemblyPath">The path to the assembly file.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The discovered plugin, or null if discovery fails.</returns>
    Task<DiscoveredPlugin?> DiscoverFromAssemblyAsync(string assemblyPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers plugins from NuGet packages.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A collection of discovered plugins from NuGet sources.</returns>
    Task<IEnumerable<DiscoveredPlugin>> DiscoverFromNuGetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a plugin is already loaded.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="version">The plugin version.</param>
    /// <returns>True if the plugin is loaded; otherwise, false.</returns>
    Task<bool> IsPluginLoadedAsync(string pluginId, string version);
}

/// <summary>
/// Represents a discovered plugin.
/// </summary>
public class DiscoveredPlugin
{
    /// <summary>
    /// Gets or sets the plugin manifest.
    /// </summary>
    public PluginManifest Manifest { get; set; } = new();

    /// <summary>
    /// Gets or sets the path to the main assembly.
    /// </summary>
    public string AssemblyPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path to the manifest file (if available).
    /// </summary>
    public string? ManifestPath { get; set; }

    /// <summary>
    /// Gets or sets the source from which the plugin was discovered.
    /// </summary>
    public PluginDiscoverySource DiscoverySource { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the plugin was discovered.
    /// </summary>
    public DateTime DiscoveredAt { get; set; }

    /// <summary>
    /// Gets or sets additional metadata about the discovery.
    /// </summary>
    public Dictionary<string, object> DiscoveryMetadata { get; set; } = new();

    /// <summary>
    /// Gets the plugin directory path.
    /// </summary>
    public string PluginDirectory => Path.GetDirectoryName(AssemblyPath) ?? string.Empty;
}

/// <summary>
/// Enumeration of plugin discovery sources.
/// </summary>
public enum PluginDiscoverySource
{
    /// <summary>
    /// Plugin discovered from manifest file.
    /// </summary>
    Manifest,

    /// <summary>
    /// Plugin discovered from assembly analysis.
    /// </summary>
    Assembly,

    /// <summary>
    /// Plugin discovered from NuGet package.
    /// </summary>
    NuGet,

    /// <summary>
    /// Plugin discovered from configuration.
    /// </summary>
    Configuration
}

/// <summary>
/// Configuration options for plugin discovery.
/// </summary>
public class PluginDiscoveryOptions
{
    /// <summary>
    /// Gets or sets the plugin directories to scan.
    /// </summary>
    public List<string> PluginDirectories { get; set; } = new()
    {
        "./plugins",
        "./modules"
    };

    /// <summary>
    /// Gets or sets the manifest file name to look for.
    /// </summary>
    public string ManifestFileName { get; set; } = "plugin.json";

    /// <summary>
    /// Gets or sets specific manifest files to load.
    /// </summary>
    public List<string> ManifestFiles { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to enable NuGet package discovery.
    /// </summary>
    public bool EnableNuGetDiscovery { get; set; } = false;

    /// <summary>
    /// Gets or sets the NuGet sources to search.
    /// </summary>
    public List<string> NuGetSources { get; set; } = new()
    {
        "https://api.nuget.org/v3/index.json"
    };

    /// <summary>
    /// Gets or sets the NuGet package tag to search for.
    /// </summary>
    public string NuGetPluginTag { get; set; } = "dotnet-shell-plugin";

    /// <summary>
    /// Gets or sets whether to include prerelease packages in NuGet search.
    /// </summary>
    public bool IncludeNuGetPrerelease { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum number of plugins to discover (0 = unlimited).
    /// </summary>
    public int MaxPlugins { get; set; } = 0;

    /// <summary>
    /// Gets or sets whether to enable parallel discovery.
    /// </summary>
    public bool EnableParallelDiscovery { get; set; } = true;

    /// <summary>
    /// Gets or sets the discovery timeout in milliseconds.
    /// </summary>
    public int DiscoveryTimeoutMs { get; set; } = 30000;
}