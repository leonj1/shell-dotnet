using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using DotNetShell.Core.Configuration.Providers;

namespace DotNetShell.Core.Configuration;

/// <summary>
/// Configuration builder that implements the configuration loading hierarchy
/// supporting multiple sources with proper precedence and environment-specific configurations.
/// </summary>
public class ShellConfigurationBuilder
{
    private readonly IConfigurationBuilder _builder;
    private readonly IHostEnvironment _environment;
    private readonly List<Action<IConfigurationBuilder>> _providerActions;

    public ShellConfigurationBuilder(IHostEnvironment environment)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _builder = new Microsoft.Extensions.Configuration.ConfigurationBuilder();
        _providerActions = new List<Action<IConfigurationBuilder>>();
    }

    /// <summary>
    /// Adds the default configuration sources in the correct hierarchy order.
    /// </summary>
    /// <param name="options">Configuration options for customizing the build process.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public ShellConfigurationBuilder AddDefaults(ShellConfigurationOptions? options = null)
    {
        options ??= new ShellConfigurationOptions();

        // Set base path if not already set
        if (string.IsNullOrEmpty(_builder.Properties["BaseDirectory"]?.ToString()))
        {
            _builder.SetBasePath(options.BasePath ?? Directory.GetCurrentDirectory());
        }

        // 1. Base configuration (appsettings.json)
        if (options.IncludeJsonFiles)
        {
            _builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: options.ReloadOnChange);

            // 2. Environment-specific configuration (appsettings.{Environment}.json)
            var environmentFile = $"appsettings.{_environment.EnvironmentName}.json";
            _builder.AddJsonFile(environmentFile, optional: true, reloadOnChange: options.ReloadOnChange);

            // 3. Local development overrides (appsettings.Local.json)
            if (_environment.IsDevelopment())
            {
                _builder.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: options.ReloadOnChange);
            }
        }

        // 4. User secrets (only in development)
        if (_environment.IsDevelopment() && options.IncludeUserSecrets)
        {
            _builder.AddUserSecrets(options.UserSecretsAssembly ?? typeof(ShellConfigurationBuilder).Assembly);
        }

        // 5. Environment variables
        if (options.IncludeEnvironmentVariables)
        {
            if (!string.IsNullOrEmpty(options.EnvironmentPrefix))
            {
                _builder.AddEnvironmentVariables(options.EnvironmentPrefix);
            }
            else
            {
                _builder.AddEnvironmentVariables();
            }
        }

        // 6. Command-line arguments
        if (options.IncludeCommandLine && options.CommandLineArgs != null)
        {
            _builder.AddCommandLine(options.CommandLineArgs, options.CommandLineSwitchMappings);
        }

        return this;
    }

    /// <summary>
    /// Adds a custom configuration provider.
    /// </summary>
    /// <param name="providerAction">Action to configure the provider.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public ShellConfigurationBuilder AddProvider(Action<IConfigurationBuilder> providerAction)
    {
        _providerActions.Add(providerAction);
        return this;
    }

    /// <summary>
    /// Adds Azure Key Vault configuration provider.
    /// </summary>
    /// <param name="keyVaultUrl">The Azure Key Vault URL.</param>
    /// <param name="optional">Whether the Key Vault is optional.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public ShellConfigurationBuilder AddAzureKeyVault(string keyVaultUrl, bool optional = true)
    {
        _providerActions.Add(builder =>
        {
            try
            {
                // Add Key Vault provider (implementation would depend on Azure.Extensions.AspNetCore.Configuration.Secrets)
                builder.Add(new AzureKeyVaultConfigurationProvider(keyVaultUrl, optional));
            }
            catch (Exception ex) when (optional)
            {
                // Log the exception but continue if Key Vault is optional
                Console.WriteLine($"Warning: Azure Key Vault configuration provider failed to initialize: {ex.Message}");
            }
        });

        return this;
    }

    /// <summary>
    /// Adds external configuration service provider.
    /// </summary>
    /// <param name="serviceUrl">The external configuration service URL.</param>
    /// <param name="refreshInterval">How often to refresh the configuration.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public ShellConfigurationBuilder AddExternalService(string serviceUrl, TimeSpan refreshInterval)
    {
        _providerActions.Add(builder =>
        {
            builder.Add(new ExternalConfigurationProvider(serviceUrl, refreshInterval));
        });

        return this;
    }

    /// <summary>
    /// Adds database configuration provider.
    /// </summary>
    /// <param name="connectionString">Database connection string.</param>
    /// <param name="tableName">Configuration table name.</param>
    /// <param name="refreshInterval">How often to refresh the configuration.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public ShellConfigurationBuilder AddDatabase(string connectionString, string tableName = "Configuration", TimeSpan? refreshInterval = null)
    {
        _providerActions.Add(builder =>
        {
            builder.Add(new DatabaseConfigurationProvider(connectionString, tableName, refreshInterval));
        });

        return this;
    }

    /// <summary>
    /// Adds module-specific configuration from the modules directory.
    /// </summary>
    /// <param name="modulesPath">Path to the modules directory.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public ShellConfigurationBuilder AddModuleConfigurations(string modulesPath)
    {
        _providerActions.Add(builder =>
        {
            builder.Add(new ModuleConfigurationProvider(modulesPath));
        });

        return this;
    }

    /// <summary>
    /// Adds feature flag configuration provider.
    /// </summary>
    /// <param name="connectionString">Feature flag service connection string.</param>
    /// <returns>The configuration builder for method chaining.</returns>
    public ShellConfigurationBuilder AddFeatureFlags(string connectionString)
    {
        _providerActions.Add(builder =>
        {
            builder.Add(new FeatureFlagConfigurationProvider(connectionString));
        });

        return this;
    }

    /// <summary>
    /// Builds the final IConfiguration with all configured providers.
    /// </summary>
    /// <returns>The built configuration.</returns>
    public IConfiguration Build()
    {
        // Apply all custom provider actions
        foreach (var providerAction in _providerActions)
        {
            providerAction(_builder);
        }

        return _builder.Build();
    }

    /// <summary>
    /// Gets the underlying IConfigurationBuilder.
    /// </summary>
    public IConfigurationBuilder Builder => _builder;
}

