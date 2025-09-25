# DotNet Shell Configuration System

This comprehensive configuration system provides enterprise-grade configuration management for the DotNet Shell project, implementing Task 1.4 from the project roadmap.

## Features

### 🔧 Multi-Source Configuration Hierarchy

The configuration system supports loading from multiple sources with proper precedence:

1. **Base configuration** (`appsettings.json`)
2. **Environment-specific configuration** (`appsettings.{Environment}.json`)
3. **Local development overrides** (`appsettings.Local.json` - Development only)
4. **User secrets** (Development only)
5. **Environment variables**
6. **Command-line arguments**
7. **External configuration providers** (Azure Key Vault, Database, HTTP services)

### 🌍 Environment-Specific Configuration Support

- Automatic environment detection
- Environment-specific configuration file loading
- Development, Staging, Production environment support
- Configuration transformation capabilities

### ✅ Configuration Validation

- **Data Annotations Support**: Validate configuration using standard .NET data annotations
- **IValidatable Pattern**: Custom validation interface for complex validation logic
- **Startup Validation**: Fail-fast validation during application startup
- **Custom Validation Rules**: Extensible validation rule system

### 🔐 Secret Placeholder Resolution

The system supports secure secret management through placeholder syntax:

- **Placeholder Syntax**: `@Provider:SecretName` (e.g., `@KeyVault:DatabasePassword`)
- **Multiple Providers**: Environment variables, Azure Key Vault, in-memory, file-based
- **Automatic Resolution**: Secrets are resolved transparently when configuration is accessed
- **Development Support**: In-memory and file-based providers for local development

### 🔄 Configuration Hot-Reload

- **File Watchers**: Automatically detect changes in configuration files
- **Change Notifications**: Subscribe to configuration change events
- **Module Notifications**: Notify modules when their configuration changes
- **IOptionsMonitor Pattern**: Standard .NET configuration change monitoring

### 📋 Configuration Schema Documentation

- **JSON Schema**: Complete JSON schema for configuration validation
- **Metadata Attributes**: Configuration property documentation and categorization
- **Auto-Generated Documentation**: Schema-based configuration documentation

## Architecture

```
DotNetShell.Core/Configuration/
├── ConfigurationService.cs          # Main configuration service implementation
├── ConfigurationBuilder.cs          # Configuration hierarchy builder
├── ConfigurationValidator.cs        # Validation engine with data annotations
├── SecretResolver.cs                # Secret placeholder resolution
├── ConfigurationOptions.cs          # Strongly-typed configuration classes
├── ConfigurationHotReload.cs        # Hot-reload and change notification system
├── shell-config-schema.json         # JSON schema for configuration
├── Providers/                       # Configuration providers
│   ├── AzureKeyVaultConfigurationProvider.cs
│   ├── ExternalConfigurationProvider.cs
│   ├── DatabaseConfigurationProvider.cs
│   ├── ModuleConfigurationProvider.cs
│   └── FeatureFlagConfigurationProvider.cs
└── README.md                        # This documentation
```

## Usage

### Basic Configuration Setup

```csharp
// In Program.cs
var builder = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.Sources.Clear();

        var shellConfigBuilder = new ShellConfigurationBuilder(context.HostingEnvironment);
        shellConfigBuilder.AddDefaults(new ShellConfigurationOptions
        {
            CommandLineArgs = args,
            ReloadOnChange = true,
            ValidateOnStartup = true
        });

        var configuration = shellConfigBuilder.Build();
        config.AddConfiguration(configuration);
    });
```

### Strongly-Typed Configuration

```csharp
// Register configuration options
services.Configure<ShellOptions>(configuration.GetSection(ShellOptions.SectionName));
services.Configure<AuthenticationOptions>(configuration.GetSection("Shell:Services:Authentication"));

// Inject and use
public class MyService
{
    private readonly IOptions<ShellOptions> _shellOptions;

    public MyService(IOptions<ShellOptions> shellOptions)
    {
        _shellOptions = shellOptions;
    }

    public void DoSomething()
    {
        var shellName = _shellOptions.Value.Name;
        var isAuthEnabled = _shellOptions.Value.Services.Authentication.Enabled;
    }
}
```

### Configuration Service Usage

```csharp
// Inject IConfigurationService
public class MyController : ControllerBase
{
    private readonly IConfigurationService _configService;

    public MyController(IConfigurationService configService)
    {
        _configService = configService;
    }

    public IActionResult GetConfig()
    {
        // Get simple values
        var appName = _configService.GetValue("Shell:Name", "Default Shell");
        var isFeatureEnabled = _configService.IsFeatureEnabled("NewAuthFlow");

        // Get strongly-typed section
        var authConfig = _configService.GetSection<AuthenticationOptions>("Shell:Services:Authentication");

        // Get secure values (with secret resolution)
        var connectionString = await _configService.GetSecureValueAsync("ConnectionStrings:Default");

        return Ok(new { appName, isFeatureEnabled, authConfig });
    }
}
```

### Secret Placeholder Usage

```json
{
  "Shell": {
    "Services": {
      "Authentication": {
        "JWT": {
          "SecretKey": "@KeyVault:JWT-SigningKey"
        }
      }
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "@Env:DATABASE_CONNECTION_STRING",
    "Redis": "@KeyVault:Redis-ConnectionString"
  }
}
```

