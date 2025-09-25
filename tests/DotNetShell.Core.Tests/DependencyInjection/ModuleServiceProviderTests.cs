using Microsoft.Extensions.DependencyInjection;
using DotNetShell.Core.DependencyInjection;
using Xunit;

namespace DotNetShell.Core.Tests.DependencyInjection;

public class ModuleServiceProviderTests
{
    [Fact]
    public void GetService_WithAllowedService_ReturnsService()
    {
        // Arrange
        var shellServices = new ServiceCollection();
        shellServices.AddTransient<IShellService, TestShellService>();
        var shellProvider = shellServices.BuildServiceProvider();

        var moduleServices = new ServiceCollection();
        moduleServices.AddTransient<IModuleService, TestModuleService>();

        var isolationPolicy = new ModuleIsolationPolicy()
            .AllowServicesForModule("TestModule", typeof(IShellService));

        var moduleProvider = new ModuleServiceProvider("TestModule", shellProvider, moduleServices, isolationPolicy);

        // Act
        var moduleService = moduleProvider.GetService<IModuleService>();
        var shellService = moduleProvider.GetService<IShellService>();

        // Assert
        Assert.NotNull(moduleService);
        Assert.IsType<TestModuleService>(moduleService);
        Assert.NotNull(shellService);
        Assert.IsType<TestShellService>(shellService);
    }

    [Fact]
    public void GetService_WithDeniedService_ReturnsNull()
    {
        // Arrange
        var shellServices = new ServiceCollection();
        shellServices.AddTransient<IRestrictedService, RestrictedService>();
        var shellProvider = shellServices.BuildServiceProvider();

        var moduleServices = new ServiceCollection();

        var isolationPolicy = new ModuleIsolationPolicy
        {
            AllowFrameworkServices = false,
            AllowLoggingServices = false,
            AllowConfigurationServices = false
        };

        var moduleProvider = new ModuleServiceProvider("TestModule", shellProvider, moduleServices, isolationPolicy);

        // Act
        var restrictedService = moduleProvider.GetService<IRestrictedService>();

        // Assert
        Assert.Null(restrictedService);
    }

    [Fact]
    public void GetRequiredService_WithDeniedService_ThrowsServiceAccessDeniedException()
    {
        // Arrange
        var shellServices = new ServiceCollection();
        shellServices.AddTransient<IRestrictedService, RestrictedService>();
        var shellProvider = shellServices.BuildServiceProvider();

        var moduleServices = new ServiceCollection();

        var isolationPolicy = new ModuleIsolationPolicy
        {
            AllowFrameworkServices = false,
            AllowLoggingServices = false,
            AllowConfigurationServices = false
        };

        var moduleProvider = new ModuleServiceProvider("TestModule", shellProvider, moduleServices, isolationPolicy);

        // Act & Assert
        Assert.Throws<ServiceAccessDeniedException>(() =>
            moduleProvider.GetRequiredService<IRestrictedService>());
    }

    [Fact]
    public void GetService_WithTrustedModule_AllowsAllServices()
    {
        // Arrange
        var shellServices = new ServiceCollection();
        shellServices.AddTransient<IRestrictedService, RestrictedService>();
        var shellProvider = shellServices.BuildServiceProvider();

        var moduleServices = new ServiceCollection();

        var isolationPolicy = new ModuleIsolationPolicy
        {
            AllowFrameworkServices = false,
            AllowLoggingServices = false,
            AllowConfigurationServices = false
        }.MarkAsTrusted("TrustedModule");

        var moduleProvider = new ModuleServiceProvider("TrustedModule", shellProvider, moduleServices, isolationPolicy);

        // Act
        var restrictedService = moduleProvider.GetService<IRestrictedService>();

        // Assert
        Assert.NotNull(restrictedService);
        Assert.IsType<RestrictedService>(restrictedService);
    }

    [Fact]
    public void GetService_WithGloballyAccessibleService_AllowsAccess()
    {
        // Arrange
        var shellServices = new ServiceCollection();
        shellServices.AddTransient<IGlobalService, GlobalService>();
        var shellProvider = shellServices.BuildServiceProvider();

        var moduleServices = new ServiceCollection();

        var isolationPolicy = new ModuleIsolationPolicy()
            .MarkAsGloballyAccessible(typeof(IGlobalService));

        var moduleProvider = new ModuleServiceProvider("TestModule", shellProvider, moduleServices, isolationPolicy);

        // Act
        var globalService = moduleProvider.GetService<IGlobalService>();

        // Assert
        Assert.NotNull(globalService);
        Assert.IsType<GlobalService>(globalService);
    }

    [Fact]
    public void CreateScope_CreatesModuleServiceScope()
    {
        // Arrange
        var shellServices = new ServiceCollection();
        var shellProvider = shellServices.BuildServiceProvider();

        var moduleServices = new ServiceCollection();
        moduleServices.AddScoped<IScopedModuleService, ScopedModuleService>();

        var moduleProvider = new ModuleServiceProvider("TestModule", shellProvider, moduleServices);

        // Act
        using var scope = moduleProvider.CreateScope();
        var service1 = scope.ServiceProvider.GetService<IScopedModuleService>();
        var service2 = scope.ServiceProvider.GetService<IScopedModuleService>();

        // Assert
        Assert.NotNull(service1);
        Assert.NotNull(service2);
        Assert.Same(service1, service2); // Same instance within scope

        Assert.IsType<ModuleServiceScope>(scope);
        var moduleScope = scope as ModuleServiceScope;
        Assert.Equal("TestModule", moduleScope!.ModuleId);
    }

