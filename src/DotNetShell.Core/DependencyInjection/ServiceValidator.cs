using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace DotNetShell.Core.DependencyInjection;

/// <summary>
/// Validates service registrations for correctness and potential issues.
/// </summary>
public class ServiceValidator
{
    private readonly ServiceValidationOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceValidator"/> class with default options.
    /// </summary>
    public ServiceValidator() : this(new ServiceValidationOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceValidator"/> class.
    /// </summary>
    /// <param name="options">The validation options.</param>
    public ServiceValidator(ServiceValidationOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Validates all services in the service collection.
    /// </summary>
    /// <param name="services">The service collection to validate.</param>
    /// <returns>The validation result.</returns>
    public ServiceValidationResult ValidateServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var result = new ServiceValidationResult();
        var serviceMap = BuildServiceMap(services);

        // Validate each service registration
        foreach (var service in services)
        {
            ValidateServiceRegistration(service, serviceMap, result);
        }

        // Perform cross-service validations
        ValidateServiceDependencies(services, serviceMap, result);
        ValidateCircularDependencies(services, serviceMap, result);
        ValidateLifetimeCompatibility(services, serviceMap, result);

        return result;
    }

    /// <summary>
    /// Validates a single service registration.
    /// </summary>
    /// <param name="serviceDescriptor">The service descriptor to validate.</param>
    /// <param name="services">All registered services for context.</param>
    /// <returns>The validation result for this service.</returns>
    public ServiceValidationResult ValidateService(ServiceDescriptor serviceDescriptor, IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(serviceDescriptor);
        ArgumentNullException.ThrowIfNull(services);

        var result = new ServiceValidationResult();
        var serviceMap = BuildServiceMap(services);

        ValidateServiceRegistration(serviceDescriptor, serviceMap, result);

        return result;
    }

    private void ValidateServiceRegistration(
        ServiceDescriptor service,
        Dictionary<Type, List<ServiceDescriptor>> serviceMap,
        ServiceValidationResult result)
    {
        try
        {
            // Validate service type
            ValidateServiceType(service, result);

            // Validate implementation
            ValidateImplementation(service, result);

            // Validate lifetime
            ValidateLifetime(service, result);

            // Validate constructors
            ValidateConstructors(service, serviceMap, result);

            // Validate factory method if present
            ValidateFactory(service, result);

            // Validate keyed services
            ValidateKeyedService(service, result);
        }
        catch (Exception ex)
        {
            result.AddError($"Validation failed for service '{service.ServiceType?.Name ?? "Unknown"}': {ex.Message}");
        }
    }

    private void ValidateServiceType(ServiceDescriptor service, ServiceValidationResult result)
    {
        if (service.ServiceType == null)
        {
            result.AddError("Service type cannot be null");
            return;
        }

        // Check if service type is valid
        if (service.ServiceType.IsGenericTypeDefinition && service.ImplementationType?.IsGenericTypeDefinition != true)
        {
            result.AddError($"Generic service type '{service.ServiceType.Name}' requires generic implementation type");
        }

        // Check for problematic service types
        if (_options.ValidateProblematicTypes)
        {
            ValidateProblematicServiceType(service.ServiceType, result);
        }
    }

    private void ValidateImplementation(ServiceDescriptor service, ServiceValidationResult result)
    {
        if (service.ImplementationType != null)
        {
            ValidateImplementationType(service, result);
        }
        else if (service.ImplementationFactory == null && service.ImplementationInstance == null)
        {
            result.AddError($"Service '{service.ServiceType?.Name}' has no implementation type, factory, or instance");
        }
    }

    private void ValidateImplementationType(ServiceDescriptor service, ServiceValidationResult result)
    {
        var implementationType = service.ImplementationType!;
        var serviceType = service.ServiceType!;

        // Check if implementation type is instantiable
        if (implementationType.IsAbstract)
        {
            result.AddError($"Implementation type '{implementationType.Name}' cannot be abstract");
        }

        if (implementationType.IsInterface)
        {
            result.AddError($"Implementation type '{implementationType.Name}' cannot be an interface");
        }

        // Check if implementation type is assignable to service type
        if (!serviceType.IsAssignableFrom(implementationType))
        {
            result.AddError($"Implementation type '{implementationType.Name}' is not assignable to service type '{serviceType.Name}'");
        }

        // Check for public constructors
        var constructors = implementationType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        if (!constructors.Any())
        {
            result.AddError($"Implementation type '{implementationType.Name}' has no public constructors");
        }

        // Check for problematic implementation types
        if (_options.ValidateProblematicTypes)
        {
            ValidateProblematicImplementationType(implementationType, result);
        }
    }

    private void ValidateLifetime(ServiceDescriptor service, ServiceValidationResult result)
    {
        // Validate singleton instance
        if (service.Lifetime == ServiceLifetime.Singleton && service.ImplementationInstance != null)
        {
            ValidateSingletonInstance(service, result);
        }

        // Check for potential lifetime issues
        if (_options.ValidateLifetimeIssues && service.ImplementationType != null)
        {
            ValidateLifetimeIssues(service, result);
        }
    }

    private void ValidateConstructors(
        ServiceDescriptor service,
        Dictionary<Type, List<ServiceDescriptor>> serviceMap,
        ServiceValidationResult result)
    {
        if (service.ImplementationType == null) return;

        var constructors = service.ImplementationType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        if (!constructors.Any())
        {
            result.AddError($"Type '{service.ImplementationType.Name}' has no public constructors");
            return;
        }

        // Find the constructor that will be used by DI
        var selectedConstructor = SelectConstructor(constructors);
        if (selectedConstructor == null)
        {
            result.AddWarning($"Type '{service.ImplementationType.Name}' has multiple constructors. DI may not select the expected one.");
            return;
        }

        // Validate constructor parameters
        ValidateConstructorParameters(selectedConstructor, serviceMap, result, service.ImplementationType);
    }

    private void ValidateConstructorParameters(
        ConstructorInfo constructor,
        Dictionary<Type, List<ServiceDescriptor>> serviceMap,
        ServiceValidationResult result,
        Type implementationType)
    {
        foreach (var parameter in constructor.GetParameters())
        {
            ValidateConstructorParameter(parameter, serviceMap, result, implementationType);
        }
    }

    private void ValidateConstructorParameter(
        ParameterInfo parameter,
        Dictionary<Type, List<ServiceDescriptor>> serviceMap,
        ServiceValidationResult result,
        Type implementationType)
    {
        var parameterType = parameter.ParameterType;

        // Check if parameter can be resolved
        if (!CanResolveParameter(parameterType, serviceMap))
        {
            if (parameter.HasDefaultValue || IsOptionalParameter(parameter))
            {
                result.AddWarning($"Optional parameter '{parameter.Name}' of type '{parameterType.Name}' in '{implementationType.Name}' constructor cannot be resolved");
            }
            else
            {
                result.AddError($"Required parameter '{parameter.Name}' of type '{parameterType.Name}' in '{implementationType.Name}' constructor cannot be resolved");
            }
        }

        // Check for problematic parameter types
        if (_options.ValidateParameterTypes)
        {
            ValidateParameterType(parameter, result, implementationType);
        }
    }

    private void ValidateFactory(ServiceDescriptor service, ServiceValidationResult result)
    {
        if (service.ImplementationFactory == null) return;

        // Basic validation - in a real implementation, you might use more sophisticated analysis
        try
        {
            // Try to inspect the factory delegate
            var method = service.ImplementationFactory.Method;
            if (method.ReturnType != typeof(object) && !service.ServiceType!.IsAssignableFrom(method.ReturnType))
            {
                result.AddWarning($"Factory method return type '{method.ReturnType.Name}' may not be compatible with service type '{service.ServiceType?.Name}'");
            }
        }
        catch (Exception ex)
        {
            result.AddWarning($"Could not validate factory method: {ex.Message}");
        }
    }

    private void ValidateKeyedService(ServiceDescriptor service, ServiceValidationResult result)
    {
        if (!service.IsKeyedService) return;

        if (service.ServiceKey == null)
        {
            result.AddError($"Keyed service '{service.ServiceType?.Name}' has null service key");
        }

        // Validate key type
        if (_options.ValidateServiceKeys && service.ServiceKey != null)
        {
            ValidateServiceKey(service.ServiceKey, service.ServiceType!, result);
        }
    }

    private void ValidateServiceDependencies(
        IServiceCollection services,
        Dictionary<Type, List<ServiceDescriptor>> serviceMap,
        ServiceValidationResult result)
    {
        if (!_options.ValidateDependencies) return;

        foreach (var service in services)
        {
            if (service.ImplementationType != null)
            {
                ValidateServiceDependenciesForType(service.ImplementationType, serviceMap, result);
            }
        }
    }

    private void ValidateCircularDependencies(
        IServiceCollection services,
        Dictionary<Type, List<ServiceDescriptor>> serviceMap,
        ServiceValidationResult result)
    {
        if (!_options.ValidateCircularDependencies) return;

        var visited = new HashSet<Type>();
        var recursionStack = new HashSet<Type>();

        foreach (var service in services)
        {
            if (service.ImplementationType != null && !visited.Contains(service.ServiceType!))
            {
                DetectCircularDependency(service.ServiceType!, serviceMap, visited, recursionStack, result, new List<Type>());
            }
        }
    }

    private void ValidateLifetimeCompatibility(
        IServiceCollection services,
        Dictionary<Type, List<ServiceDescriptor>> serviceMap,
        ServiceValidationResult result)
    {
        if (!_options.ValidateLifetimeCompatibility) return;

        foreach (var service in services)
        {
            if (service.ImplementationType != null)
            {
                ValidateLifetimeCompatibilityForService(service, serviceMap, result);
            }
        }
    }

    private bool DetectCircularDependency(
        Type serviceType,
        Dictionary<Type, List<ServiceDescriptor>> serviceMap,
        HashSet<Type> visited,
        HashSet<Type> recursionStack,
        ServiceValidationResult result,
        List<Type> currentPath)
    {
        if (recursionStack.Contains(serviceType))
        {
            var cyclePath = string.Join(" -> ", currentPath.Select(t => t.Name));
            result.AddError($"Circular dependency detected: {cyclePath} -> {serviceType.Name}");
            return true;
        }

        if (visited.Contains(serviceType)) return false;

        visited.Add(serviceType);
        recursionStack.Add(serviceType);
        currentPath.Add(serviceType);

        if (serviceMap.TryGetValue(serviceType, out var serviceDescriptors))
        {
            foreach (var descriptor in serviceDescriptors)
            {
                if (descriptor.ImplementationType != null)
                {
                    var dependencies = GetTypeDependencies(descriptor.ImplementationType);
                    foreach (var dependency in dependencies)
                    {
                        if (DetectCircularDependency(dependency, serviceMap, visited, recursionStack, result, new List<Type>(currentPath)))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        recursionStack.Remove(serviceType);
        currentPath.RemoveAt(currentPath.Count - 1);
        return false;
    }

    private void ValidateLifetimeCompatibilityForService(
        ServiceDescriptor service,
        Dictionary<Type, List<ServiceDescriptor>> serviceMap,
        ServiceValidationResult result)
    {
        if (service.ImplementationType == null) return;

        var dependencies = GetTypeDependencies(service.ImplementationType);
        foreach (var dependency in dependencies)
        {
            if (serviceMap.TryGetValue(dependency, out var dependencyServices))
            {
                foreach (var depService in dependencyServices)
                {
                    CheckLifetimeCompatibility(service, depService, result);
                }
            }
        }
    }

    private void CheckLifetimeCompatibility(ServiceDescriptor consumer, ServiceDescriptor dependency, ServiceValidationResult result)
    {
        // Singleton consuming shorter-lived services is problematic
        if (consumer.Lifetime == ServiceLifetime.Singleton)
        {
            if (dependency.Lifetime == ServiceLifetime.Scoped)
            {
                result.AddError($"Singleton service '{consumer.ServiceType?.Name}' depends on scoped service '{dependency.ServiceType?.Name}'. This will cause issues.");
            }
            else if (dependency.Lifetime == ServiceLifetime.Transient)
            {
                result.AddWarning($"Singleton service '{consumer.ServiceType?.Name}' depends on transient service '{dependency.ServiceType?.Name}'. The transient service will effectively become a singleton.");
            }
        }
        // Scoped consuming transient is generally okay, but worth noting
        else if (consumer.Lifetime == ServiceLifetime.Scoped && dependency.Lifetime == ServiceLifetime.Transient)
        {
            result.AddInfo($"Scoped service '{consumer.ServiceType?.Name}' depends on transient service '{dependency.ServiceType?.Name}'. The transient service will be created once per scope.");
        }
    }

    private static Dictionary<Type, List<ServiceDescriptor>> BuildServiceMap(IServiceCollection services)
    {
        var serviceMap = new Dictionary<Type, List<ServiceDescriptor>>();

        foreach (var service in services)
        {
            if (service.ServiceType != null)
            {
                if (!serviceMap.ContainsKey(service.ServiceType))
                {
                    serviceMap[service.ServiceType] = new List<ServiceDescriptor>();
                }
                serviceMap[service.ServiceType].Add(service);
            }
        }

        return serviceMap;
    }

    private static IEnumerable<Type> GetTypeDependencies(Type type)
    {
        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        var selectedConstructor = SelectConstructor(constructors);

        if (selectedConstructor != null)
        {
            return selectedConstructor.GetParameters().Select(p => p.ParameterType);
        }

        return Enumerable.Empty<Type>();
    }

    private static ConstructorInfo? SelectConstructor(ConstructorInfo[] constructors)
    {
        // Simple heuristic: prefer the constructor with the most parameters
        // In real DI containers, this logic is more sophisticated
        return constructors.OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
    }

    private bool CanResolveParameter(Type parameterType, Dictionary<Type, List<ServiceDescriptor>> serviceMap)
    {
        // Direct registration
        if (serviceMap.ContainsKey(parameterType))
        {
            return true;
        }

        // Framework types that are typically resolvable
        if (IsFrameworkType(parameterType))
        {
            return true;
        }

        // Generic collections (IEnumerable<T>, etc.)
        if (parameterType.IsGenericType)
        {
            var genericTypeDefinition = parameterType.GetGenericTypeDefinition();
            if (genericTypeDefinition == typeof(IEnumerable<>) ||
                genericTypeDefinition == typeof(ICollection<>) ||
                genericTypeDefinition == typeof(IList<>))
            {
                return true; // DI containers typically resolve these automatically
            }
        }

        return false;
    }

    private static bool IsFrameworkType(Type type)
    {
        return type == typeof(IServiceProvider) ||
               type == typeof(IServiceScope) ||
               type.Assembly == typeof(IServiceProvider).Assembly ||
               type.Namespace?.StartsWith("Microsoft.Extensions") == true;
    }

    private static bool IsOptionalParameter(ParameterInfo parameter)
    {
        return parameter.GetCustomAttribute<InjectAttribute>()?.Optional == true;
    }

    private void ValidateServiceDependenciesForType(Type type, Dictionary<Type, List<ServiceDescriptor>> serviceMap, ServiceValidationResult result)
    {
        var dependencies = GetTypeDependencies(type);
        foreach (var dependency in dependencies)
        {
            if (!CanResolveParameter(dependency, serviceMap))
            {
                result.AddWarning($"Type '{type.Name}' depends on '{dependency.Name}' which may not be resolvable");
            }
        }
    }

    private void ValidateProblematicServiceType(Type serviceType, ServiceValidationResult result)
    {
        // Check for value types (usually problematic)
        if (serviceType.IsValueType)
        {
            result.AddWarning($"Service type '{serviceType.Name}' is a value type, which may cause boxing/unboxing");
        }

        // Check for sealed types
        if (serviceType.IsSealed && !serviceType.IsValueType)
        {
            result.AddInfo($"Service type '{serviceType.Name}' is sealed, which may limit extensibility");
        }
    }

    private void ValidateProblematicImplementationType(Type implementationType, ServiceValidationResult result)
    {
        // Check for static classes
        if (implementationType.IsAbstract && implementationType.IsSealed)
        {
            result.AddError($"Implementation type '{implementationType.Name}' is static and cannot be instantiated");
        }

        // Check for types with finalizers
        if (HasFinalizer(implementationType))
        {
            result.AddWarning($"Implementation type '{implementationType.Name}' has a finalizer, which may impact performance");
        }
    }

    private void ValidateSingletonInstance(ServiceDescriptor service, ServiceValidationResult result)
    {
        var instance = service.ImplementationInstance!;

        // Check if instance implements IDisposable but is singleton
        if (instance is IDisposable)
        {
            result.AddWarning($"Singleton instance of type '{instance.GetType().Name}' implements IDisposable. Ensure proper disposal is handled.");
        }

        // Check thread safety for singleton instances
        if (_options.ValidateThreadSafety)
        {
            ValidateThreadSafety(instance.GetType(), result);
        }
    }

    private void ValidateLifetimeIssues(ServiceDescriptor service, ServiceValidationResult result)
    {
        var implementationType = service.ImplementationType!;

        // Check for IDisposable implementations with inappropriate lifetimes
        if (typeof(IDisposable).IsAssignableFrom(implementationType))
        {
            if (service.Lifetime == ServiceLifetime.Singleton)
            {
                result.AddInfo($"Singleton service '{implementationType.Name}' implements IDisposable. Ensure it's properly disposed on application shutdown.");
            }
        }

        // Check for types that should typically be singletons
        if (_options.SingletonHints?.Any(hint => implementationType.Name.Contains(hint)) == true &&
            service.Lifetime != ServiceLifetime.Singleton)
        {
            result.AddWarning($"Type '{implementationType.Name}' appears to be designed as a singleton but is registered as {service.Lifetime}");
        }
    }

    private void ValidateParameterType(ParameterInfo parameter, ServiceValidationResult result, Type implementationType)
    {
        var parameterType = parameter.ParameterType;

        // Check for problematic parameter types
        if (parameterType == typeof(string) && !parameter.HasDefaultValue)
        {
            result.AddWarning($"Parameter '{parameter.Name}' in '{implementationType.Name}' is string type without default value. Consider using IOptions<T> pattern.");
        }

        // Check for primitive types
        if (parameterType.IsPrimitive && !parameter.HasDefaultValue)
        {
            result.AddWarning($"Parameter '{parameter.Name}' in '{implementationType.Name}' is primitive type '{parameterType.Name}'. Consider using configuration objects.");
        }
    }

    private void ValidateServiceKey(object serviceKey, Type serviceType, ServiceValidationResult result)
    {
        // Validate key type
        if (serviceKey is string stringKey && string.IsNullOrWhiteSpace(stringKey))
        {
            result.AddWarning($"String service key for '{serviceType.Name}' is empty or whitespace");
        }

        // Check for potential key conflicts
        if (_options.DetectKeyConflicts)
        {
            // This would require maintaining state across validations in a real implementation
            result.AddInfo($"Service key validation for '{serviceType.Name}' with key '{serviceKey}'");
        }
    }

    private void ValidateThreadSafety(Type type, ServiceValidationResult result)
    {
        // Basic thread safety checks - this is a simplified implementation
        var fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        var mutableFields = fields.Where(f => !f.IsInitOnly);

        if (mutableFields.Any())
        {
            result.AddWarning($"Type '{type.Name}' has mutable fields and may not be thread-safe for singleton use");
        }
    }

    private static bool HasFinalizer(Type type)
    {
        return type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                  .Any(m => m.Name == "Finalize");
    }
}

/// <summary>
/// Options for service validation.
/// </summary>
public class ServiceValidationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to validate service dependencies.
    /// </summary>
    public bool ValidateDependencies { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to validate circular dependencies.
    /// </summary>
    public bool ValidateCircularDependencies { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to validate lifetime compatibility.
    /// </summary>
    public bool ValidateLifetimeCompatibility { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to validate problematic types.
    /// </summary>
    public bool ValidateProblematicTypes { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to validate parameter types.
    /// </summary>
    public bool ValidateParameterTypes { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to validate lifetime issues.
    /// </summary>
    public bool ValidateLifetimeIssues { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to validate thread safety for singletons.
    /// </summary>
    public bool ValidateThreadSafety { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to validate service keys.
    /// </summary>
    public bool ValidateServiceKeys { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to detect potential key conflicts.
    /// </summary>
    public bool DetectKeyConflicts { get; set; } = false;

    /// <summary>
    /// Gets or sets hints for types that should typically be singletons.
    /// </summary>
    public string[]? SingletonHints { get; set; } = new[] { "Cache", "Factory", "Manager", "Provider" };

    /// <summary>
    /// Gets or sets the maximum depth for dependency validation.
    /// </summary>
    public int MaxValidationDepth { get; set; } = 10;
}

/// <summary>
/// Result of service validation.
/// </summary>
public class ServiceValidationResult
{
    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public List<string> Errors { get; } = new();

    /// <summary>
    /// Gets the validation warnings.
    /// </summary>
    public List<string> Warnings { get; } = new();

    /// <summary>
    /// Gets the validation information messages.
    /// </summary>
    public List<string> Information { get; } = new();

    /// <summary>
    /// Gets a value indicating whether validation passed without errors.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Gets the total number of issues found.
    /// </summary>
    public int TotalIssues => Errors.Count + Warnings.Count;

    /// <summary>
    /// Adds an error to the validation result.
    /// </summary>
    /// <param name="error">The error message.</param>
    public void AddError(string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        Errors.Add(error);
    }

    /// <summary>
    /// Adds a warning to the validation result.
    /// </summary>
    /// <param name="warning">The warning message.</param>
    public void AddWarning(string warning)
    {
        ArgumentNullException.ThrowIfNull(warning);
        Warnings.Add(warning);
    }

    /// <summary>
    /// Adds an information message to the validation result.
    /// </summary>
    /// <param name="info">The information message.</param>
    public void AddInfo(string info)
    {
        ArgumentNullException.ThrowIfNull(info);
        Information.Add(info);
    }

    /// <summary>
    /// Gets a summary of the validation results.
    /// </summary>
    /// <returns>A summary string.</returns>
    public string GetSummary()
    {
        return $"Validation completed: {Errors.Count} errors, {Warnings.Count} warnings, {Information.Count} info messages";
    }

    /// <summary>
    /// Returns all messages as a formatted string.
    /// </summary>
    /// <returns>Formatted validation messages.</returns>
    public override string ToString()
    {
        var messages = new List<string>();

        if (Errors.Any())
        {
            messages.Add("ERRORS:");
            messages.AddRange(Errors.Select(e => $"  - {e}"));
        }

        if (Warnings.Any())
        {
            messages.Add("WARNINGS:");
            messages.AddRange(Warnings.Select(w => $"  - {w}"));
        }

        if (Information.Any())
        {
            messages.Add("INFORMATION:");
            messages.AddRange(Information.Select(i => $"  - {i}"));
        }

        return string.Join(Environment.NewLine, messages);
    }
}