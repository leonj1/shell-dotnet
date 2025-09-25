using System.ComponentModel.DataAnnotations;

namespace DotNetShell.Core.Configuration;

/// <summary>
/// Base interface for validatable configuration options.
/// </summary>
public interface IValidatable
{
    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    /// <param name="validationResults">Collection to store validation results.</param>
    /// <returns>True if validation passes; otherwise, false.</returns>
    bool TryValidate(out ICollection<ValidationResult> validationResults);
}

/// <summary>
/// Base class for strongly-typed configuration options with validation support.
/// </summary>
public abstract class ValidatableOptions : IValidatable
{
    /// <inheritdoc />
    public virtual bool TryValidate(out ICollection<ValidationResult> validationResults)
    {
        validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(this);

        return Validator.TryValidateObject(this, validationContext, validationResults, true);
    }

    /// <summary>
    /// Validates the configuration and throws an exception if validation fails.
    /// </summary>
    public virtual void Validate()
    {
        if (!TryValidate(out var validationResults))
        {
            var errors = string.Join(", ", validationResults.Select(r => r.ErrorMessage));
            throw new InvalidOperationException($"Configuration validation failed: {errors}");
        }
    }
}

/// <summary>
/// Main shell configuration options.
/// </summary>
[ConfigurationValidation(Description = "Main shell configuration settings", Category = "Shell")]
public class ShellOptions : ValidatableOptions
{
    public const string SectionName = "Shell";

    /// <summary>
    /// Gets or sets the shell version.
    /// </summary>
    [Required]
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the shell name.
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = "DotNetShell";

    /// <summary>
    /// Gets or sets the environment name.
    /// </summary>
    [Required]
    public string Environment { get; set; } = "Development";

    /// <summary>
    /// Gets or sets the module configuration.
    /// </summary>
    [Required]
    public ModulesOptions Modules { get; set; } = new();

    /// <summary>
    /// Gets or sets the services configuration.
    /// </summary>
    [Required]
    public ServicesOptions Services { get; set; } = new();

    /// <summary>
    /// Gets or sets the Kestrel configuration.
    /// </summary>
    public KestrelOptions Kestrel { get; set; } = new();

    /// <summary>
    /// Gets or sets the Swagger configuration.
    /// </summary>
    public SwaggerOptions Swagger { get; set; } = new();

    public override bool TryValidate(out ICollection<ValidationResult> validationResults)
    {
        var isValid = base.TryValidate(out validationResults);

        // Validate nested objects
        if (!Modules.TryValidate(out var moduleResults))
        {
            foreach (var result in moduleResults)
            {
                validationResults.Add(result);
            }
            isValid = false;
        }

        if (!Services.TryValidate(out var serviceResults))
        {
            foreach (var result in serviceResults)
            {
                validationResults.Add(result);
            }
            isValid = false;
        }

        return isValid;
    }
}

/// <summary>
/// Module configuration options.
/// </summary>
[ConfigurationValidation(Description = "Module loading and management settings", Category = "Modules")]
public class ModulesOptions : ValidatableOptions
{
    /// <summary>
    /// Gets or sets the modules source directory.
    /// </summary>
    [Required]
    public string Source { get; set; } = "./modules";