### Configuration Validation

```csharp
[ConfigurationValidation(Description = "Authentication service settings")]
public class AuthenticationOptions : ValidatableOptions
{
    [Required]
    public bool Enabled { get; set; } = true;

    [Required]
    public string DefaultProvider { get; set; } = "JWT";

    public JwtOptions JWT { get; set; } = new();

    public override bool TryValidate(out ICollection<ValidationResult> validationResults)
    {
        var isValid = base.TryValidate(out validationResults);

        // Custom validation logic
        if (Enabled && string.IsNullOrEmpty(DefaultProvider))
        {
            validationResults.Add(new ValidationResult(
                "DefaultProvider is required when authentication is enabled",
                new[] { nameof(DefaultProvider) }));
            isValid = false;
        }

        return isValid;
    }
}
```

### Hot-Reload Subscriptions

```csharp
// Subscribe to configuration changes
public class MyService : IDisposable
{
    private readonly IConfigurationHotReloadService _hotReloadService;
    private readonly IDisposable _subscription;

    public MyService(IConfigurationHotReloadService hotReloadService)
    {
        _hotReloadService = hotReloadService;

        // Subscribe to specific configuration changes
        _subscription = _hotReloadService.RegisterCallback("Shell:Services:*", OnConfigurationChanged);
    }

    private void OnConfigurationChanged(ConfigurationChangeEventArgs args)
    {
        _logger.LogInformation("Configuration changed: {Key} = {NewValue}", args.Key, args.NewValue);

        // React to configuration changes
        if (args.Key.StartsWith("Shell:Services:Authentication"))
        {
            // Reconfigure authentication
            ReconfigureAuthentication();
        }
    }

    public void Dispose()
    {
        _subscription?.Dispose();
    }
}
```

## Configuration Providers

### Azure Key Vault Provider

```csharp
builder.AddAzureKeyVault("https://your-keyvault.vault.azure.net/", optional: true);
```

### Database Configuration Provider

```csharp
builder.AddDatabase("Server=localhost;Database=Config;Trusted_Connection=true;",
    tableName: "Configuration",
    refreshInterval: TimeSpan.FromMinutes(5));
```

### External HTTP Service Provider

```csharp
builder.AddExternalService("https://config-service.internal.com/api/config",
    refreshInterval: TimeSpan.FromMinutes(2));
```

### Module Configuration Provider

```csharp
builder.AddModuleConfigurations("./modules");
```

### Feature Flag Provider

```csharp
// From HTTP service
builder.AddFeatureFlags("https://feature-flags.internal.com");

// From local file
builder.AddFeatureFlags("./feature-flags.json");
```

## Configuration Schema

The system includes a complete JSON schema (`shell-config-schema.json`) that provides:

- **Type validation** for all configuration properties
- **Default values** and constraints
- **Documentation** for each configuration option
- **IDE support** with IntelliSense and validation
- **CI/CD integration** for configuration validation

## Environment Variables

The configuration system supports these environment variable patterns:

- `ASPNETCORE_ENVIRONMENT` - Sets the environment name
- `Shell__Name` - Shell application name
- `Shell__Services__Authentication__Enabled` - Enable/disable authentication
- `ConnectionStrings__DefaultConnection` - Database connection string

Use double underscores (`__`) to represent configuration hierarchy levels.

## Best Practices

1. **Use strongly-typed options** instead of accessing IConfiguration directly
2. **Implement validation** for all configuration sections
3. **Use secret placeholders** for sensitive data
4. **Subscribe to hot-reload events** for configuration-dependent services
5. **Validate configuration** during startup to fail fast
6. **Document configuration** using the provided schema and attributes
7. **Use environment-specific files** for environment-specific settings
8. **Keep sensitive data** out of configuration files in production

## Security Considerations

- **Secret Resolution**: Secrets are resolved at runtime, not stored in configuration files
- **Sensitive Data Marking**: Use `IsSensitive = true` in validation attributes
- **Provider Security**: Each secret provider implements its own security model
- **Development vs Production**: Use different secret providers for different environments
- **Audit Logging**: Configuration changes are logged for audit purposes

## Performance Features

- **Lazy Loading**: Secrets are resolved only when accessed
- **Caching**: Resolved secrets are cached to avoid repeated lookups
- **Background Refresh**: External providers refresh configuration in the background
- **Change Detection**: Efficient change detection minimizes unnecessary reloads
- **Provider Health Checks**: Unhealthy providers are automatically bypassed

## Troubleshooting

### Common Issues

1. **Configuration validation fails**: Check data annotations and custom validation rules
2. **Secrets not resolving**: Verify secret provider configuration and connectivity
3. **Hot-reload not working**: Ensure file watchers have proper permissions
4. **Module configuration not loading**: Check module directory structure and file names

### Debugging

Enable detailed logging to troubleshoot configuration issues:

```json
{
  "Logging": {
    "LogLevel": {
      "DotNetShell.Core.Configuration": "Debug"
    }
  }
}
```

## Integration with Host

The configuration system is fully integrated with the DotNet Shell Host project and automatically:

- **Registers services** in the DI container
- **Configures validation** during startup
- **Enables hot-reload** for supported scenarios
- **Provides configuration endpoints** for debugging (in development)

This configuration system provides a solid foundation for enterprise applications requiring flexible, secure, and maintainable configuration management.