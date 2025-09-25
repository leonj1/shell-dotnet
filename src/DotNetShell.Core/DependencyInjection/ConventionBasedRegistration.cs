using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace DotNetShell.Core.DependencyInjection;

/// <summary>
/// Provides convention-based automatic service registration.
/// </summary>
public class ConventionBasedRegistration
{
    private readonly ConventionBasedRegistrationOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConventionBasedRegistration"/> class.
    /// </summary>
    public ConventionBasedRegistration() : this(new ConventionBasedRegistrationOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConventionBasedRegistration"/> class.
    /// </summary>
    /// <param name="options">The registration options.</param>
    public ConventionBasedRegistration(ConventionBasedRegistrationOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Registers services from the specified assemblies using conventions and attributes.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>The service collection for chaining.</returns>
    public IServiceCollection RegisterFromAssemblies(IServiceCollection services, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        var registrationResults = new List<ServiceRegistrationResult>();

        foreach (var assembly in assemblies)
        {
            var types = GetTypesFromAssembly(assembly);
            foreach (var type in types)
            {
                var result = ProcessType(services, type);
                if (result != null)
                {
                    registrationResults.Add(result);
                }
            }
        }

        // Log registration results if logger is available
        LogRegistrationResults(registrationResults);

        return services;
    }

    /// <summary>
    /// Registers services from types in the specified namespace.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="namespaceName">The namespace to filter by.</param>
    /// <returns>The service collection for chaining.</returns>
    public IServiceCollection RegisterFromNamespace(IServiceCollection services, Assembly assembly, string namespaceName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(namespaceName);

        var types = GetTypesFromAssembly(assembly)
            .Where(t => t.Namespace?.StartsWith(namespaceName) == true);

        var registrationResults = new List<ServiceRegistrationResult>();

        foreach (var type in types)
        {
            var result = ProcessType(services, type);
            if (result != null)
            {
                registrationResults.Add(result);
            }
        }

        LogRegistrationResults(registrationResults);
        return services;
    }

    /// <summary>
    /// Registers a specific type using conventions and attributes.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="type">The type to register.</param>
    /// <returns>Registration result.</returns>
    public ServiceRegistrationResult? RegisterType(IServiceCollection services, Type type)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(type);

        return ProcessType(services, type);
    }

    private ServiceRegistrationResult? ProcessType(IServiceCollection services, Type type)
    {
        // Skip abstract classes, interfaces, and generic type definitions
        if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
        {
            return null;
        }

        // Check for exclusion attribute
        if (type.GetCustomAttribute<ExcludeFromServiceRegistrationAttribute>() != null)
        {
            return ServiceRegistrationResult.Skipped(type, "Excluded by attribute");
        }

        // Check for explicit service registration attribute
        var serviceRegistrationAttr = type.GetCustomAttribute<ServiceRegistrationAttribute>();
        if (serviceRegistrationAttr != null)
        {
            return ProcessAttributeBasedRegistration(services, type, serviceRegistrationAttr);
        }

        // Apply convention-based registration
        return ProcessConventionBasedRegistration(services, type);
    }

    private ServiceRegistrationResult ProcessAttributeBasedRegistration(
        IServiceCollection services,
        Type type,
        ServiceRegistrationAttribute attribute)
    {
        try
        {
            var serviceTypes = DetermineServiceTypes(type, attribute);
            var registrations = new List<ServiceDescriptor>();

            foreach (var serviceType in serviceTypes)
            {
                if (ShouldRegisterService(services, serviceType, attribute.Replace))
                {
                    var descriptor = CreateServiceDescriptor(serviceType, type, attribute);
                    RegisterServiceDescriptor(services, descriptor, attribute.Replace, attribute.TryAdd);
                    registrations.Add(descriptor);
                }
            }

            return ServiceRegistrationResult.Success(type, registrations);
        }
        catch (Exception ex)
        {
            return ServiceRegistrationResult.Failed(type, ex.Message);
        }
    }

    private ServiceRegistrationResult? ProcessConventionBasedRegistration(IServiceCollection services, Type type)
    {
        if (!_options.EnableConventionBasedRegistration)
        {
            return null;
        }

        try
        {
            var serviceTypes = DetermineConventionalServiceTypes(type);
            if (!serviceTypes.Any())
            {
                return null;
            }

            var registrations = new List<ServiceDescriptor>();
            var lifetime = DetermineConventionalLifetime(type);

            foreach (var serviceType in serviceTypes)
            {
                if (ShouldRegisterService(services, serviceType, false))
                {
                    var descriptor = ServiceDescriptor.Describe(serviceType, type, lifetime);
                    RegisterServiceDescriptor(services, descriptor, false, _options.TryAddByDefault);
                    registrations.Add(descriptor);
                }
            }

            return registrations.Any()
                ? ServiceRegistrationResult.Success(type, registrations)
                : ServiceRegistrationResult.Skipped(type, "No suitable service interfaces found");
        }
        catch (Exception ex)
        {
            return ServiceRegistrationResult.Failed(type, ex.Message);
        }
    }

    private IEnumerable<Type> DetermineServiceTypes(Type implementationType, ServiceRegistrationAttribute attribute)
    {
        var serviceTypes = new List<Type>();

        // Primary service type from attribute
        if (attribute.ServiceType != null)
        {
            serviceTypes.Add(attribute.ServiceType);
        }

        // Additional service types from attribute
        if (attribute.AdditionalServiceTypes?.Any() == true)
        {
            serviceTypes.AddRange(attribute.AdditionalServiceTypes);
        }

        // Register as multiple services (all interfaces)
        if (attribute.RegisterAsMultipleServices)
        {
            var interfaces = implementationType.GetInterfaces()
                .Where(i => !IsSystemInterface(i));
            serviceTypes.AddRange(interfaces);
        }

        // If no explicit service types, use the implementation type itself
        if (!serviceTypes.Any())
        {
            serviceTypes.Add(implementationType);
        }

        return serviceTypes.Distinct();
    }

    private IEnumerable<Type> DetermineConventionalServiceTypes(Type implementationType)
    {
        var serviceTypes = new List<Type>();

        // Interface naming convention (IService -> Service)
        if (_options.UseInterfaceNamingConvention)
        {
            var matchingInterfaces = implementationType.GetInterfaces()
                .Where(i => MatchesInterfaceNamingConvention(i, implementationType))
                .Where(i => !IsSystemInterface(i));

            serviceTypes.AddRange(matchingInterfaces);
        }

        // Service suffix convention
        if (_options.UseServiceSuffixConvention && implementationType.Name.EndsWith("Service"))
        {
            var interfaces = implementationType.GetInterfaces()
                .Where(i => i.Name.EndsWith("Service") && !IsSystemInterface(i));
            serviceTypes.AddRange(interfaces);
        }

        // Repository pattern convention
        if (_options.UseRepositoryConvention && implementationType.Name.EndsWith("Repository"))
        {
            var interfaces = implementationType.GetInterfaces()
                .Where(i => i.Name.EndsWith("Repository") && !IsSystemInterface(i));
            serviceTypes.AddRange(interfaces);
        }

        // Manager pattern convention
        if (_options.UseManagerConvention && implementationType.Name.EndsWith("Manager"))
        {
            var interfaces = implementationType.GetInterfaces()
                .Where(i => i.Name.EndsWith("Manager") && !IsSystemInterface(i));
            serviceTypes.AddRange(interfaces);
        }

        // Generic interface convention
        if (_options.UseGenericInterfaceConvention)
        {
            var genericInterfaces = implementationType.GetInterfaces()
                .Where(i => i.IsGenericType && !IsSystemInterface(i));
            serviceTypes.AddRange(genericInterfaces);
        }

        // Self-registration if no interfaces found and enabled
        if (!serviceTypes.Any() && _options.AllowSelfRegistration)
        {
            serviceTypes.Add(implementationType);
        }

        return serviceTypes.Distinct();
    }

    private ServiceLifetime DetermineConventionalLifetime(Type type)
    {
        // Check for specific naming patterns
        if (_options.SingletonNamePatterns?.Any(pattern => type.Name.Contains(pattern)) == true)
        {
            return ServiceLifetime.Singleton;
        }

        if (_options.ScopedNamePatterns?.Any(pattern => type.Name.Contains(pattern)) == true)
        {
            return ServiceLifetime.Scoped;
        }

        if (_options.TransientNamePatterns?.Any(pattern => type.Name.Contains(pattern)) == true)
        {
            return ServiceLifetime.Transient;
        }

        // Check for interface patterns
        var interfaces = type.GetInterfaces();
        if (interfaces.Any(i => _options.SingletonInterfacePatterns?.Contains(i.Name) == true))
        {
            return ServiceLifetime.Singleton;
        }

        if (interfaces.Any(i => _options.ScopedInterfacePatterns?.Contains(i.Name) == true))
        {
            return ServiceLifetime.Scoped;
        }

        // Default lifetime
        return _options.DefaultLifetime;
    }

    private ServiceDescriptor CreateServiceDescriptor(Type serviceType, Type implementationType, ServiceRegistrationAttribute attribute)
    {
        if (attribute.ServiceKey != null)
        {
            // Keyed service
            if (!string.IsNullOrEmpty(attribute.FactoryMethod))
            {
                var factory = CreateKeyedFactory(implementationType, attribute.FactoryMethod);
                return ServiceDescriptor.DescribeKeyed(serviceType, attribute.ServiceKey, factory, attribute.Lifetime);
            }
            else
            {
                return ServiceDescriptor.DescribeKeyed(serviceType, attribute.ServiceKey, implementationType, attribute.Lifetime);
            }
        }
        else
        {
            // Regular service
            if (!string.IsNullOrEmpty(attribute.FactoryMethod))
            {
                var factory = CreateFactory(implementationType, attribute.FactoryMethod);
                return ServiceDescriptor.Describe(serviceType, factory, attribute.Lifetime);
            }
            else
            {
                return ServiceDescriptor.Describe(serviceType, implementationType, attribute.Lifetime);
            }
        }
    }

    private void RegisterServiceDescriptor(IServiceCollection services, ServiceDescriptor descriptor, bool replace, bool tryAdd)
    {
        if (replace)
        {
            services.Replace(descriptor);
        }
        else if (tryAdd)
        {
            services.TryAdd(descriptor);
        }
        else
        {
            services.Add(descriptor);
        }
    }

    private bool ShouldRegisterService(IServiceCollection services, Type serviceType, bool replace)
    {
        if (replace) return true;

        // Check if service is already registered
        var existingRegistration = services.FirstOrDefault(s => s.ServiceType == serviceType);
        return existingRegistration == null || _options.AllowMultipleRegistrations;
    }

    private static bool MatchesInterfaceNamingConvention(Type interfaceType, Type implementationType)
    {
        // IService -> Service convention
        return interfaceType.Name.Equals($"I{implementationType.Name}", StringComparison.Ordinal) ||
               implementationType.Name.Equals(interfaceType.Name.TrimStart('I'), StringComparison.Ordinal);
    }

    private static bool IsSystemInterface(Type type)
    {
        return type.Assembly == typeof(object).Assembly ||
               type.Assembly == typeof(IDisposable).Assembly ||
               type.Namespace?.StartsWith("System") == true ||
               type.Namespace?.StartsWith("Microsoft") == true;
    }

    private static IEnumerable<Type> GetTypesFromAssembly(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Return only the types that loaded successfully
            return ex.Types.Where(t => t != null)!;
        }
    }

