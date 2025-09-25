using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetShell.Host.HealthChecks;

/// <summary>
/// Startup health check that indicates the application has completed its startup sequence.
/// This is used for container orchestration to know when the application is fully initialized.
/// </summary>
public class StartupHealthCheck : IHealthCheck
{
    private readonly ILogger<StartupHealthCheck> _logger;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private static volatile bool _startupCompleted = false;
    private static readonly object _lock = new object();

    /// <summary>
    /// Initializes a new instance of the <see cref="StartupHealthCheck"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="environment">The web host environment.</param>
    public StartupHealthCheck(
        ILogger<StartupHealthCheck> logger,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    /// <summary>
    /// Marks the startup as completed. This should be called when the application has fully initialized.
    /// </summary>
    public static void MarkStartupComplete()
    {
        lock (_lock)
        {
            _startupCompleted = true;
        }
    }

    /// <summary>
    /// Resets the startup status. This can be used for testing or during application restart scenarios.
    /// </summary>
    public static void ResetStartupStatus()
    {
        lock (_lock)
        {
            _startupCompleted = false;
        }
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
                ["environment"] = _environment.EnvironmentName,
                ["applicationName"] = _environment.ApplicationName
            };

            // Check if startup is completed
            var startupCompleted = await CheckStartupCompletionAsync(cancellationToken);
            data["startupCompleted"] = startupCompleted;

            if (!startupCompleted)
            {
                // Perform additional startup validation checks
                var validationResults = await PerformStartupValidationAsync(cancellationToken);
                data["validationResults"] = validationResults;

                // If all validations pass, mark startup as complete
                if (validationResults.All(kvp => (bool)kvp.Value))
                {
                    MarkStartupComplete();
                    startupCompleted = true;
                    data["startupCompleted"] = true;
                    data["completedAt"] = DateTime.UtcNow;
                }
            }

            var message = startupCompleted
                ? "Application startup completed successfully"
                : "Application is still starting up";

            _logger.LogDebug("Startup health check completed with status: {Status}", startupCompleted ? "Healthy" : "Unhealthy");

            return startupCompleted
                ? HealthCheckResult.Healthy(message, data)
                : HealthCheckResult.Unhealthy(message, data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Startup health check failed");
            return HealthCheckResult.Unhealthy("Startup check failed", ex);
        }
    }

