using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetShell.Abstractions;

/// <summary>
/// Interface that all business logic modules must implement to be loaded by the Shell.
/// Provides lifecycle management, dependency injection registration, and module metadata.
/// </summary>
public interface IBusinessLogicModule
{
    /// <summary>
    /// Gets the name of the module.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the version of the module.
    /// </summary>
    Version Version { get; }

    /// <summary>
    /// Gets the description of the module.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the author or organization that created the module.
    /// </summary>
    string? Author { get; }

    /// <summary>
    /// Gets the module dependencies (other modules this module requires).
    /// </summary>
    IEnumerable<ModuleDependency> Dependencies { get; }

    /// <summary>
    /// Gets the minimum shell version required to run this module.
    /// </summary>
    Version MinimumShellVersion { get; }

    /// <summary>
    /// Gets additional metadata about the module.
    /// </summary>
    ModuleMetadata Metadata { get; }

    /// <summary>
    /// Gets a value indicating whether the module is enabled.
    /// Can be used for runtime module enable/disable functionality.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Called during module discovery to validate if the module can be loaded.
    /// This is called before any other lifecycle methods.
    /// </summary>
    /// <param name="context">The validation context containing shell and environment information.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A validation result indicating whether the module can be loaded.</returns>
    Task<ModuleValidationResult> ValidateAsync(ModuleValidationContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called during module initialization to register services with the DI container.
    /// This is the first lifecycle method called after successful validation.
    /// </summary>
    /// <param name="services">The service collection to register services with.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    Task OnInitializeAsync(IServiceCollection services, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called during application configuration to configure the middleware pipeline.
    /// This is called after all modules have been initialized.
    /// </summary>
    /// <param name="app">The application builder to configure middleware with.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous configuration operation.</returns>
    Task OnConfigureAsync(IApplicationBuilder app, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when the module should start its services and background tasks.
    /// This is called after the application has been configured.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous start operation.</returns>
    Task OnStartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when the module should stop its services gracefully.
    /// This is called during application shutdown.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous stop operation.</returns>
    Task OnStopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when the module is being unloaded (hot reload scenarios).
    /// Allows the module to clean up resources that won't be handled by garbage collection.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous unload operation.</returns>
    Task OnUnloadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when the module configuration has been updated.
    /// Allows the module to react to configuration changes at runtime.
    /// </summary>
    /// <param name="newConfiguration">The new configuration values.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous configuration update operation.</returns>
    Task OnConfigurationChangedAsync(IReadOnlyDictionary<string, object> newConfiguration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called to check if the module is healthy and functioning properly.
    /// Used by health check systems to monitor module status.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A health check result indicating the module's current status.</returns>
    Task<ModuleHealthResult> CheckHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a dependency that a module requires.
/// </summary>
public class ModuleDependency
{
    /// <summary>
    /// Gets or sets the name of the required module.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the minimum version of the required module.
    /// </summary>
    public Version? MinimumVersion { get; set; }

    /// <summary>
    /// Gets or sets the maximum version of the required module.
    /// </summary>
    public Version? MaximumVersion { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this dependency is optional.
    /// </summary>
    public bool IsOptional { get; set; }

    /// <summary>
    /// Gets or sets the reason this dependency is required.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Checks if the provided version satisfies this dependency.
    /// </summary>
    /// <param name="version">The version to check.</param>
    /// <returns>True if the version satisfies the dependency; otherwise, false.</returns>
    public bool IsSatisfiedBy(Version version)
    {
        if (MinimumVersion != null && version < MinimumVersion)
            return false;

        if (MaximumVersion != null && version > MaximumVersion)
            return false;

        return true;
    }
}

/// <summary>
/// Contains metadata about a module.
/// </summary>
public class ModuleMetadata
{
    /// <summary>
    /// Gets or sets the module category (e.g., "Business Logic", "Integration", "Utility").
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets the module tags for categorization and search.
    /// </summary>
    public IList<string> Tags { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the module website or repository URL.
    /// </summary>
    public string? Website { get; set; }

    /// <summary>
    /// Gets or sets the license information.
    /// </summary>
    public string? License { get; set; }

    /// <summary>
    /// Gets or sets the copyright information.
    /// </summary>
    public string? Copyright { get; set; }

    /// <summary>
    /// Gets or sets additional custom metadata.
    /// </summary>
    public IDictionary<string, object> CustomProperties { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the supported platforms (e.g., "Windows", "Linux", "macOS").
    /// </summary>
    public IList<string> SupportedPlatforms { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the module's icon or logo (Base64 encoded or URL).
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Gets or sets the release notes for this version.
    /// </summary>
    public string? ReleaseNotes { get; set; }
}

/// <summary>
/// Contains context information for module validation.
/// </summary>
public class ModuleValidationContext
{
    /// <summary>
    /// Gets or sets the shell version.
    /// </summary>
    public Version ShellVersion { get; set; } = new Version(1, 0, 0);

    /// <summary>
    /// Gets or sets the runtime environment information.
    /// </summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the available service types in the shell.
    /// </summary>
    public IList<Type> AvailableServices { get; set; } = new List<Type>();

    /// <summary>
    /// Gets or sets the loaded modules.
    /// </summary>
    public IList<IBusinessLogicModule> LoadedModules { get; set; } = new List<IBusinessLogicModule>();

    /// <summary>
    /// Gets or sets the configuration values available to the module.
    /// </summary>
    public IReadOnlyDictionary<string, object> Configuration { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets additional context properties.
    /// </summary>
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Represents the result of module validation.
/// </summary>
public class ModuleValidationResult
{
    /// <summary>
    /// Gets a value indicating whether the validation was successful.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the validation error messages, if any.
    /// </summary>
    public IList<string> Errors { get; init; } = new List<string>();

    /// <summary>
    /// Gets the validation warning messages, if any.
    /// </summary>
    public IList<string> Warnings { get; init; } = new List<string>();

    /// <summary>
    /// Gets additional validation context information.
    /// </summary>
    public IDictionary<string, object> Context { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <param name="warnings">Optional validation warnings.</param>
    /// <param name="context">Optional validation context.</param>
    /// <returns>A successful validation result.</returns>
    public static ModuleValidationResult Success(IList<string>? warnings = null, IDictionary<string, object>? context = null)
    {
        return new ModuleValidationResult
        {
            IsValid = true,
            Warnings = warnings ?? new List<string>(),
            Context = context ?? new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    /// <param name="warnings">Optional validation warnings.</param>
    /// <param name="context">Optional validation context.</param>
    /// <returns>A failed validation result.</returns>
    public static ModuleValidationResult Failure(IList<string> errors, IList<string>? warnings = null, IDictionary<string, object>? context = null)
    {
        return new ModuleValidationResult
        {
            IsValid = false,
            Errors = errors,
            Warnings = warnings ?? new List<string>(),
            Context = context ?? new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// Creates a failed validation result with a single error message.
    /// </summary>
    /// <param name="error">The validation error message.</param>
    /// <returns>A failed validation result.</returns>
    public static ModuleValidationResult Failure(string error)
    {
        return new ModuleValidationResult
        {
            IsValid = false,
            Errors = new List<string> { error }
        };
    }
}

/// <summary>
/// Represents the health status of a module.
/// </summary>
public class ModuleHealthResult
{
    /// <summary>
    /// Gets the health status of the module.
    /// </summary>
    public ModuleHealthStatus Status { get; init; }

    /// <summary>
    /// Gets the description of the health status.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets additional health data.
    /// </summary>
    public IDictionary<string, object> Data { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets the timestamp when the health check was performed.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the duration of the health check.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Creates a healthy result.
    /// </summary>
    /// <param name="description">Optional description.</param>
    /// <param name="data">Optional health data.</param>
    /// <param name="duration">Optional health check duration.</param>
    /// <returns>A healthy result.</returns>
    public static ModuleHealthResult Healthy(string? description = null, IDictionary<string, object>? data = null, TimeSpan duration = default)
    {
        return new ModuleHealthResult
        {
            Status = ModuleHealthStatus.Healthy,
            Description = description,
            Data = data ?? new Dictionary<string, object>(),
            Duration = duration
        };
    }

    /// <summary>
    /// Creates a degraded result.
    /// </summary>
    /// <param name="description">The description of the degraded status.</param>
    /// <param name="data">Optional health data.</param>
    /// <param name="duration">Optional health check duration.</param>
    /// <returns>A degraded result.</returns>
    public static ModuleHealthResult Degraded(string description, IDictionary<string, object>? data = null, TimeSpan duration = default)
    {
        return new ModuleHealthResult
        {
            Status = ModuleHealthStatus.Degraded,
            Description = description,
            Data = data ?? new Dictionary<string, object>(),
            Duration = duration
        };
    }

    /// <summary>
    /// Creates an unhealthy result.
    /// </summary>
    /// <param name="description">The description of the unhealthy status.</param>
    /// <param name="data">Optional health data.</param>
    /// <param name="duration">Optional health check duration.</param>
    /// <returns>An unhealthy result.</returns>
    public static ModuleHealthResult Unhealthy(string description, IDictionary<string, object>? data = null, TimeSpan duration = default)
    {
        return new ModuleHealthResult
        {
            Status = ModuleHealthStatus.Unhealthy,
            Description = description,
            Data = data ?? new Dictionary<string, object>(),
            Duration = duration
        };
    }
}

/// <summary>
/// Enumeration of module health status values.
/// </summary>
public enum ModuleHealthStatus
{
    /// <summary>
    /// The module is healthy and functioning normally.
    /// </summary>
    Healthy,

    /// <summary>
    /// The module is functioning but with degraded performance or capabilities.
    /// </summary>
    Degraded,

    /// <summary>
    /// The module is not functioning properly.
    /// </summary>
    Unhealthy
}