    private static Func<IServiceProvider, object> CreateFactory(Type implementationType, string factoryMethodName)
    {
        var method = implementationType.GetMethod(factoryMethodName, BindingFlags.Public | BindingFlags.Static);
        if (method == null)
        {
            throw new InvalidOperationException($"Factory method '{factoryMethodName}' not found on type '{implementationType.Name}'");
        }

        return serviceProvider =>
        {
            var parameters = method.GetParameters()
                .Select(p => serviceProvider.GetRequiredService(p.ParameterType))
                .ToArray();

            return method.Invoke(null, parameters)!;
        };
    }

    private static Func<IServiceProvider, object, object> CreateKeyedFactory(Type implementationType, string factoryMethodName)
    {
        var method = implementationType.GetMethod(factoryMethodName, BindingFlags.Public | BindingFlags.Static);
        if (method == null)
        {
            throw new InvalidOperationException($"Factory method '{factoryMethodName}' not found on type '{implementationType.Name}'");
        }

        return (serviceProvider, key) =>
        {
            var parameters = method.GetParameters()
                .Select(p => p.ParameterType == typeof(object) ? key : serviceProvider.GetRequiredService(p.ParameterType))
                .ToArray();

            return method.Invoke(null, parameters)!;
        };
    }

    private static void LogRegistrationResults(List<ServiceRegistrationResult> results)
    {
        // In a real implementation, use proper logging
        var successful = results.Count(r => r.IsSuccess);
        var skipped = results.Count(r => r.IsSkipped);
        var failed = results.Count(r => r.IsFailed);

        System.Diagnostics.Debug.WriteLine($"Service registration completed: {successful} successful, {skipped} skipped, {failed} failed");

        foreach (var failure in results.Where(r => r.IsFailed))
        {
            System.Diagnostics.Debug.WriteLine($"Failed to register {failure.Type.Name}: {failure.ErrorMessage}");
        }
    }
}

