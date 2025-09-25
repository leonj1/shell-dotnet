using DotNetShell.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SampleBusinessLogic.Controllers;
using SampleBusinessLogic.Repository;
using SampleBusinessLogic.Services;

namespace SampleBusinessLogic;

/// <summary>
/// Sample business logic module demonstrating the plugin architecture.
/// </summary>
public class SampleModule : IBusinessLogicModule
{
    /// <inheritdoc />
    public string Name => "SampleBusinessLogic";

    /// <inheritdoc />
    public Version Version => new Version(1, 0, 0);

    /// <inheritdoc />
    public string Description => "A sample business logic module demonstrating the Shell framework capabilities.";

    /// <inheritdoc />
    public string? Author => "DotNet Shell Team";

    /// <inheritdoc />
    public IEnumerable<ModuleDependency> Dependencies => Array.Empty<ModuleDependency>();

    /// <inheritdoc />
    public Version MinimumShellVersion => new Version(1, 0, 0);

    /// <inheritdoc />
    public ModuleMetadata Metadata => new ModuleMetadata
    {
        Tags = new[] { "sample", "demo", "business-logic" },
        Category = "Sample",
        Website = "https://github.com/company/shell-dotnet-core",
        Icon = null,
        License = "MIT"
    };

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public Task<ModuleValidationResult> ValidateAsync(ModuleValidationContext context, CancellationToken cancellationToken = default)
    {
        // Perform validation checks
        var result = new ModuleValidationResult
        {
            IsValid = true,
            Errors = new List<string>()
        };

        // Check if running in a supported environment
        if (context != null && !string.IsNullOrEmpty(context.Environment))
        {
            // Validation passed
        }

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task OnInitializeAsync(IServiceCollection services, CancellationToken cancellationToken = default)
    {
        // Register module-specific services
        services.AddScoped<ISampleService, SampleService>();
        services.AddTransient<ISampleRepository, SampleRepository>();

        // Note: Controller registration is typically done at the host level
        // The module just needs to register its services

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnConfigureAsync(IApplicationBuilder app, CancellationToken cancellationToken = default)
    {
        // Configure module-specific middleware if needed
        // For example: app.UseMiddleware<SampleMiddleware>();

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnStartAsync(CancellationToken cancellationToken = default)
    {
        // Perform any startup operations
        // For example: initialize connections, start background services, etc.
        Console.WriteLine($"[{Name}] Module started successfully.");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnStopAsync(CancellationToken cancellationToken = default)
    {
        // Perform any cleanup operations
        // For example: dispose resources, stop background services, etc.
        Console.WriteLine($"[{Name}] Module stopped successfully.");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnUnloadAsync(CancellationToken cancellationToken = default)
    {
        // Clean up resources that won't be handled by garbage collection
        Console.WriteLine($"[{Name}] Module unloaded successfully.");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnConfigurationChangedAsync(IReadOnlyDictionary<string, object> newConfiguration, CancellationToken cancellationToken = default)
    {
        // React to configuration changes
        if (newConfiguration != null && newConfiguration.Count > 0)
        {
            Console.WriteLine($"[{Name}] Configuration updated with {newConfiguration.Count} changes.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ModuleHealthResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        // Check module health
        var result = new ModuleHealthResult
        {
            Status = ModuleHealthStatus.Healthy,
            Description = "Module is functioning normally"
        };

        return Task.FromResult(result);
    }
}