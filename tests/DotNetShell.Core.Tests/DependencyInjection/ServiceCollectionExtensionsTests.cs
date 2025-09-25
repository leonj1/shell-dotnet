using Microsoft.Extensions.DependencyInjection;
using DotNetShell.Core.DependencyInjection;
using System.Reflection;
using Xunit;

namespace DotNetShell.Core.Tests.DependencyInjection;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddServices_WithValidConfiguration_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddServices(builder =>
        {
            builder.Add<ITestService, TestService>()
                   .AsTransient();
        });

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetService<ITestService>();
        Assert.NotNull(service);
        Assert.IsType<TestService>(service);
    }

    [Fact]
    public void AddServicesFromAssemblies_WithAttributedClasses_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        services.AddServicesFromAssemblies(assembly);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var attributedService = serviceProvider.GetService<IAttributedService>();
        Assert.NotNull(attributedService);
        Assert.IsType<AttributedService>(attributedService);
    }

    [Fact]
    public void BuildHierarchicalServiceProvider_WithParentAndChild_ResolvesFromBoth()
    {
        // Arrange
        var parentServices = new ServiceCollection();
        parentServices.AddTransient<IParentService, ParentService>();
        var parentProvider = parentServices.BuildServiceProvider();

        var childServices = new ServiceCollection();
        childServices.AddTransient<IChildService, ChildService>();

        // Act
        var hierarchicalProvider = childServices.BuildHierarchicalServiceProvider(parentProvider);

        // Assert
        var parentService = hierarchicalProvider.GetService<IParentService>();
        var childService = hierarchicalProvider.GetService<IChildService>();

        Assert.NotNull(parentService);
        Assert.NotNull(childService);
        Assert.IsType<ParentService>(parentService);
        Assert.IsType<ChildService>(childService);
    }

    [Fact]
    public void BuildModuleServiceProvider_WithModuleId_CreatesIsolatedProvider()
    {
        // Arrange
        var shellServices = new ServiceCollection();
        shellServices.AddTransient<IShellService, ShellService>();
        var shellProvider = shellServices.BuildServiceProvider();

        var moduleServices = new ServiceCollection();
        moduleServices.AddTransient<IModuleService, ModuleService>();

        // Act
        var moduleProvider = moduleServices.BuildModuleServiceProvider("TestModule", shellProvider);

        // Assert
        Assert.NotNull(moduleProvider);
        var moduleService = moduleProvider.GetService<IModuleService>();
        var shellService = moduleProvider.GetService<IShellService>();

        Assert.NotNull(moduleService);
        Assert.NotNull(shellService);
    }

    [Fact]
    public void ValidateServices_WithValidServices_ReturnsValid()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITestService, TestService>();

        // Act
        var result = services.ValidateServices();

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateServices_WithCircularDependency_ReturnsError()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ICircularA, CircularA>();
        services.AddTransient<ICircularB, CircularB>();

        // Act
        var result = services.ValidateServices();

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
        Assert.Contains("circular", result.Errors.First(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddWithDisposalTracking_WithDisposableService_TracksDisposal()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddWithDisposalTracking<IDisposableService, DisposableService>();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetService<IDisposableService>();
        Assert.NotNull(service);

        // Verify ServiceLifetimeManager is registered
        var lifetimeManager = serviceProvider.GetService<ServiceLifetimeManager>();
        Assert.NotNull(lifetimeManager);
    }

    [Fact]
    public void AddKeyed_WithServiceKey_RegistersKeyedService()
    {
        // Arrange
        var services = new ServiceCollection();
        const string serviceKey = "testKey";

        // Act
        services.AddKeyed<ITestService, TestService>(serviceKey);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetKeyedService<ITestService>(serviceKey);
        Assert.NotNull(service);
        Assert.IsType<TestService>(service);
    }

    [Fact]
    public void Replace_WithExistingService_ReplacesRegistration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITestService, TestService>();

        // Act
        services.Replace<ITestService, AlternativeTestService>();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetService<ITestService>();
        Assert.NotNull(service);
        Assert.IsType<AlternativeTestService>(service);
    }

    [Fact]
    public void TryAdd_WithExistingService_DoesNotReplace()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITestService, TestService>();

        // Act
        services.TryAdd<ITestService, AlternativeTestService>();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetService<ITestService>();
        Assert.NotNull(service);
        Assert.IsType<TestService>(service); // Original registration should remain
    }

    [Fact]
    public void Decorate_WithExistingService_WrapsWithDecorator()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITestService, TestService>();

        // Act
        services.Decorate<ITestService, TestServiceDecorator>();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetService<ITestService>();
        Assert.NotNull(service);
        Assert.IsType<TestServiceDecorator>(service);
    }

    [Fact]
    public void IsRegistered_WithRegisteredService_ReturnsTrue()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITestService, TestService>();

        // Act & Assert
        Assert.True(services.IsRegistered<ITestService>());
        Assert.False(services.IsRegistered<IUnregisteredService>());
    }

    [Fact]
    public void GetRegistrations_WithMultipleImplementations_ReturnsAll()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITestService, TestService>();
        services.AddTransient<ITestService, AlternativeTestService>();

        // Act
        var registrations = services.GetRegistrations<ITestService>().ToList();

        // Assert
        Assert.Equal(2, registrations.Count);
    }

    [Fact]
    public void RemoveAll_WithRegisteredService_RemovesAllRegistrations()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITestService, TestService>();
        services.AddTransient<ITestService, AlternativeTestService>();

        // Act
        services.RemoveAll<ITestService>();

        // Assert
        Assert.False(services.IsRegistered<ITestService>());
    }
}

// Test interfaces and classes
public interface ITestService
{
    string GetMessage();
}

public class TestService : ITestService
{
    public string GetMessage() => "TestService";
}

public class AlternativeTestService : ITestService
{
    public string GetMessage() => "AlternativeTestService";
}

public class TestServiceDecorator : ITestService
{
    private readonly ITestService _inner;

    public TestServiceDecorator(ITestService inner)
    {
        _inner = inner;
    }

    public string GetMessage() => $"Decorated: {_inner.GetMessage()}";
}

[ServiceRegistration(typeof(IAttributedService))]
public class AttributedService : IAttributedService
{
    public string GetMessage() => "AttributedService";
}

public interface IAttributedService
{
    string GetMessage();
}

public interface IParentService
{
    string GetMessage();
}

public class ParentService : IParentService
{
    public string GetMessage() => "ParentService";
}

public interface IChildService
{
    string GetMessage();
}

public class ChildService : IChildService
{
    public string GetMessage() => "ChildService";
}

public interface IShellService
{
    string GetMessage();
}

public class ShellService : IShellService
{
    public string GetMessage() => "ShellService";
}

public interface IModuleService
{
    string GetMessage();
}

public class ModuleService : IModuleService
{
    public string GetMessage() => "ModuleService";
}

public interface IDisposableService : IDisposable
{
    string GetMessage();
}

public class DisposableService : IDisposableService
{
    public bool IsDisposed { get; private set; }

    public string GetMessage() => "DisposableService";

    public void Dispose()
    {
        IsDisposed = true;
    }
}

public interface IUnregisteredService
{
    string GetMessage();
}

// Circular dependency test classes
public interface ICircularA
{
    string GetMessage();
}

public interface ICircularB
{
    string GetMessage();
}

public class CircularA : ICircularA
{
    public CircularA(ICircularB circularB) { }
    public string GetMessage() => "CircularA";
}

public class CircularB : ICircularB
{
    public CircularB(ICircularA circularA) { }
    public string GetMessage() => "CircularB";
}