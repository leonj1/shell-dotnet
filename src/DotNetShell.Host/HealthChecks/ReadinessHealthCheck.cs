using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetShell.Host.HealthChecks;

/// <summary>
/// Readiness health check that indicates the application is ready to receive traffic.
/// This includes checking module status and dependencies.
/// </summary>
public class ReadinessHealthCheck : IHealthCheck
{
    private readonly ILogger<ReadinessHealthCheck> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReadinessHealthCheck"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="serviceProvider">The service provider.</param>
    public ReadinessHealthCheck(
        ILogger<ReadinessHealthCheck> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Runs the health check, returning the status of the component being checked.
    /// </summary>
    /// <param name="context">A context object associated with the current execution.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the health check.</param>
    /// <returns>A <see cref="Task{HealthCheckResult}"/> that completes when the health check has finished.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var data = new Dictionary<string, object>
            {
                ["timestamp"] = DateTime.UtcNow,
                ["status"] = "ready"
            };

            var isHealthy = true;
            var messages = new List<string>();

            // Check configuration
            if (!CheckConfiguration())
            {
                isHealthy = false;
                messages.Add("Configuration validation failed");
            }
            else
            {
                data["configuration"] = "valid";
            }

            // Check services
            var serviceStatus = CheckCoreServices();
            data["services"] = serviceStatus;
            if (!serviceStatus.All(kvp => (bool)kvp.Value))
            {
                isHealthy = false;
                messages.Add("Some core services are not available");
            }

            // Check modules (when module system is implemented)
            var moduleStatus = await CheckModulesAsync(cancellationToken);
            data["modules"] = moduleStatus;

            // Check external dependencies
            var dependencyStatus = await CheckExternalDependenciesAsync(cancellationToken);
            data["dependencies"] = dependencyStatus;
            if (!dependencyStatus.All(kvp => (bool)kvp.Value))
            {
                isHealthy = false;
                messages.Add("Some external dependencies are not available");
            }

            var message = isHealthy
                ? "Application is ready to receive traffic"
                : string.Join("; ", messages);

            _logger.LogDebug("Readiness health check completed with status: {Status}", isHealthy ? "Healthy" : "Unhealthy");

            return isHealthy
                ? HealthCheckResult.Healthy(message, data)
                : HealthCheckResult.Unhealthy(message, data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Readiness health check failed");
            return HealthCheckResult.Unhealthy("Readiness check failed", ex);
        }
    }

    private bool CheckConfiguration()
    {
        try
        {
            // Validate essential configuration sections
            var shellConfig = _configuration.GetSection("Shell");
            if (!shellConfig.Exists())
            {
                _logger.LogWarning("Shell configuration section is missing");
                return false;
            }

            // Check required configuration values
            var version = _configuration["Shell:Version"];
            if (string.IsNullOrEmpty(version))
            {
                _logger.LogWarning("Shell version configuration is missing");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Configuration validation failed");
            return false;
        }
    }

    private Dictionary<string, object> CheckCoreServices()
    {
        var serviceStatus = new Dictionary<string, object>();

        try
        {
            // Check if core services are available
            serviceStatus["configuration"] = _serviceProvider.GetService<IConfiguration>() != null;
            serviceStatus["logger"] = _serviceProvider.GetService<ILoggerFactory>() != null;
            serviceStatus["httpContextAccessor"] = _serviceProvider.GetService<IHttpContextAccessor>() != null;
            serviceStatus["memoryCache"] = _serviceProvider.GetService<IMemoryCache>() != null;

            // Check authentication service if enabled
            var authEnabled = _configuration.GetValue<bool>("Shell:Services:Authentication:Enabled", false);
            if (authEnabled)
            {
                serviceStatus["authentication"] = _serviceProvider.GetService<Microsoft.AspNetCore.Authentication.IAuthenticationService>() != null;
            }

            // Check authorization service if enabled
            var authzEnabled = _configuration.GetValue<bool>("Shell:Services:Authorization:Enabled", false);
            if (authzEnabled)
            {
                serviceStatus["authorization"] = _serviceProvider.GetService<IAuthorizationService>() != null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Core services check failed");
            serviceStatus["error"] = ex.Message;
        }

        return serviceStatus;
    }

    private async Task<Dictionary<string, object>> CheckModulesAsync(CancellationToken cancellationToken)
    {
        var moduleStatus = new Dictionary<string, object>();

        try
        {
            // This will be implemented when the module system is complete
            // For now, just check if module auto-loading is enabled
            var autoLoad = _configuration.GetValue<bool>("Shell:Modules:AutoLoad", true);
            var modulesPath = _configuration["Shell:Modules:Source"] ?? "./modules";

            moduleStatus["autoLoad"] = autoLoad;
            moduleStatus["modulesPath"] = modulesPath;
            moduleStatus["modulesDirectoryExists"] = Directory.Exists(modulesPath);

            if (Directory.Exists(modulesPath))
            {
                var moduleFiles = Directory.GetFiles(modulesPath, "*.dll", SearchOption.TopDirectoryOnly);
                moduleStatus["moduleFilesCount"] = moduleFiles.Length;
            }

            // Placeholder for actual module health checks
            moduleStatus["loadedModules"] = 0;
            moduleStatus["healthyModules"] = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Module status check failed");
            moduleStatus["error"] = ex.Message;
        }

        return await Task.FromResult(moduleStatus);
    }

    private async Task<Dictionary<string, object>> CheckExternalDependenciesAsync(CancellationToken cancellationToken)
    {
        var dependencyStatus = new Dictionary<string, object>();

        try
        {
            // Check database connection if configured
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrEmpty(connectionString))
            {
                dependencyStatus["database"] = await CheckDatabaseConnectionAsync(connectionString, cancellationToken);
            }

            // Check Redis connection if configured
            var redisConnectionString = _configuration.GetConnectionString("Redis");
            if (!string.IsNullOrEmpty(redisConnectionString))
            {
                dependencyStatus["redis"] = await CheckRedisConnectionAsync(redisConnectionString, cancellationToken);
            }

            // Check external APIs if configured
            var externalApiUrls = _configuration.GetSection("ExternalApis:HealthCheckUrls").Get<string[]>();
            if (externalApiUrls?.Any() == true)
            {
                dependencyStatus["externalApis"] = await CheckExternalApisAsync(externalApiUrls, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "External dependencies check failed");
            dependencyStatus["error"] = ex.Message;
        }

        return dependencyStatus;
    }

    private async Task<bool> CheckDatabaseConnectionAsync(string connectionString, CancellationToken cancellationToken)
    {
        try
        {
            // This would be implemented with actual database connectivity check
            // For now, just validate the connection string format
            return !string.IsNullOrWhiteSpace(connectionString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database connection check failed");
            return false;
        }
    }

    private async Task<bool> CheckRedisConnectionAsync(string connectionString, CancellationToken cancellationToken)
    {
        try
        {
            // This would be implemented with actual Redis connectivity check
            // For now, just validate the connection string format
            return !string.IsNullOrWhiteSpace(connectionString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis connection check failed");
            return false;
        }
    }

    private async Task<Dictionary<string, bool>> CheckExternalApisAsync(string[] apiUrls, CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, bool>();
        var httpClient = _serviceProvider.GetService<IHttpClientFactory>()?.CreateClient();

        if (httpClient == null)
        {
            _logger.LogWarning("HTTP client not available for external API checks");
            return results;
        }

        foreach (var url in apiUrls)
        {
            try
            {
                using var response = await httpClient.GetAsync(url, cancellationToken);
                results[url] = response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check external API: {Url}", url);
                results[url] = false;
            }
        }

        return results;
    }
}