/// <summary>
/// Configuration options for customizing the ShellConfigurationBuilder behavior.
/// </summary>
public class ShellConfigurationOptions
{
    /// <summary>
    /// Gets or sets the base path for configuration files.
    /// </summary>
    public string? BasePath { get; set; }

    /// <summary>
    /// Gets or sets whether to include JSON configuration files.
    /// </summary>
    public bool IncludeJsonFiles { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include user secrets in development.
    /// </summary>
    public bool IncludeUserSecrets { get; set; } = true;

    /// <summary>
    /// Gets or sets the assembly to use for user secrets.
    /// </summary>
    public System.Reflection.Assembly? UserSecretsAssembly { get; set; }

    /// <summary>
    /// Gets or sets whether to include environment variables.
    /// </summary>
    public bool IncludeEnvironmentVariables { get; set; } = true;

    /// <summary>
    /// Gets or sets the prefix for environment variables.
    /// </summary>
    public string? EnvironmentPrefix { get; set; }

    /// <summary>
    /// Gets or sets whether to include command-line arguments.
    /// </summary>
    public bool IncludeCommandLine { get; set; } = true;

    /// <summary>
    /// Gets or sets the command-line arguments.
    /// </summary>
    public string[]? CommandLineArgs { get; set; }

    /// <summary>
    /// Gets or sets the command-line switch mappings.
    /// </summary>
    public IDictionary<string, string>? CommandLineSwitchMappings { get; set; }

    /// <summary>
    /// Gets or sets whether configuration files should reload on change.
    /// </summary>
    public bool ReloadOnChange { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate configuration on startup.
    /// </summary>
    public bool ValidateOnStartup { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to fail fast on validation errors.
    /// </summary>
    public bool FailFastOnValidationErrors { get; set; } = true;
}

/// <summary>
/// Extension methods for IServiceCollection to register the configuration system.
/// </summary>
public static class ShellConfigurationExtensions
{
    /// <summary>
    /// Adds the Shell configuration system to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">The host environment.</param>
    /// <param name="configureOptions">Action to configure options.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddShellConfiguration(
        this Microsoft.Extensions.DependencyInjection.IServiceCollection services,
        IHostEnvironment environment,
        Action<ShellConfigurationOptions>? configureOptions = null)
    {
        var options = new ShellConfigurationOptions();
        configureOptions?.Invoke(options);

        var builder = new ShellConfigurationBuilder(environment);
        builder.AddDefaults(options);

        var configuration = builder.Build();

        services.AddSingleton<IConfiguration>(configuration);

        return services;
    }

    /// <summary>
    /// Adds the Shell configuration system with custom builder configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">The host environment.</param>
    /// <param name="configureBuilder">Action to configure the builder.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddShellConfiguration(
        this Microsoft.Extensions.DependencyInjection.IServiceCollection services,
        IHostEnvironment environment,
        Action<ShellConfigurationBuilder> configureBuilder)
    {
        var builder = new ShellConfigurationBuilder(environment);
        configureBuilder(builder);

        var configuration = builder.Build();

        services.AddSingleton<IConfiguration>(configuration);

        return services;
    }
}