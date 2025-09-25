using Microsoft.Extensions.DependencyInjection;
using DotNetShell.Core.DependencyInjection;
using Xunit;

namespace DotNetShell.Core.Tests.DependencyInjection;

public class ServiceValidatorTests
{
    [Fact]
    public void ValidateServices_WithValidServices_ReturnsValid()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<IValidService, ValidService>();
        services.AddSingleton<ISingletonService, SingletonService>();

        var validator = new ServiceValidator();

        // Act
        var result = validator.ValidateServices(services);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateServices_WithMissingDependency_ReturnsError()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<IServiceWithMissingDependency, ServiceWithMissingDependency>();

        var validator = new ServiceValidator();

        // Act
        var result = validator.ValidateServices(services);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("cannot be resolved"));
    }

    [Fact]
    public void ValidateServices_WithCircularDependency_ReturnsError()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ICircularServiceA, CircularServiceA>();
        services.AddTransient<ICircularServiceB, CircularServiceB>();

        var validator = new ServiceValidator();

        // Act
        var result = validator.ValidateServices(services);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("circular", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateServices_WithLifetimeIssues_ReturnsWarning()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<IScopedDependency, ScopedDependency>();
        services.AddSingleton<ISingletonWithScopedDependency, SingletonWithScopedDependency>();

        var validator = new ServiceValidator();

        // Act
        var result = validator.ValidateServices(services);

        // Assert
        Assert.False(result.IsValid); // Should have errors for singleton depending on shorter-lived service
        Assert.Contains(result.Errors, e => e.Contains("Singleton service") && e.Contains("scoped service"));
    }

    [Fact]
    public void ValidateServices_WithAbstractImplementation_ReturnsError()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITestService, AbstractTestService>();

        var validator = new ServiceValidator();

        // Act
        var result = validator.ValidateServices(services);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("abstract"));
    }

    [Fact]
    public void ValidateServices_WithInterfaceImplementation_ReturnsError()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITestService, IAnotherTestService>();

        var validator = new ServiceValidator();

        // Act
        var result = validator.ValidateServices(services);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("interface"));
    }

    [Fact]
    public void ValidateServices_WithIncompatibleImplementationType_ReturnsError()
    {
        // Arrange
        var services = new ServiceCollection();
        services.Add(new ServiceDescriptor(typeof(ITestService), typeof(IncompatibleService), ServiceLifetime.Transient));

        var validator = new ServiceValidator();

        // Act
        var result = validator.ValidateServices(services);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("not assignable"));
    }

    [Fact]
    public void ValidateServices_WithNoPublicConstructors_ReturnsError()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITestService, ServiceWithoutPublicConstructor>();

        var validator = new ServiceValidator();

        // Act
        var result = validator.ValidateServices(services);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("no public constructors"));
    }

    [Fact]
    public void ValidateServices_WithValidKeyedService_ReturnsValid()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddKeyedTransient<ITestService, ValidService>("testKey");

        var validator = new ServiceValidator();

        // Act
        var result = validator.ValidateServices(services);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateServices_WithNullServiceKey_ReturnsError()
    {
        // Arrange
        var services = new ServiceCollection();
        var descriptor = ServiceDescriptor.DescribeKeyed(typeof(ITestService), null, typeof(ValidService), ServiceLifetime.Transient);
        services.Add(descriptor);

        var validator = new ServiceValidator();

        // Act
        var result = validator.ValidateServices(services);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("null service key"));
    }

    [Fact]
    public void ValidateServices_WithFactoryMethod_ValidatesCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITestService>(provider => new ValidService());

        var validator = new ServiceValidator();

        // Act
        var result = validator.ValidateServices(services);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateServices_WithSingletonInstance_ValidatesCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        var instance = new ValidService();
        services.AddSingleton<ITestService>(instance);

        var validator = new ServiceValidator();

        // Act
        var result = validator.ValidateServices(services);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateServices_WithOptionalParameter_AllowsMissingDependency()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<IServiceWithOptionalDependency, ServiceWithOptionalDependency>();

        var validator = new ServiceValidator();

        // Act
        var result = validator.ValidateServices(services);

        // Assert
        // Should only have warnings, not errors, for optional dependencies
        Assert.True(result.IsValid || result.Warnings.Any(w => w.Contains("Optional parameter")));
    }

    [Fact]
    public void ValidateService_SingleService_ValidatesCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITestService, ValidService>();
        services.AddTransient<IDependency, Dependency>();

        var serviceDescriptor = services.First(s => s.ServiceType == typeof(ITestService));
        var validator = new ServiceValidator();

        // Act
        var result = validator.ValidateService(serviceDescriptor, services);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateServices_WithCustomOptions_RespectsOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<IValidService, ValidService>();

        var options = new ServiceValidationOptions
        {
            ValidateDependencies = false,
            ValidateCircularDependencies = false,
            ValidateLifetimeCompatibility = false
        };

        var validator = new ServiceValidator(options);

        // Act
        var result = validator.ValidateServices(services);

        // Assert
        Assert.True(result.IsValid);
    }
}

// Test services for ServiceValidator tests
public interface IValidService
{
    string GetMessage();
}

public class ValidService : IValidService
{
    public string GetMessage() => "ValidService";
}

public interface ISingletonService
{
    string GetMessage();
}

public class SingletonService : ISingletonService
{
    public string GetMessage() => "SingletonService";
}

public interface IServiceWithMissingDependency
{
    string GetMessage();
}

public class ServiceWithMissingDependency : IServiceWithMissingDependency
{
    public ServiceWithMissingDependency(IMissingDependency missingDependency)
    {
    }

    public string GetMessage() => "ServiceWithMissingDependency";
}

public interface IMissingDependency
{
    string GetMessage();
}

// Circular dependency test services
public interface ICircularServiceA
{
    string GetMessage();
}

public interface ICircularServiceB
{
    string GetMessage();
}

public class CircularServiceA : ICircularServiceA
{
    public CircularServiceA(ICircularServiceB circularB) { }
    public string GetMessage() => "CircularServiceA";
}

public class CircularServiceB : ICircularServiceB
{
    public CircularServiceB(ICircularServiceA circularA) { }
    public string GetMessage() => "CircularServiceB";
}

// Lifetime issue test services
public interface IScopedDependency
{
    string GetMessage();
}

public class ScopedDependency : IScopedDependency
{
    public string GetMessage() => "ScopedDependency";
}

public interface ISingletonWithScopedDependency
{
    string GetMessage();
}

public class SingletonWithScopedDependency : ISingletonWithScopedDependency
{
    public SingletonWithScopedDependency(IScopedDependency scopedDependency) { }
    public string GetMessage() => "SingletonWithScopedDependency";
}

// Invalid implementation types
public abstract class AbstractTestService : ITestService
{
    public abstract string GetMessage();
}

public interface IAnotherTestService : ITestService
{
}

public class IncompatibleService
{
    public string GetMessage() => "IncompatibleService";
}

public class ServiceWithoutPublicConstructor : ITestService
{
    private ServiceWithoutPublicConstructor() { }

    public string GetMessage() => "ServiceWithoutPublicConstructor";
}

// Service with optional dependency
public interface IServiceWithOptionalDependency
{
    string GetMessage();
}

public class ServiceWithOptionalDependency : IServiceWithOptionalDependency
{
    public ServiceWithOptionalDependency([Inject(Optional = true)] IMissingDependency? missingDependency = null)
    {
    }

    public string GetMessage() => "ServiceWithOptionalDependency";
}

public interface IDependency
{
    string GetMessage();
}

public class Dependency : IDependency
{
    public string GetMessage() => "Dependency";
}