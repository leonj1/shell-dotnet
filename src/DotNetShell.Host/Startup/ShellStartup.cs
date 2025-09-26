using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Text;
using System.Text.Json;
using DotNetShell.Host.HealthChecks;
using DotNetShell.Host.Middleware;
using DotNetShell.Host.Filters;
using DotNetShell.Host.Swagger;
using DotNetShell.Core.Configuration;
using DotNetShell.Abstractions.Services;

namespace DotNetShell.Host.Startup;

/// <summary>
/// Configures services and the application request pipeline for the Shell Host.
/// </summary>
public class ShellStartup
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellStartup"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="environment">The web host environment.</param>
    public ShellStartup(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    /// <summary>
    /// Configures services for the application.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public void ConfigureServices(IServiceCollection services)
    {
        // Add configuration services
        ConfigureConfigurationServices(services);

        // Add configuration sections
        ConfigureConfiguration(services);

        // Add core services
        ConfigureCoreServices(services);

        // Add authentication services
        ConfigureAuthentication(services);

        // Add authorization services
        ConfigureAuthorization(services);

        // Add health checks
        ConfigureHealthChecks(services);

        // Add API versioning
        ConfigureApiVersioning(services);

        // Add Swagger/OpenAPI
        ConfigureSwagger(services);

        // Add CORS
        ConfigureCors(services);

        // Add response compression
        ConfigureResponseCompression(services);

        // Add telemetry services
        ConfigureTelemetry(services);
    }

    /// <summary>
    /// Configures the HTTP request pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    public void Configure(IApplicationBuilder app)
    {
        // Configure forwarded headers for reverse proxy scenarios
        if (!_environment.IsDevelopment())
        {
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });
        }

        // Add global exception handling
        app.UseMiddleware<GlobalExceptionMiddleware>();

        // Add request logging
        app.UseMiddleware<RequestLoggingMiddleware>();

        // Security headers
        app.UseMiddleware<SecurityHeadersMiddleware>();

        // Configure for development
        if (_environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        // Add Swagger in appropriate environments
        var swaggerEnabled = _configuration.GetValue<bool>("Shell:Swagger:Enabled", true);
        if (swaggerEnabled && (_environment.IsDevelopment() || _environment.IsStaging()))
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                var apiVersionDescriptionProvider = app.ApplicationServices.GetRequiredService<IApiVersionDescriptionProvider>();

                foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions.Reverse())
                {
                    c.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json",
                        $"DotNet Shell API {description.GroupName.ToUpperInvariant()}");
                }

                c.RoutePrefix = "swagger";
                c.DocumentTitle = "DotNet Shell API Documentation";
                c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
            });
        }

        // Use HTTPS redirection
        app.UseHttpsRedirection();

        // Use response compression
        app.UseResponseCompression();

        // Use routing
        app.UseRouting();

        // Use CORS
        app.UseCors();

        // Use authentication
        app.UseAuthentication();

        // Use authorization
        app.UseAuthorization();

        // Use guest endpoint authentication enforcement
        app.UseGuestEndpointAuthentication();

        // Configure endpoints
        app.UseEndpoints(endpoints =>
        {
            // Map controllers
            endpoints.MapControllers();

            // Map health check endpoints
            MapHealthCheckEndpoints(endpoints);

            // Map default endpoint
            endpoints.MapGet("/", async context =>
            {
                var response = new
                {
                    Name = _configuration["Shell:Name"] ?? "DotNet Shell",
                    Version = _configuration["Shell:Version"] ?? "1.0.0",
                    Environment = _environment.EnvironmentName,
                    Status = "Running",
                    Timestamp = DateTime.UtcNow,
                    Documentation = swaggerEnabled && (_environment.IsDevelopment() || _environment.IsStaging())
                        ? $"{context.Request.Scheme}://{context.Request.Host}/swagger"
                        : null
                };

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                }));
            });

            // Map fallback
            endpoints.MapFallback(async context =>
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("Endpoint not found");
            });
        });
    }

    private void ConfigureConfigurationServices(IServiceCollection services)
    {
        // Register configuration services
        services.AddSingleton<ISecretResolver, SecretResolver>();
        services.AddSingleton<IConfigurationValidator, ConfigurationValidator>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();

        // Add configuration hot-reload
        services.AddConfigurationHotReload(autoStart: true);
    }

    private void ConfigureConfiguration(IServiceCollection services)
    {
        // Configure strongly typed configuration sections using the new options classes
        services.Configure<ShellOptions>(_configuration.GetSection(ShellOptions.SectionName));
        services.Configure<ModulesOptions>(_configuration.GetSection("Shell:Modules"));
        services.Configure<ServicesOptions>(_configuration.GetSection("Shell:Services"));
        services.Configure<AuthenticationOptions>(_configuration.GetSection("Shell:Services:Authentication"));
        services.Configure<JwtOptions>(_configuration.GetSection("Shell:Services:Authentication:JWT"));
        services.Configure<AuthorizationOptions>(_configuration.GetSection("Shell:Services:Authorization"));
        services.Configure<LoggingOptions>(_configuration.GetSection("Shell:Services:Logging"));
        services.Configure<TelemetryOptions>(_configuration.GetSection("Shell:Services:Telemetry"));
        services.Configure<HealthChecksOptions>(_configuration.GetSection("Shell:Services:HealthChecks"));
        services.Configure<KestrelOptions>(_configuration.GetSection("Shell:Kestrel"));
        services.Configure<SwaggerOptions>(_configuration.GetSection("Shell:Swagger"));

        // Validate configuration on startup
        var validator = services.BuildServiceProvider().GetRequiredService<IConfigurationValidator>();
        var shellSection = _configuration.GetSection(ShellOptions.SectionName);
        var validationResult = validator.Validate(shellSection, typeof(ShellOptions));

        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.Message));
            throw new InvalidOperationException($"Configuration validation failed: {errors}");
        }
    }

    private void ConfigureCoreServices(IServiceCollection services)
    {
        // Add controllers with custom options
        services.AddControllers(options =>
        {
            // Add custom filters
            options.Filters.Add<Filters.ValidationActionFilter>();
        })
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.WriteIndented = _environment.IsDevelopment();
            options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        });

        // Add API explorer
        services.AddEndpointsApiExplorer();

        // Add memory cache
        services.AddMemoryCache();

        // Add HTTP client factory
        services.AddHttpClient();

        // Add data protection
        services.AddDataProtection();

        // Add options
        services.AddOptions();
    }

    private void ConfigureAuthentication(IServiceCollection services)
    {
        var authEnabled = _configuration.GetValue<bool>("Shell:Services:Authentication:Enabled", false);
        if (!authEnabled) return;

        var jwtSettings = _configuration.GetSection("Shell:Services:Authentication:JWT");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is required");
            var key = Encoding.ASCII.GetBytes(secretKey);

            options.RequireHttpsMetadata = jwtSettings.GetValue<bool>("RequireHttpsMetadata", !_environment.IsDevelopment());
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = jwtSettings.GetValue<bool>("ValidateIssuerSigningKey", true),
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = jwtSettings.GetValue<bool>("ValidateIssuer", true),
                ValidIssuer = jwtSettings["Issuer"],
                ValidateAudience = jwtSettings.GetValue<bool>("ValidateAudience", true),
                ValidAudience = jwtSettings["Audience"],
                RequireExpirationTime = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Parse(jwtSettings["ClockSkew"] ?? "00:05:00")
            };
        });
    }

    private void ConfigureAuthorization(IServiceCollection services)
    {
        var authzEnabled = _configuration.GetValue<bool>("Shell:Services:Authorization:Enabled", false);
        if (!authzEnabled) return;

        services.AddAuthorization(options =>
        {
            // Add default policies
            options.AddPolicy("RequireAuthentication", policy =>
                policy.RequireAuthenticatedUser());

            options.AddPolicy("RequireAdminRole", policy =>
                policy.RequireRole("Admin"));

            options.AddPolicy("RequireUserRole", policy =>
                policy.RequireRole("User", "Admin"));

            // Add custom policies
            options.AddPolicy("RequireModuleAccess", policy =>
                policy.RequireClaim("module_access"));
        });
    }

    private void ConfigureHealthChecks(IServiceCollection services)
    {
        var healthCheckBuilder = services.AddHealthChecks()
            .AddCheck<LivenessHealthCheck>("liveness", HealthStatus.Unhealthy, new[] { "live" })
            .AddCheck<ReadinessHealthCheck>("readiness", HealthStatus.Unhealthy, new[] { "ready" })
            .AddCheck<StartupHealthCheck>("startup", HealthStatus.Unhealthy, new[] { "startup" });

        // Add additional health checks based on configuration
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(connectionString))
        {
            // Add database health check when available
            // healthCheckBuilder.AddSqlServer(connectionString, name: "database");
        }
    }

    private void ConfigureApiVersioning(IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ApiVersionReader = ApiVersionReader.Combine(
                new QueryStringApiVersionReader("version"),
                new HeaderApiVersionReader("X-Version"),
                new UrlSegmentApiVersionReader());
        });

        services.AddVersionedApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });
    }

    private void ConfigureSwagger(IServiceCollection services)
    {
        var swaggerEnabled = _configuration.GetValue<bool>("Shell:Swagger:Enabled", true);
        if (!swaggerEnabled) return;

        services.AddSwaggerGen(c =>
        {
            var apiVersionDescriptionProvider = services.BuildServiceProvider().GetRequiredService<IApiVersionDescriptionProvider>();

            foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions)
            {
                c.SwaggerDoc(description.GroupName, new OpenApiInfo
                {
                    Title = _configuration["Shell:Swagger:Title"] ?? "DotNet Shell API",
                    Version = description.ApiVersion.ToString(),
                    Description = _configuration["Shell:Swagger:Description"] ?? "Enterprise-grade modular hosting framework for .NET applications",
                    Contact = new OpenApiContact
                    {
                        Name = _configuration["Shell:Swagger:Contact:Name"] ?? "Development Team",
                        Email = _configuration["Shell:Swagger:Contact:Email"],
                        Url = !string.IsNullOrEmpty(_configuration["Shell:Swagger:Contact:Url"])
                            ? new Uri(_configuration["Shell:Swagger:Contact:Url"])
                            : null
                    }
                });
            }

            // Add JWT authentication to Swagger
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

            // Include XML comments
            var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
            if (File.Exists(xmlPath))
            {
                c.IncludeXmlComments(xmlPath);
            }

            // Add custom operation filter
            c.OperationFilter<SwaggerOperationFilter>();

            // Add custom schema filter
            c.SchemaFilter<SwaggerSchemaFilter>();
        });
    }

    private void ConfigureCors(IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                if (_environment.IsDevelopment())
                {
                    builder.AllowAnyOrigin()
                           .AllowAnyMethod()
                           .AllowAnyHeader();
                }
                else
                {
                    var allowedOrigins = _configuration.GetSection("Shell:Cors:AllowedOrigins")
                        .Get<string[]>() ?? Array.Empty<string>();

                    if (allowedOrigins.Any())
                    {
                        builder.WithOrigins(allowedOrigins)
                               .AllowAnyMethod()
                               .AllowAnyHeader()
                               .AllowCredentials();
                    }
                }
            });
        });
    }

    private void ConfigureResponseCompression(IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
            options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
        });
    }

    private void ConfigureTelemetry(IServiceCollection services)
    {
        var telemetryEnabled = _configuration.GetValue<bool>("Shell:Services:Telemetry:Enabled", false);
        if (!telemetryEnabled) return;

        // OpenTelemetry configuration will be added here when the telemetry service is implemented
    }

    private void MapHealthCheckEndpoints(IEndpointRouteBuilder endpoints)
    {
        var healthConfig = _configuration.GetSection("Shell:Services:HealthChecks");

        endpoints.MapHealthChecks(healthConfig["Endpoints:Liveness"] ?? "/health/live", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("live"),
            ResponseWriter = HealthCheckResponseWriter.WriteResponse
        });

        endpoints.MapHealthChecks(healthConfig["Endpoints:Readiness"] ?? "/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready"),
            ResponseWriter = HealthCheckResponseWriter.WriteResponse
        });

        endpoints.MapHealthChecks(healthConfig["Endpoints:Startup"] ?? "/health/startup", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("startup"),
            ResponseWriter = HealthCheckResponseWriter.WriteResponse
        });

        // General health endpoint
        endpoints.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = HealthCheckResponseWriter.WriteResponse
        });
    }
}

