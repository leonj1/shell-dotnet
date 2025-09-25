using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using DotNetShell.Abstractions;

namespace DotNetShell.Core.Plugins;

/// <summary>
/// Service responsible for validating plugin assemblies, manifests, and security requirements.
/// </summary>
public class PluginValidator : IPluginValidator
{
    private readonly PluginValidationOptions _options;
    private readonly ILogger<PluginValidator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginValidator"/> class.
    /// </summary>
    /// <param name="options">Plugin validation configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    public PluginValidator(
        IOptions<PluginValidationOptions> options,
        ILogger<PluginValidator> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validates a discovered plugin comprehensively.
    /// </summary>
    /// <param name="plugin">The plugin to validate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The validation result.</returns>
    public async Task<PluginValidationResult> ValidatePluginAsync(
        DiscoveredPlugin plugin,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        _logger.LogDebug("Starting validation for plugin: {PluginId} v{Version}",
            plugin.Manifest.Id, plugin.Manifest.Version);

        var result = new PluginValidationResult
        {
            PluginId = plugin.Manifest.Id,
            Version = plugin.Manifest.Version,
            IsValid = true
        };

        try
        {
            // 1. Validate manifest
            await ValidateManifestAsync(plugin, result, cancellationToken);

            // 2. Validate assembly
            await ValidateAssemblyAsync(plugin, result, cancellationToken);

            // 3. Validate security requirements
            await ValidateSecurityAsync(plugin, result, cancellationToken);

            // 4. Validate dependencies
            await ValidateDependenciesAsync(plugin, result, cancellationToken);

            // 5. Validate compatibility
            await ValidateCompatibilityAsync(plugin, result, cancellationToken);

            // 6. Validate business logic module interface
            await ValidateModuleInterfaceAsync(plugin, result, cancellationToken);

            // Overall validation status
            result.IsValid = result.Errors.Count == 0;

            _logger.LogDebug("Plugin validation completed for {PluginId}: {IsValid} ({ErrorCount} errors, {WarningCount} warnings)",
                plugin.Manifest.Id, result.IsValid, result.Errors.Count, result.Warnings.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during plugin validation: {PluginId}", plugin.Manifest.Id);
            result.IsValid = false;
            result.Errors.Add($"Validation failed with exception: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Validates only the plugin manifest.
    /// </summary>
    /// <param name="manifest">The manifest to validate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The validation result.</returns>
    public Task<PluginValidationResult> ValidateManifestAsync(
        PluginManifest manifest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var result = new PluginValidationResult
        {
            PluginId = manifest.Id,
            Version = manifest.Version,
            IsValid = true
        };

        var errors = manifest.Validate().ToList();
        foreach (var error in errors)
        {
            result.Errors.Add(error);
        }

        result.IsValid = result.Errors.Count == 0;

        return Task.FromResult(result);
    }

    /// <summary>
    /// Validates plugin assembly structure and contents.
    /// </summary>
    /// <param name="assemblyPath">The path to the assembly to validate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The validation result.</returns>
    public async Task<PluginValidationResult> ValidateAssemblyAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyPath);

        var result = new PluginValidationResult
        {
            IsValid = true
        };

        try
        {
            if (!File.Exists(assemblyPath))
            {
                result.Errors.Add($"Assembly file not found: {assemblyPath}");
                result.IsValid = false;
                return result;
            }

            // Validate assembly can be loaded
            using var tempContext = new PluginLoadContext(assemblyPath, isCollectible: true);
            var assembly = tempContext.LoadPluginAssembly();

            // Validate assembly structure
            ValidateAssemblyStructure(assembly, result);

            result.IsValid = result.Errors.Count == 0;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating assembly: {AssemblyPath}", assemblyPath);
            result.IsValid = false;
            result.Errors.Add($"Assembly validation failed: {ex.Message}");
            return result;
        }
    }

    private async Task ValidateManifestAsync(
        DiscoveredPlugin plugin,
        PluginValidationResult result,
        CancellationToken cancellationToken)
    {
        _logger.LogTrace("Validating manifest for plugin: {PluginId}", plugin.Manifest.Id);

        // Basic manifest validation
        var manifestErrors = plugin.Manifest.Validate().ToList();
        foreach (var error in manifestErrors)
        {
            result.Errors.Add($"Manifest: {error}");
        }

        // Validate manifest file exists if specified
        if (!string.IsNullOrEmpty(plugin.ManifestPath) && !File.Exists(plugin.ManifestPath))
        {
            result.Warnings.Add($"Manifest file not found: {plugin.ManifestPath}");
        }

        // Validate main assembly path
        if (!File.Exists(plugin.AssemblyPath))
        {
            result.Errors.Add($"Main assembly not found: {plugin.AssemblyPath}");
        }
        else
        {
            // Check assembly file integrity
            await ValidateFileIntegrityAsync(plugin.AssemblyPath, result, cancellationToken);
        }
    }

    private async Task ValidateAssemblyAsync(
        DiscoveredPlugin plugin,
        PluginValidationResult result,
        CancellationToken cancellationToken)
    {
        _logger.LogTrace("Validating assembly for plugin: {PluginId}", plugin.Manifest.Id);

        try
        {
            using var tempContext = new PluginLoadContext(plugin.AssemblyPath, isCollectible: true);
            var assembly = tempContext.LoadPluginAssembly();

            // Validate assembly metadata
            ValidateAssemblyMetadata(assembly, plugin.Manifest, result);

            // Validate assembly structure
            ValidateAssemblyStructure(assembly, result);

            // Validate assembly dependencies
            ValidateAssemblyDependencies(assembly, result);

            // Additional security checks
            if (_options.EnableSecurityValidation)
            {
                await ValidateAssemblySecurityAsync(assembly, result, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Assembly loading failed: {ex.Message}");
        }
    }

    private async Task ValidateSecurityAsync(
        DiscoveredPlugin plugin,
        PluginValidationResult result,
        CancellationToken cancellationToken)
    {
        if (!_options.EnableSecurityValidation)
        {
            return;
        }

        _logger.LogTrace("Validating security for plugin: {PluginId}", plugin.Manifest.Id);

        // Validate digital signature if required
        if (_options.RequireDigitalSignature)
        {
            ValidateDigitalSignature(plugin.AssemblyPath, result);
        }

        // Validate trusted sources
        if (_options.TrustedSources.Count > 0)
        {
            ValidateTrustedSource(plugin, result);
        }

        // Validate file hash if provided
        if (_options.ValidateFileHash)
        {
            await ValidateFileHashAsync(plugin.AssemblyPath, result, cancellationToken);
        }

        // Check for potentially dangerous code patterns
        if (_options.ScanForDangerousCode)
        {
            await ScanForDangerousCodeAsync(plugin.AssemblyPath, result, cancellationToken);
        }
    }

    private async Task ValidateDependenciesAsync(
        DiscoveredPlugin plugin,
        PluginValidationResult result,
        CancellationToken cancellationToken)
    {
        _logger.LogTrace("Validating dependencies for plugin: {PluginId}", plugin.Manifest.Id);

        foreach (var dependency in plugin.Manifest.Dependencies)
        {
            if (string.IsNullOrEmpty(dependency.Id))
            {
                result.Errors.Add("Dependency ID cannot be empty");
                continue;
            }

            // Check if dependency is available (this would query the plugin registry)
            // For now, we'll just validate the dependency format
            if (!string.IsNullOrEmpty(dependency.MinimumVersion) &&
                !Version.TryParse(dependency.MinimumVersion, out _))
            {
                result.Errors.Add($"Invalid minimum version for dependency {dependency.Id}: {dependency.MinimumVersion}");
            }

            if (!string.IsNullOrEmpty(dependency.MaximumVersion) &&
                !Version.TryParse(dependency.MaximumVersion, out _))
            {
                result.Errors.Add($"Invalid maximum version for dependency {dependency.Id}: {dependency.MaximumVersion}");
            }

            // Add warning for missing optional dependencies
            if (!dependency.Optional)
            {
                result.Warnings.Add($"Required dependency {dependency.Id} availability not verified");
            }
        }

        // Validate runtime dependencies
        foreach (var runtimeDep in plugin.Manifest.RuntimeDependencies)
        {
            if (string.IsNullOrEmpty(runtimeDep.PackageId))
            {
                result.Errors.Add("Runtime dependency package ID cannot be empty");
            }

            if (string.IsNullOrEmpty(runtimeDep.Version))
            {
                result.Errors.Add($"Runtime dependency {runtimeDep.PackageId} version cannot be empty");
            }
        }
    }

    private async Task ValidateCompatibilityAsync(
        DiscoveredPlugin plugin,
        PluginValidationResult result,
        CancellationToken cancellationToken)
    {
        _logger.LogTrace("Validating compatibility for plugin: {PluginId}", plugin.Manifest.Id);

        // Validate shell version compatibility
        if (!string.IsNullOrEmpty(plugin.Manifest.MinimumShellVersion))
        {
            if (Version.TryParse(plugin.Manifest.MinimumShellVersion, out var minShellVersion))
            {
                if (_options.CurrentShellVersion < minShellVersion)
                {
                    result.Errors.Add($"Plugin requires shell version {minShellVersion} or higher, but current version is {_options.CurrentShellVersion}");
                }
            }
        }

        if (!string.IsNullOrEmpty(plugin.Manifest.MaximumShellVersion))
        {
            if (Version.TryParse(plugin.Manifest.MaximumShellVersion, out var maxShellVersion))
            {
                if (_options.CurrentShellVersion > maxShellVersion)
                {
                    result.Warnings.Add($"Plugin was designed for shell version {maxShellVersion} or lower, current version is {_options.CurrentShellVersion}");
                }
            }
        }

        // Validate platform compatibility
        if (plugin.Manifest.SupportedPlatforms.Count > 0)
        {
            var currentPlatform = GetCurrentPlatform();
            if (!plugin.Manifest.SupportsPlatform(currentPlatform))
            {
                result.Errors.Add($"Plugin does not support current platform: {currentPlatform}");
            }
        }

        // Validate .NET framework compatibility
        try
        {
            using var tempContext = new PluginLoadContext(plugin.AssemblyPath, isCollectible: true);
            var assembly = tempContext.LoadPluginAssembly();

            var targetFramework = assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()?.FrameworkName;
            if (!string.IsNullOrEmpty(targetFramework))
            {
                result.ValidationDetails["TargetFramework"] = targetFramework;

                // Add warning if target framework doesn't match current runtime
                if (!IsCompatibleFramework(targetFramework))
                {
                    result.Warnings.Add($"Plugin target framework {targetFramework} may not be fully compatible with current runtime");
                }
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Could not determine plugin target framework: {ex.Message}");
        }
    }

    private async Task ValidateModuleInterfaceAsync(
        DiscoveredPlugin plugin,
        PluginValidationResult result,
        CancellationToken cancellationToken)
    {
        _logger.LogTrace("Validating module interface for plugin: {PluginId}", plugin.Manifest.Id);

        try
        {
            using var tempContext = new PluginLoadContext(plugin.AssemblyPath, isCollectible: true);
            var assembly = tempContext.LoadPluginAssembly();

            // Find the entry point type
            var entryPointType = assembly.GetType(plugin.Manifest.EntryPoint);
            if (entryPointType == null)
            {
                result.Errors.Add($"Entry point type not found: {plugin.Manifest.EntryPoint}");
                return;
            }

            // Validate it implements IBusinessLogicModule
            if (!typeof(IBusinessLogicModule).IsAssignableFrom(entryPointType))
            {
                result.Errors.Add($"Entry point type {plugin.Manifest.EntryPoint} does not implement IBusinessLogicModule");
                return;
            }

            // Validate it's a concrete class
            if (entryPointType.IsAbstract || entryPointType.IsInterface)
            {
                result.Errors.Add($"Entry point type {plugin.Manifest.EntryPoint} must be a concrete class");
                return;
            }

            // Validate it has a parameterless constructor
            var constructor = entryPointType.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
            {
                result.Errors.Add($"Entry point type {plugin.Manifest.EntryPoint} must have a parameterless constructor");
                return;
            }

            result.ValidationDetails["EntryPointType"] = entryPointType.FullName ?? entryPointType.Name;
            result.ValidationDetails["HasParameterlessConstructor"] = true;
            result.ValidationDetails["ImplementsIBusinessLogicModule"] = true;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Failed to validate module interface: {ex.Message}");
        }
    }

    private void ValidateAssemblyMetadata(Assembly assembly, PluginManifest manifest, PluginValidationResult result)
    {
        var assemblyName = assembly.GetName();

        // Warn if assembly name doesn't match manifest
        if (!string.Equals(assemblyName.Name, manifest.Id, StringComparison.OrdinalIgnoreCase))
        {
            result.Warnings.Add($"Assembly name '{assemblyName.Name}' doesn't match manifest ID '{manifest.Id}'");
        }

        // Warn if assembly version doesn't match manifest
        if (assemblyName.Version != null && assemblyName.Version.ToString() != manifest.Version)
        {
            result.Warnings.Add($"Assembly version '{assemblyName.Version}' doesn't match manifest version '{manifest.Version}'");
        }

        result.ValidationDetails["AssemblyName"] = assemblyName.Name ?? "Unknown";
        result.ValidationDetails["AssemblyVersion"] = assemblyName.Version?.ToString() ?? "Unknown";
    }

    private void ValidateAssemblyStructure(Assembly assembly, PluginValidationResult result)
    {
        try
        {
            // Check for exported types
            var exportedTypes = assembly.GetExportedTypes();
            if (exportedTypes.Length == 0)
            {
                result.Warnings.Add("Assembly has no exported types");
            }

            // Check for IBusinessLogicModule implementations
            var moduleTypes = exportedTypes
                .Where(t => t.IsClass && !t.IsAbstract && typeof(IBusinessLogicModule).IsAssignableFrom(t))
                .ToList();

            if (moduleTypes.Count == 0)
            {
                result.Errors.Add("Assembly contains no IBusinessLogicModule implementations");
            }
            else if (moduleTypes.Count > 1)
            {
                result.Warnings.Add($"Assembly contains multiple IBusinessLogicModule implementations: {string.Join(", ", moduleTypes.Select(t => t.Name))}");
            }

            result.ValidationDetails["ExportedTypesCount"] = exportedTypes.Length;
            result.ValidationDetails["ModuleTypesCount"] = moduleTypes.Count;
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Could not analyze assembly structure: {ex.Message}");
        }
    }

    private void ValidateAssemblyDependencies(Assembly assembly, PluginValidationResult result)
    {
        try
        {
            var referencedAssemblies = assembly.GetReferencedAssemblies();
            var problemDependencies = new List<string>();

            foreach (var reference in referencedAssemblies)
            {
                // Check for potentially problematic references
                if (reference.Name != null && _options.ProhibitedAssemblies.Contains(reference.Name))
                {
                    problemDependencies.Add(reference.Name);
                }
            }

            if (problemDependencies.Count > 0)
            {
                result.Errors.Add($"Assembly references prohibited dependencies: {string.Join(", ", problemDependencies)}");
            }

            result.ValidationDetails["ReferencedAssembliesCount"] = referencedAssemblies.Length;
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Could not analyze assembly dependencies: {ex.Message}");
        }
    }

    private async Task ValidateAssemblySecurityAsync(Assembly assembly, PluginValidationResult result, CancellationToken cancellationToken)
    {
        // Check for security attributes
        var securityAttributes = assembly.GetCustomAttributes(typeof(System.Security.SecurityTransparentAttribute), false);
        if (securityAttributes.Length == 0 && _options.RequireSecurityTransparent)
        {
            result.Warnings.Add("Assembly is not marked as SecurityTransparent");
        }

        result.ValidationDetails["HasSecurityTransparentAttribute"] = securityAttributes.Length > 0;
    }

    private void ValidateDigitalSignature(string assemblyPath, PluginValidationResult result)
    {
        // TODO: Implement digital signature validation
        result.Warnings.Add("Digital signature validation not implemented");
    }

    private void ValidateTrustedSource(DiscoveredPlugin plugin, PluginValidationResult result)
    {
        var pluginDirectory = Path.GetDirectoryName(plugin.AssemblyPath) ?? string.Empty;
        var isTrusted = _options.TrustedSources.Any(source =>
            pluginDirectory.StartsWith(source, StringComparison.OrdinalIgnoreCase));

        if (!isTrusted)
        {
            result.Errors.Add($"Plugin is not from a trusted source. Path: {pluginDirectory}");
        }

        result.ValidationDetails["IsTrustedSource"] = isTrusted;
    }

    private async Task ValidateFileIntegrityAsync(string filePath, PluginValidationResult result, CancellationToken cancellationToken)
    {
        try
        {
            // Basic file integrity check
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length == 0)
            {
                result.Errors.Add($"File is empty: {filePath}");
            }

            result.ValidationDetails["FileSize"] = fileInfo.Length;
            result.ValidationDetails["LastModified"] = fileInfo.LastWriteTimeUtc;
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Could not validate file integrity: {ex.Message}");
        }
    }

    private async Task ValidateFileHashAsync(string filePath, PluginValidationResult result, CancellationToken cancellationToken)
    {
        try
        {
            using var fileStream = File.OpenRead(filePath);
            using var sha256 = SHA256.Create();
            var hash = await sha256.ComputeHashAsync(fileStream, cancellationToken);
            var hashString = Convert.ToHexString(hash);

            result.ValidationDetails["SHA256Hash"] = hashString;

            // TODO: Compare against known good hashes or signature
            _logger.LogTrace("File hash computed for {FilePath}: {Hash}", filePath, hashString);
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Could not compute file hash: {ex.Message}");
        }
    }

    private async Task ScanForDangerousCodeAsync(string assemblyPath, PluginValidationResult result, CancellationToken cancellationToken)
    {
        // TODO: Implement dangerous code pattern detection
        // This could involve:
        // - Scanning for use of dangerous APIs
        // - Checking for code injection patterns
        // - Validating permission usage
        result.Warnings.Add("Dangerous code scanning not implemented");
    }

    private string GetCurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
            return "Windows";
        if (OperatingSystem.IsLinux())
            return "Linux";
        if (OperatingSystem.IsMacOS())
            return "macOS";
        return "Unknown";
    }

    private bool IsCompatibleFramework(string targetFramework)
    {
        // Basic compatibility check - would need more sophisticated logic
        return targetFramework.Contains(".NETCoreApp") || targetFramework.Contains(".NET");
    }
}

/// <summary>
/// Interface for plugin validation service.
/// </summary>
public interface IPluginValidator
{
    /// <summary>
    /// Validates a discovered plugin comprehensively.
    /// </summary>
    /// <param name="plugin">The plugin to validate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The validation result.</returns>
    Task<PluginValidationResult> ValidatePluginAsync(DiscoveredPlugin plugin, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates only the plugin manifest.
    /// </summary>
    /// <param name="manifest">The manifest to validate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The validation result.</returns>
    Task<PluginValidationResult> ValidateManifestAsync(PluginManifest manifest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates plugin assembly structure and contents.
    /// </summary>
    /// <param name="assemblyPath">The path to the assembly to validate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The validation result.</returns>
    Task<PluginValidationResult> ValidateAssemblyAsync(string assemblyPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of plugin validation.
/// </summary>
public class PluginValidationResult
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
    /// Gets or sets whether the plugin passed validation.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public List<string> Errors { get; } = new();

    /// <summary>
    /// Gets the validation warnings.
    /// </summary>
    public List<string> Warnings { get; } = new();

    /// <summary>
    /// Gets the validation details and metadata.
    /// </summary>
    public Dictionary<string, object> ValidationDetails { get; } = new();

    /// <summary>
    /// Gets or sets the validation timestamp.
    /// </summary>
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the validation duration.
    /// </summary>
    public TimeSpan ValidationDuration { get; set; }
}

/// <summary>
/// Configuration options for plugin validation.
/// </summary>
public class PluginValidationOptions
{
    /// <summary>
    /// Gets or sets whether to enable security validation.
    /// </summary>
    public bool EnableSecurityValidation { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to require digital signatures.
    /// </summary>
    public bool RequireDigitalSignature { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to require SecurityTransparent attribute.
    /// </summary>
    public bool RequireSecurityTransparent { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to validate file hashes.
    /// </summary>
    public bool ValidateFileHash { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to scan for dangerous code patterns.
    /// </summary>
    public bool ScanForDangerousCode { get; set; } = true;

    /// <summary>
    /// Gets or sets the trusted source directories.
    /// </summary>
    public List<string> TrustedSources { get; set; } = new();

    /// <summary>
    /// Gets or sets the prohibited assembly names.
    /// </summary>
    public List<string> ProhibitedAssemblies { get; set; } = new()
    {
        "System.Management",
        "System.DirectoryServices",
        "Microsoft.Win32.Registry"
    };

    /// <summary>
    /// Gets or sets the current shell version for compatibility checks.
    /// </summary>
    public Version CurrentShellVersion { get; set; } = new Version(1, 0, 0);

    /// <summary>
    /// Gets or sets the maximum allowed assembly size in bytes.
    /// </summary>
    public long MaxAssemblySizeBytes { get; set; } = 50 * 1024 * 1024; // 50MB

    /// <summary>
    /// Gets or sets the validation timeout in milliseconds.
    /// </summary>
    public int ValidationTimeoutMs { get; set; } = 30000;
}