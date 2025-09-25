using DotNetShell.Core.Plugins;
using DotNetShell.Core.DependencyInjection;
using DotNetShell.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using FluentAssertions;

namespace DotNetShell.Core.Tests.Plugins;

/// <summary>
/// Integration tests for the plugin loading system.
/// </summary>
public class PluginLoadingIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ILogger<PluginLoadingIntegrationTests> _logger;
    private readonly string _testPluginsDirectory;

    public PluginLoadingIntegrationTests()
    {
        // Setup test services
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();

        // Configure plugin services
        services.Configure<PluginDiscoveryOptions>(options =>
        {
            _testPluginsDirectory = Path.Combine(Path.GetTempPath(), "test-plugins", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testPluginsDirectory);
            options.PluginDirectories.Add(_testPluginsDirectory);
        });

        services.Configure<PluginValidationOptions>(options =>
        {
            options.EnableSecurityValidation = false; // Simplified for tests
        });

        services.Configure<PluginInitializationOptions>(options =>
        {
            options.ShellVersion = new Version(1, 0, 0);
            options.Environment = "Test";
        });

        services.Configure<PluginLoaderOptions>(options =>
        {
            options.EnablePluginUnloading = true;
            options.ContinueOnFailure = true;
        });

        services.Configure<PluginManagerOptions>(options =>
        {
            options.EnableHealthChecks = false; // Disable for tests
        });

        // Register plugin system services
        services.AddSingleton<IPluginDiscoveryService, PluginDiscoveryService>();
        services.AddSingleton<IPluginValidator, PluginValidator>();
        services.AddSingleton<IPluginMetadataReader, PluginMetadataReader>();
        services.AddSingleton<IPluginInitializer, PluginInitializer>();
        services.AddSingleton<IPluginLoader, PluginLoader>();
        services.AddSingleton<IPluginManager, PluginManager>();
        services.AddSingleton<ModuleServiceProviderFactory>();

        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<PluginLoadingIntegrationTests>>();
    }

    [Fact]
    public async Task PluginDiscovery_WithValidManifest_ShouldDiscoverPlugin()
    {
        // Arrange
        var discoveryService = _serviceProvider.GetRequiredService<IPluginDiscoveryService>();
        await CreateTestPluginManifest("TestPlugin", "1.0.0");

        // Act
        var discoveredPlugins = await discoveryService.DiscoverFromDirectoryAsync(_testPluginsDirectory);

        // Assert
        discoveredPlugins.Should().NotBeNull();
        discoveredPlugins.Should().HaveCount(1);

        var plugin = discoveredPlugins.First();
        plugin.Manifest.Id.Should().Be("TestPlugin");
        plugin.Manifest.Version.Should().Be("1.0.0");
    }

    [Fact]
    public async Task PluginValidator_WithValidManifest_ShouldPassValidation()
    {
        // Arrange
        var validator = _serviceProvider.GetRequiredService<IPluginValidator>();
        var manifest = CreateTestManifest("TestPlugin", "1.0.0");

        // Act
        var result = await validator.ValidateManifestAsync(manifest);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task PluginValidator_WithInvalidManifest_ShouldFailValidation()
    {
        // Arrange
        var validator = _serviceProvider.GetRequiredService<IPluginValidator>();
        var manifest = new PluginManifest(); // Invalid - missing required fields

        // Act
        var result = await validator.ValidateManifestAsync(manifest);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PluginMetadataReader_WithTestAssembly_ShouldExtractMetadata()
    {
        // Arrange
        var metadataReader = _serviceProvider.GetRequiredService<IPluginMetadataReader>();
        var testAssemblyPath = typeof(PluginLoadingIntegrationTests).Assembly.Location;

        // Act
        var metadata = await metadataReader.ReadMetadataAsync(testAssemblyPath);

        // Assert
        metadata.Should().NotBeNull();
        metadata.AssemblyName.Should().NotBeEmpty();
        metadata.AssemblyVersion.Should().NotBeEmpty();
    }

    [Fact]
    public void PluginLoadContext_WithValidPath_ShouldCreateSuccessfully()
    {
        // Arrange
        var testAssemblyPath = typeof(PluginLoadingIntegrationTests).Assembly.Location;

        // Act & Assert
        using var loadContext = new PluginLoadContext(testAssemblyPath, "TestContext", false, _logger);

        loadContext.Should().NotBeNull();
        loadContext.ContextId.Should().StartWith("TestContext");
        loadContext.PluginPath.Should().Be(testAssemblyPath);
    }

    [Fact]
    public async Task PluginLoadContext_WithValidAssembly_ShouldLoadSuccessfully()
    {
        // Arrange
        var testAssemblyPath = typeof(PluginLoadingIntegrationTests).Assembly.Location;

        // Act & Assert
        using var loadContext = new PluginLoadContext(testAssemblyPath, "TestContext", false, _logger);
        var assembly = loadContext.LoadPluginAssembly();

        assembly.Should().NotBeNull();
        assembly.GetName().Name.Should().NotBeEmpty();
    }

    [Fact]
    public void PluginManifest_Validation_ShouldWork()
    {
        // Arrange
        var validManifest = CreateTestManifest("TestPlugin", "1.0.0");
        var invalidManifest = new PluginManifest(); // Missing required fields

        // Act
        var validErrors = validManifest.Validate().ToList();
        var invalidErrors = invalidManifest.Validate().ToList();

        // Assert
        validErrors.Should().BeEmpty();
        invalidErrors.Should().NotBeEmpty();
        invalidErrors.Should().Contain(e => e.Contains("Plugin ID"));
    }

    [Fact]
    public void PluginManifest_CompatibilityCheck_ShouldWork()
    {
        // Arrange
        var manifest = CreateTestManifest("TestPlugin", "1.0.0");
        manifest.MinimumShellVersion = "0.9.0";
        manifest.MaximumShellVersion = "2.0.0";

        var compatibleVersion = new Version(1, 5, 0);
        var tooOldVersion = new Version(0, 8, 0);
        var tooNewVersion = new Version(3, 0, 0);

        // Act & Assert
        manifest.IsCompatibleWith(compatibleVersion).Should().BeTrue();
        manifest.IsCompatibleWith(tooOldVersion).Should().BeFalse();
        manifest.IsCompatibleWith(tooNewVersion).Should().BeFalse();
    }

    [Fact]
    public void PluginManifest_PlatformSupport_ShouldWork()
    {
        // Arrange
        var manifest = CreateTestManifest("TestPlugin", "1.0.0");
        manifest.SupportedPlatforms.Add("Windows");
        manifest.SupportedPlatforms.Add("Linux");

        // Act & Assert
        manifest.SupportsPlatform("Windows").Should().BeTrue();
        manifest.SupportsPlatform("Linux").Should().BeTrue();
        manifest.SupportsPlatform("macOS").Should().BeFalse();
    }

    [Fact]
    public void PluginDependency_VersionCheck_ShouldWork()
    {
        // Arrange
        var dependency = new PluginDependency
        {
            Id = "TestDep",
            MinimumVersion = "1.0.0",
            MaximumVersion = "2.0.0"
        };

        var compatibleVersion = new Version(1, 5, 0);
        var tooOldVersion = new Version(0, 9, 0);
        var tooNewVersion = new Version(2, 1, 0);

        // Act & Assert
        dependency.IsSatisfiedBy(compatibleVersion).Should().BeTrue();
        dependency.IsSatisfiedBy(tooOldVersion).Should().BeFalse();
        dependency.IsSatisfiedBy(tooNewVersion).Should().BeFalse();
    }

    [Fact]
    public void RuntimeDependency_Validation_ShouldWork()
    {
        // Arrange
        var validDependency = new RuntimeDependency
        {
            PackageId = "TestPackage",
            Version = "1.0.0"
        };

        var invalidDependency = new RuntimeDependency(); // Missing required fields

        // Act
        var validErrors = validDependency.Validate().ToList();
        var invalidErrors = invalidDependency.Validate().ToList();

        // Assert
        validErrors.Should().BeEmpty();
        invalidErrors.Should().NotBeEmpty();
        invalidErrors.Should().Contain(e => e.Contains("Package ID"));
    }

    private async Task CreateTestPluginManifest(string pluginId, string version)
    {
        var manifest = CreateTestManifest(pluginId, version);
        var manifestPath = Path.Combine(_testPluginsDirectory, "plugin.json");

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(manifestPath, json);
    }

    private static PluginManifest CreateTestManifest(string id, string version)
    {
        return new PluginManifest
        {
            Id = id,
            Name = id,
            Version = version,
            Description = $"Test plugin {id}",
            Author = "Test Author",
            MainAssembly = "TestPlugin.dll",
            EntryPoint = "TestPlugin.TestModule",
            MinimumShellVersion = "1.0.0"
        };
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testPluginsDirectory))
            {
                Directory.Delete(_testPluginsDirectory, true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup test plugins directory: {Directory}", _testPluginsDirectory);
        }

        _serviceProvider?.Dispose();
    }
}

/// <summary>
/// Test implementation of IBusinessLogicModule for testing purposes.
/// </summary>
public class TestBusinessLogicModule : IBusinessLogicModule
{
    public string Name => "TestModule";
    public Version Version => new Version(1, 0, 0);
    public string Description => "Test module for integration tests";
    public string? Author => "Test Author";
    public IEnumerable<ModuleDependency> Dependencies => Array.Empty<ModuleDependency>();
    public Version MinimumShellVersion => new Version(1, 0, 0);
    public ModuleMetadata Metadata => new ModuleMetadata { Category = "Test" };
    public bool IsEnabled => true;

    public Task<ModuleValidationResult> ValidateAsync(ModuleValidationContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ModuleValidationResult.Success());
    }

    public Task OnInitializeAsync(IServiceCollection services, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task OnConfigureAsync(Microsoft.AspNetCore.Builder.IApplicationBuilder app, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task OnStartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task OnStopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task OnUnloadAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task OnConfigurationChangedAsync(IReadOnlyDictionary<string, object> newConfiguration, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<ModuleHealthResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ModuleHealthResult.Healthy("Test module is healthy"));
    }
}