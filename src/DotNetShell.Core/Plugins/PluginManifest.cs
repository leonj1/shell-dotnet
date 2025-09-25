using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DotNetShell.Core.Plugins;

/// <summary>
/// Represents a plugin manifest that contains metadata and configuration information for a plugin.
/// </summary>
public class PluginManifest
{
    /// <summary>
    /// Gets or sets the plugin identifier. Must be unique across the system.
    /// </summary>
    [Required]
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the plugin name.
    /// </summary>
    [Required]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the plugin version.
    /// </summary>
    [Required]
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the plugin description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the plugin author or organization.
    /// </summary>
    [JsonPropertyName("author")]
    public string? Author { get; set; }

    /// <summary>
    /// Gets or sets the plugin website or repository URL.
    /// </summary>
    [JsonPropertyName("website")]
    public string? Website { get; set; }

    /// <summary>
    /// Gets or sets the license information.
    /// </summary>
    [JsonPropertyName("license")]
    public string? License { get; set; }

    /// <summary>
    /// Gets or sets the copyright information.
    /// </summary>
    [JsonPropertyName("copyright")]
    public string? Copyright { get; set; }

    /// <summary>
    /// Gets or sets the plugin category.
    /// </summary>
    [JsonPropertyName("category")]
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets the plugin tags for categorization and search.
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Gets or sets the supported platforms.
    /// </summary>
    [JsonPropertyName("supportedPlatforms")]
    public List<string> SupportedPlatforms { get; set; } = new();

