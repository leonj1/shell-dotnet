using DotNetShell.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

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
    public Task OnInitializeAsync(IServiceCollection services)
    {
        // Register module-specific services
        services.AddScoped<ISampleService, SampleService>();
        services.AddTransient<ISampleRepository, SampleRepository>();

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnConfigureAsync(IApplicationBuilder app)
    {
        // Configure module-specific middleware if needed
        // For example: app.UseMiddleware<SampleMiddleware>();

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnStartAsync(CancellationToken cancellationToken)
    {
        // Perform any startup operations
        // For example: initialize connections, start background services, etc.

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnStopAsync(CancellationToken cancellationToken)
    {
        // Perform any cleanup operations
        // For example: dispose resources, stop background services, etc.

        return Task.CompletedTask;
    }
}