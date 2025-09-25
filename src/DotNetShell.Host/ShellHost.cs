namespace DotNetShell.Host;

/// <summary>
/// The main shell host that manages the application lifecycle and module loading.
/// </summary>
public class ShellHost
{
    private readonly WebApplicationBuilder _builder;
    private readonly ILogger<ShellHost> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellHost"/> class.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    public ShellHost(WebApplicationBuilder builder)
    {
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));

        // Create a temporary logger for initialization
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<ShellHost>();
    }

    /// <summary>
    /// Configures the HTTP request pipeline.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>A task that represents the asynchronous configuration operation.</returns>
    public async Task ConfigurePipelineAsync(WebApplication app)
    {
        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            var swaggerEnabled = app.Configuration.GetValue<bool>("Shell:Swagger:Enabled", true);
            if (swaggerEnabled)
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DotNet Shell API v1");
                    c.RoutePrefix = "swagger";
                });
            }
        }

        // Security headers
        app.UseHttpsRedirection();

        // CORS (if configured)
        // app.UseCors();

        // Authentication & Authorization
        // app.UseAuthentication();
        // app.UseAuthorization();

        // Health checks
        app.UseHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = _ => false // Only run liveness checks
        });

        app.UseHealthChecks("/health/ready");

        // Routing
        app.UseRouting();

        // Map controllers
        app.MapControllers();

        // Map additional endpoints
        app.MapGet("/", () => new
        {
            Name = "DotNet Shell",
            Version = "1.0.0",
            Status = "Running",
            Timestamp = DateTime.UtcNow
        });

        await Task.CompletedTask;
    }

    /// <summary>
    /// Runs the shell host application.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>A task that represents the running application.</returns>
    public async Task RunAsync(WebApplication app)
    {
        try
        {
            _logger.LogInformation("Starting DotNet Shell Host...");
            _logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
            _logger.LogInformation("Version: {Version}", app.Configuration["Shell:Version"]);

            // TODO: Discover and load modules here
            await DiscoverAndLoadModulesAsync();

            _logger.LogInformation("DotNet Shell Host started successfully");

            // Run the application
            await app.RunAsync();
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to start DotNet Shell Host");
            throw;
        }
    }

    /// <summary>
    /// Discovers and loads business logic modules.
    /// </summary>
    /// <returns>A task that represents the asynchronous module loading operation.</returns>
    private async Task DiscoverAndLoadModulesAsync()
    {
        var modulesPath = _builder.Configuration["Shell:Modules:Source"] ?? "./modules";
        var autoLoad = _builder.Configuration.GetValue<bool>("Shell:Modules:AutoLoad", true);

        _logger.LogInformation("Module discovery - Path: {ModulesPath}, AutoLoad: {AutoLoad}", modulesPath, autoLoad);

        if (!autoLoad)
        {
            _logger.LogInformation("Module auto-loading is disabled");
            return;
        }

        if (!Directory.Exists(modulesPath))
        {
            _logger.LogWarning("Modules directory does not exist: {ModulesPath}", modulesPath);
            return;
        }

        // TODO: Implement actual module discovery and loading
        var moduleFiles = Directory.GetFiles(modulesPath, "*.dll", SearchOption.TopDirectoryOnly);
        _logger.LogInformation("Found {ModuleCount} potential module files", moduleFiles.Length);

        foreach (var moduleFile in moduleFiles)
        {
            _logger.LogDebug("Found potential module: {ModuleFile}", moduleFile);
        }

        await Task.CompletedTask;
    }
}