    /// <summary>
    /// Gets or sets the main assembly path relative to the manifest file.
    /// </summary>
    [Required]
    [JsonPropertyName("mainAssembly")]
    public string MainAssembly { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the entry point type name that implements IBusinessLogicModule.
    /// </summary>
    [Required]
    [JsonPropertyName("entryPoint")]
    public string EntryPoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the minimum shell version required.
    /// </summary>
    [JsonPropertyName("minimumShellVersion")]
    public string? MinimumShellVersion { get; set; }

    /// <summary>
    /// Gets or sets the maximum shell version supported.
    /// </summary>
    [JsonPropertyName("maximumShellVersion")]
    public string? MaximumShellVersion { get; set; }

    /// <summary>
    /// Gets or sets the plugin dependencies.
    /// </summary>
    [JsonPropertyName("dependencies")]
    public List<PluginDependency> Dependencies { get; set; } = new();

    /// <summary>
    /// Gets or sets the runtime dependencies (NuGet packages).
    /// </summary>
    [JsonPropertyName("runtimeDependencies")]
    public List<RuntimeDependency> RuntimeDependencies { get; set; } = new();

    /// <summary>
    /// Gets or sets the plugin capabilities.
    /// </summary>
    [JsonPropertyName("capabilities")]
    public PluginCapabilities Capabilities { get; set; } = new();

    /// <summary>
    /// Gets or sets the plugin configuration schema.
    /// </summary>
    [JsonPropertyName("configurationSchema")]
    public PluginConfigurationSchema? ConfigurationSchema { get; set; }

    /// <summary>
    /// Gets or sets the plugin permissions required.
    /// </summary>
    [JsonPropertyName("permissions")]
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// Gets or sets the plugin resources.
    /// </summary>
    [JsonPropertyName("resources")]
    public PluginResources? Resources { get; set; }

    /// <summary>
    /// Gets or sets additional custom properties.
    /// </summary>
    [JsonPropertyName("customProperties")]
    public Dictionary<string, object> CustomProperties { get; set; } = new();

    /// <summary>
    /// Gets or sets the manifest format version.
    /// </summary>
    [JsonPropertyName("manifestVersion")]
    public string ManifestVersion { get; set; } = "1.0";

    /// <summary>
    /// Gets or sets the build information.
    /// </summary>
    [JsonPropertyName("build")]
    public PluginBuildInfo? Build { get; set; }

    /// <summary>
    /// Gets or sets the plugin health check configuration.
    /// </summary>
    [JsonPropertyName("healthCheck")]
    public PluginHealthCheckConfig? HealthCheck { get; set; }

    /// <summary>
    /// Validates the plugin manifest.
    /// </summary>
    /// <returns>A collection of validation errors, or empty if valid.</returns>
    public IEnumerable<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Id))
        {
            errors.Add("Plugin ID is required and cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            errors.Add("Plugin name is required and cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(Version))
        {
            errors.Add("Plugin version is required and cannot be empty.");
        }
        else if (!System.Version.TryParse(Version, out _))
        {
            errors.Add("Plugin version must be a valid version string (e.g., '1.0.0').");
        }

        if (string.IsNullOrWhiteSpace(MainAssembly))
        {
            errors.Add("Main assembly path is required and cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(EntryPoint))
        {
            errors.Add("Entry point type name is required and cannot be empty.");
        }

        if (!string.IsNullOrWhiteSpace(MinimumShellVersion) && !System.Version.TryParse(MinimumShellVersion, out _))
        {
            errors.Add("Minimum shell version must be a valid version string.");
        }

        if (!string.IsNullOrWhiteSpace(MaximumShellVersion) && !System.Version.TryParse(MaximumShellVersion, out _))
        {
            errors.Add("Maximum shell version must be a valid version string.");
        }

        // Validate dependencies
        foreach (var dependency in Dependencies)
        {
            var dependencyErrors = dependency.Validate();
            errors.AddRange(dependencyErrors.Select(e => $"Dependency '{dependency.Id}': {e}"));
        }

        // Validate runtime dependencies
        foreach (var runtimeDep in RuntimeDependencies)
        {
            var runtimeDepErrors = runtimeDep.Validate();
            errors.AddRange(runtimeDepErrors.Select(e => $"Runtime dependency '{runtimeDep.PackageId}': {e}"));
        }

        return errors;
    }

    /// <summary>
    /// Checks if the plugin is compatible with the specified shell version.
    /// </summary>
    /// <param name="shellVersion">The shell version to check against.</param>
    /// <returns>True if compatible; otherwise, false.</returns>
    public bool IsCompatibleWith(Version shellVersion)
    {
        if (System.Version.TryParse(MinimumShellVersion, out var minVersion) && shellVersion < minVersion)
        {
            return false;
        }

        if (System.Version.TryParse(MaximumShellVersion, out var maxVersion) && shellVersion > maxVersion)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if the plugin supports the specified platform.
    /// </summary>
    /// <param name="platform">The platform to check (e.g., "Windows", "Linux", "macOS").</param>
    /// <returns>True if supported; otherwise, false.</returns>
    public bool SupportsPlatform(string platform)
    {
        if (SupportedPlatforms.Count == 0)
        {
            return true; // No platform restrictions
        }

        return SupportedPlatforms.Contains(platform, StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Represents a plugin dependency.
/// </summary>
public class PluginDependency
{
    /// <summary>
    /// Gets or sets the dependency plugin identifier.
    /// </summary>
    [Required]
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the minimum version required.
    /// </summary>
    [JsonPropertyName("minimumVersion")]
    public string? MinimumVersion { get; set; }

    /// <summary>
    /// Gets or sets the maximum version supported.
    /// </summary>
    [JsonPropertyName("maximumVersion")]
    public string? MaximumVersion { get; set; }

    /// <summary>
    /// Gets or sets whether this dependency is optional.
    /// </summary>
    [JsonPropertyName("optional")]
    public bool Optional { get; set; }

    /// <summary>
    /// Gets or sets the reason for this dependency.
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    /// <summary>
    /// Validates the plugin dependency.
    /// </summary>
    /// <returns>A collection of validation errors, or empty if valid.</returns>
    public IEnumerable<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Id))
        {
            errors.Add("Dependency ID is required and cannot be empty.");
        }

        if (!string.IsNullOrWhiteSpace(MinimumVersion) && !System.Version.TryParse(MinimumVersion, out _))
        {
            errors.Add("Minimum version must be a valid version string.");
        }

        if (!string.IsNullOrWhiteSpace(MaximumVersion) && !System.Version.TryParse(MaximumVersion, out _))
        {
            errors.Add("Maximum version must be a valid version string.");
        }

        return errors;
    }

    /// <summary>
    /// Checks if the specified version satisfies this dependency.
    /// </summary>
    /// <param name="version">The version to check.</param>
    /// <returns>True if satisfied; otherwise, false.</returns>
    public bool IsSatisfiedBy(Version version)
    {
        if (System.Version.TryParse(MinimumVersion, out var minVersion) && version < minVersion)
        {
            return false;
        }

        if (System.Version.TryParse(MaximumVersion, out var maxVersion) && version > maxVersion)
        {
            return false;
        }

        return true;
    }
}

/// <summary>
/// Represents a runtime dependency (NuGet package).
/// </summary>
public class RuntimeDependency
{
    /// <summary>
    /// Gets or sets the package identifier.
    /// </summary>
    [Required]
    [JsonPropertyName("packageId")]
    public string PackageId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the package version.
    /// </summary>
    [Required]
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this dependency is optional.
    /// </summary>
    [JsonPropertyName("optional")]
    public bool Optional { get; set; }

    /// <summary>
    /// Validates the runtime dependency.
    /// </summary>
    /// <returns>A collection of validation errors, or empty if valid.</returns>
    public IEnumerable<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(PackageId))
        {
            errors.Add("Package ID is required and cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(Version))
        {
            errors.Add("Version is required and cannot be empty.");
        }

        return errors;
    }
}

/// <summary>
/// Represents plugin capabilities.
/// </summary>
public class PluginCapabilities
{
    /// <summary>
    /// Gets or sets whether the plugin supports hot reload.
    /// </summary>
    [JsonPropertyName("supportsHotReload")]
    public bool SupportsHotReload { get; set; }

    /// <summary>
    /// Gets or sets whether the plugin supports configuration changes at runtime.
    /// </summary>
    [JsonPropertyName("supportsConfigurationReload")]
    public bool SupportsConfigurationReload { get; set; }

    /// <summary>
    /// Gets or sets whether the plugin supports health checks.
    /// </summary>
    [JsonPropertyName("supportsHealthChecks")]
    public bool SupportsHealthChecks { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the plugin supports graceful shutdown.
    /// </summary>
    [JsonPropertyName("supportsGracefulShutdown")]
    public bool SupportsGracefulShutdown { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the plugin requires elevated permissions.
    /// </summary>
    [JsonPropertyName("requiresElevatedPermissions")]
    public bool RequiresElevatedPermissions { get; set; }

    /// <summary>
    /// Gets or sets whether the plugin provides web endpoints.
    /// </summary>
    [JsonPropertyName("providesWebEndpoints")]
    public bool ProvidesWebEndpoints { get; set; }

    /// <summary>
    /// Gets or sets whether the plugin provides background services.
    /// </summary>
    [JsonPropertyName("providesBackgroundServices")]
    public bool ProvidesBackgroundServices { get; set; }

    /// <summary>
    /// Gets or sets custom capabilities.
    /// </summary>
    [JsonPropertyName("customCapabilities")]
    public Dictionary<string, object> CustomCapabilities { get; set; } = new();
}

/// <summary>
/// Represents plugin configuration schema.
/// </summary>
public class PluginConfigurationSchema
{
    /// <summary>
    /// Gets or sets the configuration section name.
    /// </summary>
    [JsonPropertyName("sectionName")]
    public string? SectionName { get; set; }

    /// <summary>
    /// Gets or sets the JSON schema for configuration validation.
    /// </summary>
    [JsonPropertyName("jsonSchema")]
    public string? JsonSchema { get; set; }

    /// <summary>
    /// Gets or sets the default configuration values.
    /// </summary>
    [JsonPropertyName("defaultValues")]
    public Dictionary<string, object> DefaultValues { get; set; } = new();
}

/// <summary>
/// Represents plugin resources.
/// </summary>
public class PluginResources
{
    /// <summary>
    /// Gets or sets the plugin icon (Base64 encoded or relative path).
    /// </summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    /// <summary>
    /// Gets or sets the documentation files.
    /// </summary>
    [JsonPropertyName("documentation")]
    public List<string> Documentation { get; set; } = new();

    /// <summary>
    /// Gets or sets the localization resources.
    /// </summary>
    [JsonPropertyName("localization")]
    public Dictionary<string, string> Localization { get; set; } = new();

    /// <summary>
    /// Gets or sets additional resource files.
    /// </summary>
    [JsonPropertyName("additionalFiles")]
    public List<string> AdditionalFiles { get; set; } = new();
}

/// <summary>
/// Represents plugin build information.
/// </summary>
public class PluginBuildInfo
{
    /// <summary>
    /// Gets or sets the build timestamp.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the build number.
    /// </summary>
    [JsonPropertyName("buildNumber")]
    public string? BuildNumber { get; set; }

    /// <summary>
    /// Gets or sets the commit hash.
    /// </summary>
    [JsonPropertyName("commitHash")]
    public string? CommitHash { get; set; }

    /// <summary>
    /// Gets or sets the branch name.
    /// </summary>
    [JsonPropertyName("branch")]
    public string? Branch { get; set; }

    /// <summary>
    /// Gets or sets the target framework.
    /// </summary>
    [JsonPropertyName("targetFramework")]
    public string? TargetFramework { get; set; }

    /// <summary>
    /// Gets or sets the build configuration.
    /// </summary>
    [JsonPropertyName("configuration")]
    public string? Configuration { get; set; }
}

/// <summary>
/// Represents plugin health check configuration.
/// </summary>
public class PluginHealthCheckConfig
{
    /// <summary>
    /// Gets or sets whether health checks are enabled.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the health check interval in milliseconds.
    /// </summary>
    [JsonPropertyName("intervalMs")]
    public int IntervalMs { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the health check timeout in milliseconds.
    /// </summary>
    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the number of consecutive failures before marking as unhealthy.
    /// </summary>
    [JsonPropertyName("failureThreshold")]
    public int FailureThreshold { get; set; } = 3;

    /// <summary>
    /// Gets or sets the number of consecutive successes before marking as healthy.
    /// </summary>
    [JsonPropertyName("successThreshold")]
    public int SuccessThreshold { get; set; } = 1;

    /// <summary>
    /// Gets or sets additional health check configuration.
    /// </summary>
    [JsonPropertyName("additionalConfig")]
    public Dictionary<string, object> AdditionalConfig { get; set; } = new();
}