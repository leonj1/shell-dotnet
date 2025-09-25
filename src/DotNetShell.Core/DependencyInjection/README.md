# Dependency Injection System

This directory contains a comprehensive dependency injection system for the .NET Core Shell project, providing hierarchical DI containers with module isolation capabilities.

## Overview

The DI system implements Task 1.5 from TASKS.md and provides:

1. **DI Container Configuration** - ServiceCollectionExtensions for easy service registration
2. **Service Registration Helpers** - Fluent API for service registration with conventions
3. **Hierarchical Service Providers** - Parent-child container relationships
4. **Scoped Service Providers for Modules** - Module isolation with controlled access
5. **Service Lifetime Management** - Advanced lifetime tracking and disposal
6. **Service Validation** - Comprehensive validation of service registrations

## Architecture

### Core Components

#### 1. ServiceCollectionExtensions
- **Location**: `ServiceCollectionExtensions.cs`
- **Purpose**: Fluent API for service registration
- **Features**:
  - Convention-based registration from assemblies
  - Hierarchical and module service provider creation
  - Service validation and lifetime configuration
  - Keyed services, decorators, and conditional registration

#### 2. HierarchicalServiceProvider
- **Location**: `HierarchicalServiceProvider.cs`
- **Purpose**: Parent-child container relationships
- **Features**:
  - Service resolution from child first, then parent
  - Proper disposal tracking and lifecycle management
  - Service resolution path debugging
  - Scope creation with hierarchy preservation

#### 3. ModuleServiceProvider
- **Location**: `ModuleServiceProvider.cs`
- **Purpose**: Module isolation and controlled access
- **Features**:
  - Module-specific service containers
  - Access control through isolation policies
  - Service access logging and statistics
  - Module-scoped service resolution

#### 4. ModuleIsolationPolicy
- **Location**: `ModuleIsolationPolicy.cs`
- **Purpose**: Access control policies for modules
- **Features**:
  - Service access level controls (Global, CrossModule, ModuleOnly, Prohibited)
  - Trusted module designation
  - Framework service allowlists
  - Service access auditing

#### 5. ServiceLifetimeManager
- **Location**: `ServiceLifetimeManager.cs`
- **Purpose**: Service lifetime and disposal management
- **Features**:
  - Automatic disposal tracking for IDisposable/IAsyncDisposable
  - Service lifetime validation
  - Custom lifetime scopes
  - Memory leak detection

#### 6. ServiceValidator
- **Location**: `ServiceValidator.cs`
- **Purpose**: Service registration validation
- **Features**:
  - Dependency validation
  - Circular dependency detection
  - Lifetime compatibility checking
  - Constructor parameter validation

#### 7. ConventionBasedRegistration
- **Location**: `ConventionBasedRegistration.cs`
- **Purpose**: Automatic service registration using conventions
- **Features**:
  - Interface naming conventions (IService -> Service)
  - Service suffix patterns (Repository, Manager, Service)
  - Lifetime inference from naming patterns
  - Assembly scanning with filtering

#### 8. ServiceRegistrationAttribute
- **Location**: `ServiceRegistrationAttribute.cs`
- **Purpose**: Attribute-based service registration
- **Features**:
  - Service lifetime specification
  - Multiple service registration
  - Keyed service support
  - Factory method registration

## Usage Examples

### Basic Service Registration

```csharp
services.AddServices(builder =>
{
    builder.Add<IUserService, UserService>()
           .AsScoped();

    builder.Add<IEmailService>(provider =>
        new EmailService(provider.GetRequiredService<IConfiguration>()))
           .AsSingleton();
});
```

### Convention-Based Registration

```csharp
// Register all services from assemblies using conventions
services.AddServicesFromAssemblies(typeof(UserService).Assembly);

// With custom options
services.AddServicesFromAssemblies(options =>
{
    options.UseInterfaceNamingConvention = true;
    options.UseServiceSuffixConvention = true;
    options.DefaultLifetime = ServiceLifetime.Scoped;
}, assemblies);
```

### Module Service Provider

```csharp
// Create module provider with isolation
var shellProvider = services.BuildServiceProvider();
var moduleServices = new ServiceCollection();
moduleServices.AddTransient<IModuleService, ModuleService>();

var isolationPolicy = new ModuleIsolationPolicy()
    .AllowServicesForModule("MyModule", typeof(ILoggingService))
    .MarkAsGloballyAccessible(typeof(IConfiguration));

var moduleProvider = new ModuleServiceProvider(
    "MyModule",
    shellProvider,
    moduleServices,
    isolationPolicy);
```

### Hierarchical Containers

```csharp
// Create parent-child relationship
var parentProvider = parentServices.BuildServiceProvider();
var childProvider = childServices.BuildHierarchicalServiceProvider(parentProvider);

// Child resolves from both containers
var childService = childProvider.GetService<IChildService>(); // From child
var parentService = childProvider.GetService<IParentService>(); // From parent
```

### Service Validation

```csharp
// Validate service registrations
var validationResult = services.ValidateServices();

if (!validationResult.IsValid)
{
    foreach (var error in validationResult.Errors)
    {
        Console.WriteLine($"Error: {error}");
    }
}
```

### Lifetime Management

```csharp
services.ConfigureServiceLifetimes(builder =>
{
    builder.EnableDisposalTracking()
           .EnableLifetimeValidation()
           .ConfigureMemoryLeakDetection(options =>
           {
               options.CheckInterval = TimeSpan.FromMinutes(5);
               options.AutoTriggerGC = true;
           });
});
```

## Integration with Shell Host

The DI system integrates with the shell host through:

1. **Service Registration**: Extensions are called during startup configuration
2. **Module Loading**: Module service providers are created for each loaded module
3. **Validation**: Service validation runs at startup to ensure correctness
4. **Lifetime Management**: Automatic disposal during application shutdown

## Testing

Comprehensive unit tests are provided in `/tests/DotNetShell.Core.Tests/DependencyInjection/`:

- `ServiceCollectionExtensionsTests.cs` - Tests for fluent API and extensions
- `HierarchicalServiceProviderTests.cs` - Tests for parent-child relationships
- `ModuleServiceProviderTests.cs` - Tests for module isolation
- `ServiceValidatorTests.cs` - Tests for service validation

## Performance Considerations

1. **Service Resolution Cache**: Module providers cache access checks for performance
2. **Weak References**: Lifetime manager uses weak references to avoid memory leaks
3. **Lazy Validation**: Validation can be disabled in production if needed
4. **Optimized Lookups**: Child service types are tracked for faster resolution

## Security Features

1. **Module Isolation**: Services are isolated by module with configurable access
2. **Access Control**: Fine-grained control over which services modules can access
3. **Audit Logging**: Service access attempts are logged for security monitoring
4. **Policy Enforcement**: Isolation policies prevent unauthorized service access

## Extensibility

The system is designed for extensibility:

1. **Custom Isolation Policies**: Implement custom access control logic
2. **Validation Rules**: Add custom validation rules through options
3. **Lifetime Scopes**: Create custom service lifetime scopes
4. **Convention Filters**: Add custom type filters for registration

## Dependencies

The DI system depends on:

- `Microsoft.Extensions.DependencyInjection` - Core DI abstractions
- `Microsoft.Extensions.Logging` - For internal logging (optional)
- `DotNetShell.Abstractions` - Shell abstraction interfaces

No external dependencies are required for core functionality.