    /// <summary>
    /// Gets or sets whether to auto-load modules on startup.
    /// </summary>
    public bool AutoLoad { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to reload modules when files change.
    /// </summary>
    public bool ReloadOnChange { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate modules on load.
    /// </summary>
    public bool ValidateOnLoad { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of concurrent modules.
    /// </summary>
    [Range(1, 1000)]
    public int MaxConcurrentModules { get; set; } = 100;

    /// <summary>
    /// Gets or sets the module load timeout in seconds.
    /// </summary>
    [Range(1, 300)]
    public int LoadTimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Services configuration options.
/// </summary>
[ConfigurationValidation(Description = "Infrastructure services configuration", Category = "Services")]
public class ServicesOptions : ValidatableOptions
{
    /// <summary>
    /// Gets or sets the authentication configuration.
    /// </summary>
    [Required]
    public AuthenticationOptions Authentication { get; set; } = new();

    /// <summary>
    /// Gets or sets the authorization configuration.
    /// </summary>
    [Required]
    public AuthorizationOptions Authorization { get; set; } = new();

    /// <summary>
    /// Gets or sets the logging configuration.
    /// </summary>
    [Required]
    public LoggingOptions Logging { get; set; } = new();

    /// <summary>
    /// Gets or sets the telemetry configuration.
    /// </summary>
    [Required]
    public TelemetryOptions Telemetry { get; set; } = new();

    /// <summary>
    /// Gets or sets the health checks configuration.
    /// </summary>
    [Required]
    public HealthChecksOptions HealthChecks { get; set; } = new();

    public override bool TryValidate(out ICollection<ValidationResult> validationResults)
    {
        var isValid = base.TryValidate(out validationResults);

        // Validate nested service options
        var serviceOptions = new[]
        {
            Authentication, Authorization, Logging, Telemetry, HealthChecks
        };

        foreach (var option in serviceOptions)
        {
            if (!option.TryValidate(out var results))
            {
                foreach (var result in results)
                {
                    validationResults.Add(result);
                }
                isValid = false;
            }
        }

        return isValid;
    }
}

/// <summary>
/// Authentication service configuration options.
/// </summary>
[ConfigurationValidation(Description = "Authentication service settings", Category = "Authentication")]
public class AuthenticationOptions : ValidatableOptions
{
    /// <summary>
    /// Gets or sets whether authentication is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the default authentication provider.
    /// </summary>
    [Required]
    public string DefaultProvider { get; set; } = "JWT";

    /// <summary>
    /// Gets or sets the JWT authentication options.
    /// </summary>
    public JwtOptions JWT { get; set; } = new();

    /// <summary>
    /// Gets or sets OAuth 2.0 options.
    /// </summary>
    public OAuth2Options OAuth2 { get; set; } = new();

    /// <summary>
    /// Gets or sets Azure AD options.
    /// </summary>
    public AzureAdOptions AzureAD { get; set; } = new();
}

/// <summary>
/// JWT authentication options.
/// </summary>
[ConfigurationValidation(Description = "JWT authentication settings", Category = "JWT", IsSensitive = true)]
public class JwtOptions : ValidatableOptions
{
    /// <summary>
    /// Gets or sets the JWT issuer.
    /// </summary>
    [Required]
    public string Issuer { get; set; } = "DotNetShell";

    /// <summary>
    /// Gets or sets the JWT audience.
    /// </summary>
    [Required]
    public string Audience { get; set; } = "DotNetShell.API";

    /// <summary>
    /// Gets or sets the secret key (can use secret placeholder).
    /// </summary>
    [Required]
    [MinLength(32)]
    [ConfigurationValidation(IsSensitive = true)]
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the token expiration time in minutes.
    /// </summary>
    [Range(1, 525600)] // 1 minute to 1 year
    public int ExpireMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets the refresh token expiration in days.
    /// </summary>
    [Range(1, 365)]
    public int RefreshTokenExpireDays { get; set; } = 7;

    /// <summary>
    /// Gets or sets whether HTTPS metadata is required.
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate the issuer signing key.
    /// </summary>
    public bool ValidateIssuerSigningKey { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate the issuer.
    /// </summary>
    public bool ValidateIssuer { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate the audience.
    /// </summary>
    public bool ValidateAudience { get; set; } = true;

    /// <summary>
    /// Gets or sets the clock skew tolerance.
    /// </summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// OAuth 2.0 configuration options.
/// </summary>
public class OAuth2Options : ValidatableOptions
{
    public string ClientId { get; set; } = string.Empty;

    [ConfigurationValidation(IsSensitive = true)]
    public string ClientSecret { get; set; } = string.Empty;

    public string AuthorizationEndpoint { get; set; } = string.Empty;
    public string TokenEndpoint { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Azure AD configuration options.
/// </summary>
public class AzureAdOptions : ValidatableOptions
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;

    [ConfigurationValidation(IsSensitive = true)]
    public string ClientSecret { get; set; } = string.Empty;

    public string Instance { get; set; } = "https://login.microsoftonline.com/";
}

/// <summary>
/// Authorization service configuration options.
/// </summary>
public class AuthorizationOptions : ValidatableOptions
{
    public bool Enabled { get; set; } = true;
    public string DefaultPolicy { get; set; } = "RequireAuthentication";
    public bool CachePolicies { get; set; } = true;

    [Range(1, 1440)] // 1 minute to 24 hours
    public int CacheExpireMinutes { get; set; } = 30;
}

/// <summary>
/// Logging service configuration options.
/// </summary>
public class LoggingOptions : ValidatableOptions
{
    public string MinLevel { get; set; } = "Information";
    public bool IncludeScopes { get; set; } = true;
    public ConsoleLoggingOptions Console { get; set; } = new();
    public FileLoggingOptions File { get; set; } = new();
}

/// <summary>
/// Console logging options.
/// </summary>
public class ConsoleLoggingOptions : ValidatableOptions
{
    public bool Enabled { get; set; } = true;
    public bool IncludeTimestamp { get; set; } = true;
    public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";
}

/// <summary>
/// File logging options.
/// </summary>
public class FileLoggingOptions : ValidatableOptions
{
    public bool Enabled { get; set; } = true;
    public string Path { get; set; } = "./logs/shell-.log";
    public string RollingInterval { get; set; } = "Day";

    [Range(1, 365)]
    public int RetainedFileCountLimit { get; set; } = 31;

    [Range(1024, long.MaxValue)]
    public long FileSizeLimitBytes { get; set; } = 104857600; // 100MB
}

/// <summary>
/// Telemetry service configuration options.
/// </summary>
public class TelemetryOptions : ValidatableOptions
{
    public bool Enabled { get; set; } = true;
    public string ServiceName { get; set; } = "DotNetShell";
    public string ServiceVersion { get; set; } = "1.0.0";
    public ConsoleExporterOptions Console { get; set; } = new();
    public TracingOptions Tracing { get; set; } = new();
    public MetricsOptions Metrics { get; set; } = new();
}

/// <summary>
/// Console exporter options.
/// </summary>
public class ConsoleExporterOptions : ValidatableOptions
{
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Tracing configuration options.
/// </summary>
public class TracingOptions : ValidatableOptions
{
    public bool Enabled { get; set; } = true;

    [Range(0.0, 1.0)]
    public double SampleRatio { get; set; } = 1.0;
}

/// <summary>
/// Metrics configuration options.
/// </summary>
public class MetricsOptions : ValidatableOptions
{
    public bool Enabled { get; set; } = true;

    [Range(1000, 300000)] // 1 second to 5 minutes
    public int ExportIntervalMilliseconds { get; set; } = 30000;
}

/// <summary>
/// Health checks configuration options.
/// </summary>
public class HealthChecksOptions : ValidatableOptions
{
    public bool Enabled { get; set; } = true;
    public HealthCheckEndpointsOptions Endpoints { get; set; } = new();
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}

/// <summary>
/// Health check endpoints options.
/// </summary>
public class HealthCheckEndpointsOptions : ValidatableOptions
{
    public string Liveness { get; set; } = "/health/live";
    public string Readiness { get; set; } = "/health/ready";
    public string Startup { get; set; } = "/health/startup";
}

/// <summary>
/// Kestrel web server configuration options.
/// </summary>
public class KestrelOptions : ValidatableOptions
{
    public KestrelEndpointsOptions Endpoints { get; set; } = new();
    public KestrelLimitsOptions Limits { get; set; } = new();
}

/// <summary>
/// Kestrel endpoints options.
/// </summary>
public class KestrelEndpointsOptions : ValidatableOptions
{
    public EndpointOptions Http { get; set; } = new() { Url = "http://localhost:5000" };
    public EndpointOptions Https { get; set; } = new() { Url = "https://localhost:5001" };
}

/// <summary>
/// Individual endpoint options.
/// </summary>
public class EndpointOptions : ValidatableOptions
{
    [Required]
    [Url]
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Kestrel limits options.
/// </summary>
public class KestrelLimitsOptions : ValidatableOptions
{
    [Range(1, 10000)]
    public int MaxConcurrentConnections { get; set; } = 100;

    [Range(1, 10000)]
    public int MaxConcurrentUpgradedConnections { get; set; } = 100;

    [Range(1024, 1073741824)] // 1KB to 1GB
    public long MaxRequestBodySize { get; set; } = 30000000; // 30MB

    public TimeSpan RequestHeadersTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan ResponseDrainTimeout { get; set; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// Swagger/OpenAPI configuration options.
/// </summary>
public class SwaggerOptions : ValidatableOptions
{
    public bool Enabled { get; set; } = true;

    [Required]
    public string Title { get; set; } = "DotNet Shell API";

    [Required]
    public string Version { get; set; } = "v1";

    public string Description { get; set; } = "Enterprise-grade modular hosting framework for .NET applications";
    public SwaggerContactOptions Contact { get; set; } = new();
}

/// <summary>
/// Swagger contact information options.
/// </summary>
public class SwaggerContactOptions : ValidatableOptions
{
    public string Name { get; set; } = "Development Team";

    [EmailAddress]
    public string Email { get; set; } = "dev@company.com";

    [Url]
    public string Url { get; set; } = "https://github.com/company/shell-dotnet-core";
}