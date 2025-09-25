using Microsoft.Extensions.DependencyInjection;
using DotNetShell.Core.DependencyInjection;
using Xunit;

namespace DotNetShell.Core.Tests.DependencyInjection;

public class HierarchicalServiceProviderTests
{
    [Fact]
    public void GetService_FromChildProvider_ReturnsChildService()
    {
        // Arrange
        var parentServices = new ServiceCollection();
        parentServices.AddTransient<ITestService, ParentTestService>();
        var parentProvider = parentServices.BuildServiceProvider();

        var childServices = new ServiceCollection();
        childServices.AddTransient<ITestService, ChildTestService>();

        var hierarchicalProvider = new HierarchicalServiceProvider(parentProvider, childServices);

        // Act
        var service = hierarchicalProvider.GetService<ITestService>();

        // Assert
        Assert.NotNull(service);
        Assert.IsType<ChildTestService>(service);
        Assert.Equal("ChildTestService", service.GetMessage());
    }

    [Fact]
    public void GetService_OnlyInParentProvider_ReturnsParentService()
    {
        // Arrange
        var parentServices = new ServiceCollection();
        parentServices.AddTransient<IParentOnlyService, ParentOnlyService>();
        var parentProvider = parentServices.BuildServiceProvider();

        var childServices = new ServiceCollection();
        var hierarchicalProvider = new HierarchicalServiceProvider(parentProvider, childServices);

        // Act
        var service = hierarchicalProvider.GetService<IParentOnlyService>();

        // Assert
        Assert.NotNull(service);
        Assert.IsType<ParentOnlyService>(service);
        Assert.Equal("ParentOnlyService", service.GetMessage());
    }

    [Fact]
    public void GetService_NotRegistered_ReturnsNull()
    {
        // Arrange
        var parentServices = new ServiceCollection();
        var parentProvider = parentServices.BuildServiceProvider();

        var childServices = new ServiceCollection();
        var hierarchicalProvider = new HierarchicalServiceProvider(parentProvider, childServices);

        // Act
        var service = hierarchicalProvider.GetService<IUnregisteredService>();

        // Assert
        Assert.Null(service);
    }

