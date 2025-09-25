using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace DotNetShell.Host.Controllers;

/// <summary>
/// Shell management API controller that demonstrates the core host functionality.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Produces("application/json")]
public class ShellController : ControllerBase
{
    private readonly ILogger<ShellController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellController"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="environment">The web host environment.</param>
    public ShellController(
        ILogger<ShellController> logger,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    /// <summary>
    /// Gets basic information about the shell host.
    /// </summary>
    /// <returns>Shell host information.</returns>
    [HttpGet("info")]
    [ProducesResponseType(typeof(ShellInfo), 200)]
    [ProducesResponseType(typeof(ValidationErrorResponse), 400)]
    [ProducesResponseType(500)]
    public ActionResult<ShellInfo> GetInfo()
    {
        _logger.LogInformation("Shell info requested");

        var info = new ShellInfo
        {
            Name = _configuration["Shell:Name"] ?? "DotNet Shell",
            Version = _configuration["Shell:Version"] ?? "1.0.0",
            Environment = _environment.EnvironmentName,
            Status = "Running",
            Uptime = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime,
            Features = new ShellFeatures
            {
                AuthenticationEnabled = _configuration.GetValue<bool>("Shell:Services:Authentication:Enabled", false),
                AuthorizationEnabled = _configuration.GetValue<bool>("Shell:Services:Authorization:Enabled", false),
                TelemetryEnabled = _configuration.GetValue<bool>("Shell:Services:Telemetry:Enabled", false),
                SwaggerEnabled = _configuration.GetValue<bool>("Shell:Swagger:Enabled", true),
                HealthChecksEnabled = _configuration.GetValue<bool>("Shell:Services:HealthChecks:Enabled", true),
                ModuleAutoLoadEnabled = _configuration.GetValue<bool>("Shell:Modules:AutoLoad", true)
            }
        };

        return Ok(info);
    }

    /// <summary>
    /// Gets the current configuration (sanitized).
    /// </summary>
    /// <returns>Shell configuration information.</returns>
    [HttpGet("config")]
    [ProducesResponseType(typeof(Dictionary<string, object>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(500)]
    public ActionResult<Dictionary<string, object>> GetConfig()
    {
        _logger.LogInformation("Shell configuration requested");

        var config = new Dictionary<string, object>
        {
            ["shell"] = new
            {
                name = _configuration["Shell:Name"],
                version = _configuration["Shell:Version"],
                environment = _environment.EnvironmentName
            },
            ["features"] = new
            {
                authentication = _configuration.GetValue<bool>("Shell:Services:Authentication:Enabled"),
                authorization = _configuration.GetValue<bool>("Shell:Services:Authorization:Enabled"),
                telemetry = _configuration.GetValue<bool>("Shell:Services:Telemetry:Enabled"),
                healthChecks = _configuration.GetValue<bool>("Shell:Services:HealthChecks:Enabled")
            },
            ["modules"] = new
            {
                autoLoad = _configuration.GetValue<bool>("Shell:Modules:AutoLoad"),
                source = _configuration["Shell:Modules:Source"]
            }
        };

        return Ok(config);
    }

    /// <summary>
    /// Gets information about loaded modules.
    /// </summary>
    /// <returns>Module information.</returns>
    [HttpGet("modules")]
    [ProducesResponseType(typeof(ModulesInfo), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(500)]
    public ActionResult<ModulesInfo> GetModules()
    {
        _logger.LogInformation("Module information requested");

        var modulesPath = _configuration["Shell:Modules:Source"] ?? "./modules";
        var autoLoad = _configuration.GetValue<bool>("Shell:Modules:AutoLoad", true);

        var modulesInfo = new ModulesInfo
        {
            AutoLoad = autoLoad,
            ModulesPath = modulesPath,
            ModulesDirectoryExists = Directory.Exists(modulesPath),
            LoadedModules = new List<ModuleInfo>() // This will be populated when module system is implemented
        };

        if (Directory.Exists(modulesPath))
        {
            var moduleFiles = Directory.GetFiles(modulesPath, "*.dll", SearchOption.TopDirectoryOnly);
            modulesInfo.AvailableModuleFiles = moduleFiles.Select(f => Path.GetFileName(f)).ToList();
        }

        return Ok(modulesInfo);
    }

    /// <summary>
    /// Validates a request payload (demonstration endpoint).
    /// </summary>
    /// <param name="request">The request to validate.</param>
    /// <returns>Validation result.</returns>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(ValidationResult), 200)]
    [ProducesResponseType(typeof(ValidationErrorResponse), 400)]
    [ProducesResponseType(500)]
    public ActionResult<ValidationResult> ValidateRequest([FromBody] SampleRequest request)
    {
        _logger.LogInformation("Request validation requested for: {@Request}", request);

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = new ValidationResult
        {
            IsValid = true,
            Message = "Request is valid",
            ValidatedAt = DateTime.UtcNow,
            Request = request
        };

        return Ok(result);
    }

    /// <summary>
    /// Protected endpoint that requires authentication (demonstration).
    /// </summary>
    /// <returns>Protected resource information.</returns>
    [HttpGet("protected")]
    [Authorize]
    [ProducesResponseType(typeof(ProtectedResource), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(500)]
    public ActionResult<ProtectedResource> GetProtectedResource()
    {
        var userId = User?.Identity?.Name ?? "unknown";
        _logger.LogInformation("Protected resource requested by user: {UserId}", userId);

        var resource = new ProtectedResource
        {
            Message = "You have successfully accessed a protected resource!",
            UserId = userId,
            AccessTime = DateTime.UtcNow,
            Claims = User?.Claims?.Select(c => new { c.Type, c.Value }).ToList() ?? new List<object>()
        };

        return Ok(resource);
    }
}

/// <summary>
/// Represents shell host information.
/// </summary>
public class ShellInfo
{
    /// <summary>
    /// Gets or sets the shell name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the shell version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the environment name.
    /// </summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the uptime.
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Gets or sets the enabled features.
    /// </summary>
    public ShellFeatures Features { get; set; } = new();
}

/// <summary>
/// Represents enabled shell features.
/// </summary>
public class ShellFeatures
{
    /// <summary>
    /// Gets or sets a value indicating whether authentication is enabled.
    /// </summary>
    public bool AuthenticationEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether authorization is enabled.
    /// </summary>
    public bool AuthorizationEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether telemetry is enabled.
    /// </summary>
    public bool TelemetryEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Swagger is enabled.
    /// </summary>
    public bool SwaggerEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether health checks are enabled.
    /// </summary>
    public bool HealthChecksEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether module auto-loading is enabled.
    /// </summary>
    public bool ModuleAutoLoadEnabled { get; set; }
}

/// <summary>
/// Represents information about modules.
/// </summary>
public class ModulesInfo
{
    /// <summary>
    /// Gets or sets a value indicating whether auto-loading is enabled.
    /// </summary>
    public bool AutoLoad { get; set; }

    /// <summary>
    /// Gets or sets the modules path.
    /// </summary>
    public string ModulesPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the modules directory exists.
    /// </summary>
    public bool ModulesDirectoryExists { get; set; }

    /// <summary>
    /// Gets or sets the list of available module files.
    /// </summary>
    public List<string> AvailableModuleFiles { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of loaded modules.
    /// </summary>
    public List<ModuleInfo> LoadedModules { get; set; } = new();
}

/// <summary>
/// Represents information about a loaded module.
/// </summary>
public class ModuleInfo
{
    /// <summary>
    /// Gets or sets the module name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the module version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the module status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the module was loaded.
    /// </summary>
    public DateTime LoadedAt { get; set; }
}

/// <summary>
/// Represents a sample request for validation demonstration.
/// </summary>
public class SampleRequest
{
    /// <summary>
    /// Gets or sets the name (required).
    /// </summary>
    [Required(ErrorMessage = "Name is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email (required, valid email format).
    /// </summary>
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the age (optional, must be positive).
    /// </summary>
    [Range(0, 150, ErrorMessage = "Age must be between 0 and 150")]
    public int? Age { get; set; }
}

/// <summary>
/// Represents a validation result.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the request is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the validation message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the validation was performed.
    /// </summary>
    public DateTime ValidatedAt { get; set; }

    /// <summary>
    /// Gets or sets the validated request.
    /// </summary>
    public object? Request { get; set; }
}

/// <summary>
/// Represents a protected resource.
/// </summary>
public class ProtectedResource
{
    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user ID.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the access time.
    /// </summary>
    public DateTime AccessTime { get; set; }

    /// <summary>
    /// Gets or sets the user claims.
    /// </summary>
    public List<object> Claims { get; set; } = new();
}

/// <summary>
/// Represents a validation error response with detailed field-level errors.
/// </summary>
public class ValidationErrorResponse
{
    /// <summary>
    /// Gets or sets the HTTP status code.
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Gets or sets the error type.
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the trace identifier.
    /// </summary>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the error occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the field-level validation errors.
    /// </summary>
    public Dictionary<string, List<string>> Errors { get; set; } = new();
}