/// <summary>
/// Options for convention-based service registration.
/// </summary>
public class ConventionBasedRegistrationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to enable convention-based registration.
    /// </summary>
    public bool EnableConventionBasedRegistration { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to use interface naming convention (IService -> Service).
    /// </summary>
    public bool UseInterfaceNamingConvention { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to use service suffix convention.
    /// </summary>
    public bool UseServiceSuffixConvention { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to use repository pattern convention.
    /// </summary>
    public bool UseRepositoryConvention { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to use manager pattern convention.
    /// </summary>
    public bool UseManagerConvention { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to use generic interface convention.
    /// </summary>
    public bool UseGenericInterfaceConvention { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to allow self-registration (register type as itself).
    /// </summary>
    public bool AllowSelfRegistration { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to allow multiple registrations for the same service type.
    /// </summary>
    public bool AllowMultipleRegistrations { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to use TryAdd by default instead of Add.
    /// </summary>
    public bool TryAddByDefault { get; set; } = true;

    /// <summary>
    /// Gets or sets the default service lifetime for convention-based registrations.
    /// </summary>
    public ServiceLifetime DefaultLifetime { get; set; } = ServiceLifetime.Transient;

    /// <summary>
    /// Gets or sets name patterns that indicate singleton lifetime.
    /// </summary>
    public string[]? SingletonNamePatterns { get; set; } = new[] { "Cache", "Factory", "Provider", "Manager" };

    /// <summary>
    /// Gets or sets name patterns that indicate scoped lifetime.
    /// </summary>
    public string[]? ScopedNamePatterns { get; set; } = new[] { "Repository", "Context", "Session" };

    /// <summary>
    /// Gets or sets name patterns that indicate transient lifetime.
    /// </summary>
    public string[]? TransientNamePatterns { get; set; } = new[] { "Service", "Handler", "Command", "Query" };

    /// <summary>
    /// Gets or sets interface patterns that indicate singleton lifetime.
    /// </summary>
    public string[]? SingletonInterfacePatterns { get; set; } = new[] { "ICache", "IFactory", "IProvider" };

    /// <summary>
    /// Gets or sets interface patterns that indicate scoped lifetime.
    /// </summary>
    public string[]? ScopedInterfacePatterns { get; set; } = new[] { "IRepository", "IContext", "IUnitOfWork" };

    /// <summary>
    /// Gets or sets assemblies to exclude from scanning.
    /// </summary>
    public string[]? ExcludedAssemblies { get; set; }

    /// <summary>
    /// Gets or sets namespaces to exclude from scanning.
    /// </summary>
    public string[]? ExcludedNamespaces { get; set; } = new[] { "System", "Microsoft" };

    /// <summary>
    /// Gets or sets type predicates for custom filtering.
    /// </summary>
    public Func<Type, bool>[]? TypeFilters { get; set; }

    /// <summary>
    /// Gets or sets custom lifetime selectors.
    /// </summary>
    public Func<Type, ServiceLifetime?>[]? LifetimeSelectors { get; set; }
}

/// <summary>
/// Result of service registration attempt.
/// </summary>
public class ServiceRegistrationResult
{
    /// <summary>
    /// Gets the type that was processed.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Gets a value indicating whether registration was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets a value indicating whether registration was skipped.
    /// </summary>
    public bool IsSkipped { get; init; }

    /// <summary>
    /// Gets a value indicating whether registration failed.
    /// </summary>
    public bool IsFailed => !IsSuccess && !IsSkipped;

    /// <summary>
    /// Gets the error message for failed registrations.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the reason for skipped registrations.
    /// </summary>
    public string? SkipReason { get; init; }

    /// <summary>
    /// Gets the registered service descriptors.
    /// </summary>
    public IReadOnlyList<ServiceDescriptor> Registrations { get; init; } = new List<ServiceDescriptor>();

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceRegistrationResult"/> class.
    /// </summary>
    /// <param name="type">The processed type.</param>
    public ServiceRegistrationResult(Type type)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
    }

    /// <summary>
    /// Creates a successful registration result.
    /// </summary>
    /// <param name="type">The registered type.</param>
    /// <param name="registrations">The service descriptors.</param>
    /// <returns>A successful result.</returns>
    public static ServiceRegistrationResult Success(Type type, IList<ServiceDescriptor> registrations)
    {
        return new ServiceRegistrationResult(type)
        {
            IsSuccess = true,
            Registrations = registrations.ToList().AsReadOnly()
        };
    }

    /// <summary>
    /// Creates a skipped registration result.
    /// </summary>
    /// <param name="type">The skipped type.</param>
    /// <param name="reason">The skip reason.</param>
    /// <returns>A skipped result.</returns>
    public static ServiceRegistrationResult Skipped(Type type, string reason)
    {
        return new ServiceRegistrationResult(type)
        {
            IsSkipped = true,
            SkipReason = reason
        };
    }

    /// <summary>
    /// Creates a failed registration result.
    /// </summary>
    /// <param name="type">The failed type.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>A failed result.</returns>
    public static ServiceRegistrationResult Failed(Type type, string errorMessage)
    {
        return new ServiceRegistrationResult(type)
        {
            ErrorMessage = errorMessage
        };
    }
}