    [Fact]
    public void GetStatistics_ReturnsAccessStatistics()
    {
        // Arrange
        var shellServices = new ServiceCollection();
        shellServices.AddTransient<IShellService, TestShellService>();
        shellServices.AddTransient<IRestrictedService, RestrictedService>();
        var shellProvider = shellServices.BuildServiceProvider();

        var moduleServices = new ServiceCollection();

        var isolationPolicy = new ModuleIsolationPolicy()
            .AllowServicesForModule("TestModule", typeof(IShellService));

        var moduleProvider = new ModuleServiceProvider("TestModule", shellProvider, moduleServices, isolationPolicy);

        // Act - make some service requests
        moduleProvider.GetService<IShellService>(); // Should be allowed
        moduleProvider.GetService<IRestrictedService>(); // Should be denied

        var stats = moduleProvider.GetStatistics();

        // Assert
        Assert.Equal("TestModule", stats.ModuleId);
        Assert.True(stats.TotalAccessAttempts >= 2);
        Assert.True(stats.AllowedAccesses >= 1);
        Assert.True(stats.DeniedAccesses >= 1);
    }

    [Fact]
    public void IsServiceAccessible_ChecksAccessibility()
    {
        // Arrange
        var shellServices = new ServiceCollection();
        var shellProvider = shellServices.BuildServiceProvider();

        var moduleServices = new ServiceCollection();

        var isolationPolicy = new ModuleIsolationPolicy()
            .AllowServicesForModule("TestModule", typeof(IShellService));

        var moduleProvider = new ModuleServiceProvider("TestModule", shellProvider, moduleServices, isolationPolicy);

        // Act & Assert
        Assert.True(moduleProvider.IsServiceAccessible(typeof(IShellService)));
        Assert.False(moduleProvider.IsServiceAccessible(typeof(IRestrictedService)));
    }

    [Fact]
    public void GetAccessibleServiceTypes_ReturnsAccessibleTypes()
    {
        // Arrange
        var shellServices = new ServiceCollection();
        var shellProvider = shellServices.BuildServiceProvider();

        var moduleServices = new ServiceCollection();
        moduleServices.AddTransient<IModuleService, TestModuleService>();

        var isolationPolicy = new ModuleIsolationPolicy()
            .AllowServicesForModule("TestModule", typeof(IShellService))
            .MarkAsGloballyAccessible(typeof(IGlobalService));

        var moduleProvider = new ModuleServiceProvider("TestModule", shellProvider, moduleServices, isolationPolicy);

        // Act
        var accessibleTypes = moduleProvider.GetAccessibleServiceTypes().ToList();

        // Assert
        Assert.Contains(typeof(IGlobalService), accessibleTypes);
        Assert.Contains(typeof(IShellService), accessibleTypes);
    }

    [Fact]
    public void ClearCache_ClearsResolutionCache()
    {
        // Arrange
        var shellServices = new ServiceCollection();
        shellServices.AddTransient<IShellService, TestShellService>();
        var shellProvider = shellServices.BuildServiceProvider();

        var moduleServices = new ServiceCollection();

        var isolationPolicy = new ModuleIsolationPolicy()
            .AllowServicesForModule("TestModule", typeof(IShellService));

        var moduleProvider = new ModuleServiceProvider("TestModule", shellProvider, moduleServices, isolationPolicy);

        // Prime the cache
        moduleProvider.GetService<IShellService>();

        // Act
        moduleProvider.ClearCache();

        // Assert - No direct way to verify cache is cleared, but method should not throw
        var service = moduleProvider.GetService<IShellService>();
        Assert.NotNull(service);
    }

    [Fact]
    public void Dispose_DisposesResources()
    {
        // Arrange
        var shellServices = new ServiceCollection();
        var shellProvider = shellServices.BuildServiceProvider();

        var moduleServices = new ServiceCollection();
        moduleServices.AddTransient<IDisposableModuleService, DisposableModuleService>();

        var moduleProvider = new ModuleServiceProvider("TestModule", shellProvider, moduleServices);

        // Get service to ensure it's created
        var disposableService = moduleProvider.GetService<IDisposableModuleService>() as DisposableModuleService;
        Assert.NotNull(disposableService);

        // Act
        moduleProvider.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => moduleProvider.GetService<IModuleService>());
    }

    [Fact]
    public async Task DisposeAsync_DisposesResourcesAsync()
    {
        // Arrange
        var shellServices = new ServiceCollection();
        var shellProvider = shellServices.BuildServiceProvider();

        var moduleServices = new ServiceCollection();

        var moduleProvider = new ModuleServiceProvider("TestModule", shellProvider, moduleServices);

        // Act
        await moduleProvider.DisposeAsync();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => moduleProvider.GetService<IModuleService>());
    }
}

// Test services for ModuleServiceProvider tests
public class TestShellService : IShellService
{
    public string GetMessage() => "TestShellService";
}

public class TestModuleService : IModuleService
{
    public string GetMessage() => "TestModuleService";
}

public interface IRestrictedService
{
    string GetMessage();
}

public class RestrictedService : IRestrictedService
{
    public string GetMessage() => "RestrictedService";
}

public interface IGlobalService
{
    string GetMessage();
}

public class GlobalService : IGlobalService
{
    public string GetMessage() => "GlobalService";
}

public interface IScopedModuleService
{
    string GetMessage();
}

public class ScopedModuleService : IScopedModuleService
{
    public string GetMessage() => "ScopedModuleService";
}

public interface IDisposableModuleService : IDisposable
{
    string GetMessage();
}

public class DisposableModuleService : IDisposableModuleService
{
    public bool IsDisposed { get; private set; }

    public string GetMessage() => "DisposableModuleService";

    public void Dispose()
    {
        IsDisposed = true;
    }
}