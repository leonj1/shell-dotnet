using Microsoft.OpenApi.Models;

namespace DotNetShell.Host;

/// <summary>
/// Builder class for configuring the Shell Host application.
/// </summary>
public class ShellHostBuilder
{
    private readonly WebApplicationBuilder _builder;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellHostBuilder"/> class.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    public ShellHostBuilder(WebApplicationBuilder builder)
    {
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
    }

    /// <summary>
    /// Configures the configuration system.
    /// </summary>
    /// <returns>The shell host builder for method chaining.</returns>
    public ShellHostBuilder ConfigureConfiguration()
    {
        // Add additional configuration sources
        _builder.Configuration
            .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables("SHELL_");

        return this;
    }

    /// <summary>
    /// Configures logging services.
    /// </summary>
    /// <returns>The shell host builder for method chaining.</returns>
    public ShellHostBuilder ConfigureLogging()
    {
        // Clear default logging providers and add Serilog
        _builder.Logging.ClearProviders();

        // Serilog configuration will be added here
        // For now, use the default configuration

        return this;
    }

    /// <summary>
    /// Configures core services.
    /// </summary>
    /// <returns>The shell host builder for method chaining.</returns>
    public ShellHostBuilder ConfigureServices()
    {
        // Add controllers
        _builder.Services.AddControllers();

        // Add API explorer for Swagger
        _builder.Services.AddEndpointsApiExplorer();

        // Add memory cache
        _builder.Services.AddMemoryCache();

        // Add HTTP client
        _builder.Services.AddHttpClient();

        return this;
    }

    /// <summary>
    /// Configures telemetry services.
    /// </summary>
    /// <returns>The shell host builder for method chaining.</returns>
    public ShellHostBuilder ConfigureTelemetry()
    {
        // OpenTelemetry configuration will be added here
        return this;
    }

    /// <summary>
    /// Configures authentication services.
    /// </summary>
    /// <returns>The shell host builder for method chaining.</returns>
    public ShellHostBuilder ConfigureAuthentication()
    {
        // JWT authentication configuration will be added here
        return this;
    }

    /// <summary>
    /// Configures authorization services.
    /// </summary>
    /// <returns>The shell host builder for method chaining.</returns>
    public ShellHostBuilder ConfigureAuthorization()
    {
        // Authorization policies will be added here
        return this;
    }

    /// <summary>
    /// Configures health check services.
    /// </summary>
    /// <returns>The shell host builder for method chaining.</returns>
    public ShellHostBuilder ConfigureHealthChecks()
    {
        _builder.Services.AddHealthChecks()
            .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Application is running"));

        return this;
    }

    /// <summary>
    /// Configures Swagger/OpenAPI services.
    /// </summary>
    /// <returns>The shell host builder for method chaining.</returns>
    public ShellHostBuilder ConfigureSwagger()
    {
        var swaggerEnabled = _builder.Configuration.GetValue<bool>("Shell:Swagger:Enabled", true);
        if (!swaggerEnabled)
            return this;

        _builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = _builder.Configuration["Shell:Swagger:Title"] ?? "DotNet Shell API",
                Version = _builder.Configuration["Shell:Swagger:Version"] ?? "v1",
                Description = _builder.Configuration["Shell:Swagger:Description"] ?? "Enterprise-grade modular hosting framework for .NET applications"
            });

            // Add security definition for JWT
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return this;
    }

    /// <summary>
    /// Builds the shell host.
    /// </summary>
    /// <returns>The configured shell host.</returns>
    public ShellHost Build()
    {
        return new ShellHost(_builder);
    }
}