    private Task<bool> CheckStartupCompletionAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return Task.FromResult(_startupCompleted);
        }
    }

    private async Task<Dictionary<string, object>> PerformStartupValidationAsync(CancellationToken cancellationToken)
    {
        var validationResults = new Dictionary<string, object>();

        try
        {
            // Validate configuration is loaded
            validationResults["configurationLoaded"] = ValidateConfiguration();

            // Validate required services are registered
            validationResults["servicesRegistered"] = ValidateServices();

            // Validate application environment
            validationResults["environmentValid"] = ValidateEnvironment();

            // Validate modules directory (if applicable)
            validationResults["modulesDirectoryReady"] = ValidateModulesDirectory();

            // Validate external dependencies (basic checks)
            validationResults["externalDependenciesReady"] = await ValidateExternalDependenciesAsync(cancellationToken);

            // Validate security configuration
            validationResults["securityConfigurationValid"] = ValidateSecurityConfiguration();

            // Add startup timing information
            var process = System.Diagnostics.Process.GetCurrentProcess();
            validationResults["processStartTime"] = process.StartTime;
            validationResults["uptime"] = DateTime.UtcNow - process.StartTime;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Startup validation failed");
            validationResults["error"] = ex.Message;
        }

        return validationResults;
    }

    private bool ValidateConfiguration()
    {
        try
        {
            // Check essential configuration sections
            var shellSection = _configuration.GetSection("Shell");
            if (!shellSection.Exists())
            {
                _logger.LogError("Shell configuration section is missing");
                return false;
            }

            var version = _configuration["Shell:Version"];
            if (string.IsNullOrEmpty(version))
            {
                _logger.LogError("Shell version configuration is missing");
                return false;
            }

            var name = _configuration["Shell:Name"];
            if (string.IsNullOrEmpty(name))
            {
                _logger.LogWarning("Shell name configuration is missing, using default");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Configuration validation failed during startup check");
            return false;
        }
    }

    private bool ValidateServices()
    {
        try
        {
            // This would validate that all required services are properly registered
            // For now, just return true as services are validated elsewhere
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service validation failed during startup check");
            return false;
        }
    }

    private bool ValidateEnvironment()
    {
        try
        {
            // Validate environment name is set
            if (string.IsNullOrEmpty(_environment.EnvironmentName))
            {
                _logger.LogError("Environment name is not set");
                return false;
            }

            // Validate content root exists
            if (!Directory.Exists(_environment.ContentRootPath))
            {
                _logger.LogError("Content root path does not exist: {Path}", _environment.ContentRootPath);
                return false;
            }

            // Validate web root exists (if it should)
            if (!string.IsNullOrEmpty(_environment.WebRootPath) && !Directory.Exists(_environment.WebRootPath))
            {
                _logger.LogWarning("Web root path does not exist: {Path}", _environment.WebRootPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Environment validation failed during startup check");
            return false;
        }
    }

    private bool ValidateModulesDirectory()
    {
        try
        {
            var autoLoad = _configuration.GetValue<bool>("Shell:Modules:AutoLoad", true);
            if (!autoLoad)
            {
                // If auto-load is disabled, modules directory validation is not critical
                return true;
            }

            var modulesPath = _configuration["Shell:Modules:Source"] ?? "./modules";
            var fullModulesPath = Path.GetFullPath(modulesPath, _environment.ContentRootPath);

            if (!Directory.Exists(fullModulesPath))
            {
                _logger.LogWarning("Modules directory does not exist: {Path}", fullModulesPath);

                // Try to create the directory
                try
                {
                    Directory.CreateDirectory(fullModulesPath);
                    _logger.LogInformation("Created modules directory: {Path}", fullModulesPath);
                }
                catch (Exception createEx)
                {
                    _logger.LogError(createEx, "Failed to create modules directory: {Path}", fullModulesPath);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Modules directory validation failed during startup check");
            return false;
        }
    }

    private async Task<bool> ValidateExternalDependenciesAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Basic validation of external dependencies configuration
            var hasDatabase = !string.IsNullOrEmpty(_configuration.GetConnectionString("DefaultConnection"));
            var hasRedis = !string.IsNullOrEmpty(_configuration.GetConnectionString("Redis"));

            // If no external dependencies are configured, validation passes
            if (!hasDatabase && !hasRedis)
            {
                return true;
            }

            // For startup check, we just validate configuration format rather than connectivity
            // Actual connectivity is checked in ReadinessHealthCheck
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "External dependencies validation failed during startup check");
            return false;
        }
    }

    private bool ValidateSecurityConfiguration()
    {
        try
        {
            var authEnabled = _configuration.GetValue<bool>("Shell:Services:Authentication:Enabled", false);
            if (authEnabled)
            {
                // Validate JWT configuration if authentication is enabled
                var jwtSection = _configuration.GetSection("Shell:Services:Authentication:JWT");
                if (!jwtSection.Exists())
                {
                    _logger.LogError("JWT configuration section is missing while authentication is enabled");
                    return false;
                }

                var issuer = jwtSection["Issuer"];
                var audience = jwtSection["Audience"];

                if (string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(audience))
                {
                    _logger.LogError("JWT issuer and audience must be configured when authentication is enabled");
                    return false;
                }

                // In production, we should validate that SecretKey is configured
                if (!_environment.IsDevelopment())
                {
                    var secretKey = jwtSection["SecretKey"];
                    if (string.IsNullOrEmpty(secretKey))
                    {
                        _logger.LogError("JWT SecretKey must be configured in production");
                        return false;
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Security configuration validation failed during startup check");
            return false;
        }
    }
}