    [Fact]
    public void GetRequiredService_NotRegistered_ThrowsException()
    {
        // Arrange
        var parentServices = new ServiceCollection();
        var parentProvider = parentServices.BuildServiceProvider();

        var childServices = new ServiceCollection();
        var hierarchicalProvider = new HierarchicalServiceProvider(parentProvider, childServices);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            hierarchicalProvider.GetRequiredService<IUnregisteredService>());
    }

    [Fact]
    public void GetServices_FromBothProviders_ReturnsAllServices()
    {
        // Arrange
        var parentServices = new ServiceCollection();
        parentServices.AddTransient<ITestService, ParentTestService>();
        parentServices.AddTransient<ITestService, AnotherParentTestService>();
        var parentProvider = parentServices.BuildServiceProvider();

        var childServices = new ServiceCollection();
        childServices.AddTransient<ITestService, ChildTestService>();

        var hierarchicalProvider = new HierarchicalServiceProvider(parentProvider, childServices);

        // Act
        var services = hierarchicalProvider.GetServices<ITestService>().ToList();

        // Assert
        Assert.Equal(3, services.Count);
        Assert.Contains(services, s => s is ChildTestService);
        Assert.Contains(services, s => s is ParentTestService);
        Assert.Contains(services, s => s is AnotherParentTestService);
    }

    [Fact]
    public void CreateScope_CreatesNewHierarchicalScope()
    {
        // Arrange
        var parentServices = new ServiceCollection();
        parentServices.AddScoped<IScopedService, ScopedService>();
        var parentProvider = parentServices.BuildServiceProvider();

        var childServices = new ServiceCollection();
        childServices.AddScoped<IChildScopedService, ChildScopedService>();

        var hierarchicalProvider = new HierarchicalServiceProvider(parentProvider, childServices);

        // Act
        using var scope = hierarchicalProvider.CreateScope();
        var scopedService1 = scope.ServiceProvider.GetService<IScopedService>();
        var scopedService2 = scope.ServiceProvider.GetService<IScopedService>();
        var childScopedService = scope.ServiceProvider.GetService<IChildScopedService>();

        // Assert
        Assert.NotNull(scopedService1);
        Assert.NotNull(scopedService2);
        Assert.NotNull(childScopedService);
        Assert.Same(scopedService1, scopedService2); // Same instance within scope
    }

    [Fact]
    public void IsServiceRegistered_WithRegisteredService_ReturnsTrue()
    {
        // Arrange
        var parentServices = new ServiceCollection();
        parentServices.AddTransient<IParentOnlyService, ParentOnlyService>();
        var parentProvider = parentServices.BuildServiceProvider();

        var childServices = new ServiceCollection();
        childServices.AddTransient<ITestService, ChildTestService>();

        var hierarchicalProvider = new HierarchicalServiceProvider(parentProvider, childServices);

        // Act & Assert
        Assert.True(hierarchicalProvider.IsServiceRegistered(typeof(ITestService)));
        Assert.True(hierarchicalProvider.IsServiceRegistered(typeof(IParentOnlyService)));
        Assert.False(hierarchicalProvider.IsServiceRegistered(typeof(IUnregisteredService)));
    }

    [Fact]
    public void GetResolutionPath_TracksServiceResolution()
    {
        // Arrange
        var parentServices = new ServiceCollection();
        parentServices.AddTransient<IParentOnlyService, ParentOnlyService>();
        var parentProvider = parentServices.BuildServiceProvider();

        var childServices = new ServiceCollection();
        childServices.AddTransient<ITestService, ChildTestService>();

        var hierarchicalProvider = new HierarchicalServiceProvider(parentProvider, childServices);

        // Act
        var childPath = hierarchicalProvider.GetResolutionPath(typeof(ITestService));
        var parentPath = hierarchicalProvider.GetResolutionPath(typeof(IParentOnlyService));
        var nonePath = hierarchicalProvider.GetResolutionPath(typeof(IUnregisteredService));

        // Assert
        Assert.Equal("Child", childPath.ResolvedFrom);
        Assert.Equal(typeof(ChildTestService), childPath.ActualType);

        Assert.Equal("Parent", parentPath.ResolvedFrom);
        Assert.Equal(typeof(ParentOnlyService), parentPath.ActualType);

        Assert.Equal("None", nonePath.ResolvedFrom);
        Assert.Null(nonePath.ActualType);
    }

    [Fact]
    public void Dispose_DisposesAllResources()
    {
        // Arrange
        var parentServices = new ServiceCollection();
        var parentProvider = parentServices.BuildServiceProvider();

        var childServices = new ServiceCollection();
        childServices.AddTransient<IDisposableService, TestDisposableService>();

        var hierarchicalProvider = new HierarchicalServiceProvider(parentProvider, childServices);

        // Get a service to ensure it's tracked
        var disposableService = hierarchicalProvider.GetService<IDisposableService>() as TestDisposableService;
        Assert.NotNull(disposableService);

        // Act
        hierarchicalProvider.Dispose();

        // Assert
        Assert.True(disposableService.IsDisposed);
    }

    [Fact]
    public async Task DisposeAsync_DisposesAllResourcesAsync()
    {
        // Arrange
        var parentServices = new ServiceCollection();
        var parentProvider = parentServices.BuildServiceProvider();

        var childServices = new ServiceCollection();
        childServices.AddTransient<IAsyncDisposableService, TestAsyncDisposableService>();

        var hierarchicalProvider = new HierarchicalServiceProvider(parentProvider, childServices);

        // Get a service to ensure it's tracked
        var disposableService = hierarchicalProvider.GetService<IAsyncDisposableService>() as TestAsyncDisposableService;
        Assert.NotNull(disposableService);

        // Act
        await hierarchicalProvider.DisposeAsync();

        // Assert
        Assert.True(disposableService.IsDisposed);
    }

    [Fact]
    public void GetService_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var parentServices = new ServiceCollection();
        var parentProvider = parentServices.BuildServiceProvider();

        var childServices = new ServiceCollection();
        var hierarchicalProvider = new HierarchicalServiceProvider(parentProvider, childServices);

        hierarchicalProvider.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() =>
            hierarchicalProvider.GetService<ITestService>());
    }
}

// Test services for HierarchicalServiceProvider tests
public class ParentTestService : ITestService
{
    public string GetMessage() => "ParentTestService";
}

public class ChildTestService : ITestService
{
    public string GetMessage() => "ChildTestService";
}

public class AnotherParentTestService : ITestService
{
    public string GetMessage() => "AnotherParentTestService";
}

public interface IParentOnlyService
{
    string GetMessage();
}

public class ParentOnlyService : IParentOnlyService
{
    public string GetMessage() => "ParentOnlyService";
}

public interface IScopedService
{
    string GetMessage();
}

public class ScopedService : IScopedService
{
    public string GetMessage() => "ScopedService";
}

public interface IChildScopedService
{
    string GetMessage();
}

public class ChildScopedService : IChildScopedService
{
    public string GetMessage() => "ChildScopedService";
}

public class TestDisposableService : IDisposableService
{
    public bool IsDisposed { get; private set; }

    public string GetMessage() => "TestDisposableService";

    public void Dispose()
    {
        IsDisposed = true;
    }
}

public interface IAsyncDisposableService
{
    string GetMessage();
}

public class TestAsyncDisposableService : IAsyncDisposableService, IAsyncDisposable
{
    public bool IsDisposed { get; private set; }

    public string GetMessage() => "TestAsyncDisposableService";

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}