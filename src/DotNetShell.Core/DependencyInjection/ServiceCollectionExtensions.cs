using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace DotNetShell.Core.DependencyInjection;

/// <summary>
/// Extension methods for IServiceCollection to provide fluent API for service registration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds services to the container with a fluent API.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddServices(this IServiceCollection services, Action<IServiceRegistrationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new ServiceRegistrationBuilder(services);
        configure(builder);
        return services;
    }

    /// <summary>
    /// Registers services from the specified assemblies using conventions and attributes.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">The assemblies to scan for services.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddServicesFromAssemblies(this IServiceCollection services, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);

        var registration = new ConventionBasedRegistration();
        return registration.RegisterFromAssemblies(services, assemblies);
    }

    /// <summary>
    /// Registers services from the specified assemblies using conventions and attributes.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration for the registration process.</param>
    /// <param name="assemblies">The assemblies to scan for services.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddServicesFromAssemblies(
        this IServiceCollection services,
        Action<ConventionBasedRegistrationOptions> configure,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new ConventionBasedRegistrationOptions();
        configure(options);

        var registration = new ConventionBasedRegistration(options);
        return registration.RegisterFromAssemblies(services, assemblies);
    }

    /// <summary>
    /// Creates a hierarchical service provider with parent-child relationship.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="parentProvider">The parent service provider.</param>
    /// <returns>A hierarchical service provider.</returns>
    public static IServiceProvider BuildHierarchicalServiceProvider(
        this IServiceCollection services,
        IServiceProvider parentProvider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(parentProvider);

        return new HierarchicalServiceProvider(parentProvider, services);
    }

    /// <summary>
    /// Creates a module service provider with isolation capabilities.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="parentProvider">The parent service provider.</param>
    /// <returns>A module service provider.</returns>
    public static IServiceProvider BuildModuleServiceProvider(
        this IServiceCollection services,
        string moduleId,
        IServiceProvider parentProvider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(moduleId);
        ArgumentNullException.ThrowIfNull(parentProvider);

        return new ModuleServiceProvider(moduleId, parentProvider, services);
    }

    /// <summary>
    /// Validates all service registrations for correctness.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>Validation results.</returns>
    public static ServiceValidationResult ValidateServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var validator = new ServiceValidator();
        return validator.ValidateServices(services);
    }

    /// <summary>
    /// Validates all service registrations with custom options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Validation options.</param>
    /// <returns>Validation results.</returns>
    public static ServiceValidationResult ValidateServices(this IServiceCollection services, ServiceValidationOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        var validator = new ServiceValidator(options);
        return validator.ValidateServices(services);
    }

    /// <summary>
    /// Configures service lifetime management.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration for lifetime management.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection ConfigureServiceLifetimes(
        this IServiceCollection services,
        Action<ServiceLifetimeManagerBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new ServiceLifetimeManagerBuilder(services);
        configure(builder);
        return services;
    }

    /// <summary>
    /// Adds a service with automatic disposal tracking.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <typeparam name="TImplementation">The implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The service lifetime.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddWithDisposalTracking<TService, TImplementation>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TService : class
        where TImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register the actual service
        services.Add(new ServiceDescriptor(typeof(TService), typeof(TImplementation), lifetime));

        // Register disposal tracking if the implementation is disposable
        if (typeof(IDisposable).IsAssignableFrom(typeof(TImplementation)) ||
            typeof(IAsyncDisposable).IsAssignableFrom(typeof(TImplementation)))
        {
            services.TryAddSingleton<ServiceLifetimeManager>();
        }

        return services;
    }

    /// <summary>
    /// Adds a keyed service with the specified key.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <typeparam name="TImplementation">The implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="serviceKey">The service key.</param>
    /// <param name="lifetime">The service lifetime.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKeyed<TService, TImplementation>(
        this IServiceCollection services,
        object serviceKey,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TService : class
        where TImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serviceKey);

        return services.Add(ServiceDescriptor.DescribeKeyed(typeof(TService), serviceKey, typeof(TImplementation), lifetime));
    }

    /// <summary>
    /// Replaces an existing service registration.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <typeparam name="TImplementation">The implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The service lifetime.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection Replace<TService, TImplementation>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TService : class
        where TImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Replace(ServiceDescriptor.Describe(typeof(TService), typeof(TImplementation), lifetime));
        return services;
    }

    /// <summary>
    /// Adds a service only if no implementation is already registered.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <typeparam name="TImplementation">The implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The service lifetime.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection TryAdd<TService, TImplementation>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TService : class
        where TImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAdd(ServiceDescriptor.Describe(typeof(TService), typeof(TImplementation), lifetime));
        return services;
    }

    /// <summary>
    /// Adds a conditional service that is only registered if the condition is met.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <typeparam name="TImplementation">The implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="condition">The condition to check.</param>
    /// <param name="lifetime">The service lifetime.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIf<TService, TImplementation>(
        this IServiceCollection services,
        Func<IServiceProvider, bool> condition,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TService : class
        where TImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(condition);

        services.Add(ServiceDescriptor.Describe(typeof(TService), serviceProvider =>
        {
            if (condition(serviceProvider))
            {
                return ActivatorUtilities.CreateInstance<TImplementation>(serviceProvider);
            }
            throw new InvalidOperationException($"Service {typeof(TService).Name} is not available under current conditions.");
        }, lifetime));

        return services;
    }

    /// <summary>
    /// Adds a decorator for an existing service.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <typeparam name="TDecorator">The decorator type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection Decorate<TService, TDecorator>(this IServiceCollection services)
        where TService : class
        where TDecorator : class, TService
    {
        ArgumentNullException.ThrowIfNull(services);

        // Find existing service registration
        var serviceDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(TService));
        if (serviceDescriptor == null)
        {
            throw new ArgumentException($"Service {typeof(TService).Name} is not registered.");
        }

        // Remove existing registration
        services.Remove(serviceDescriptor);

        // Add decorator that wraps the original service
        services.Add(ServiceDescriptor.Describe(typeof(TService), serviceProvider =>
        {
            var original = CreateOriginalService(serviceProvider, serviceDescriptor);
            return ActivatorUtilities.CreateInstance<TDecorator>(serviceProvider, original);
        }, serviceDescriptor.Lifetime));

        return services;

        static object CreateOriginalService(IServiceProvider serviceProvider, ServiceDescriptor serviceDescriptor)
        {
            if (serviceDescriptor.ImplementationInstance != null)
            {
                return serviceDescriptor.ImplementationInstance;
            }

            if (serviceDescriptor.ImplementationFactory != null)
            {
                return serviceDescriptor.ImplementationFactory(serviceProvider);
            }

            return ActivatorUtilities.CreateInstance(serviceProvider, serviceDescriptor.ImplementationType!);
        }
    }

    /// <summary>
    /// Removes all registrations for the specified service type.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection RemoveAll<TService>(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        for (int i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(TService))
            {
                services.RemoveAt(i);
            }
        }

        return services;
    }

    /// <summary>
    /// Gets all registered implementations for a service type.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>Collection of service descriptors.</returns>
    public static IEnumerable<ServiceDescriptor> GetRegistrations<TService>(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.Where(s => s.ServiceType == typeof(TService));
    }

    /// <summary>
    /// Checks if a service is registered.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>True if the service is registered; otherwise, false.</returns>
    public static bool IsRegistered<TService>(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.Any(s => s.ServiceType == typeof(TService));
    }

    /// <summary>
    /// Adds services for module isolation support.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddModuleIsolation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ModuleServiceProviderFactory>();
        services.TryAddScoped<IModuleServiceScope, ModuleServiceScope>();
        services.TryAddSingleton<ServiceLifetimeManager>();

        return services;
    }
}

/// <summary>
/// Fluent API builder for service registration.
/// </summary>
public interface IServiceRegistrationBuilder
{
    /// <summary>
    /// Gets the service collection.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Adds a service registration.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <typeparam name="TImplementation">The implementation type.</typeparam>
    /// <returns>A service registration builder.</returns>
    IServiceRegistrationBuilder<TService> Add<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService;

    /// <summary>
    /// Adds a service registration with factory.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <param name="factory">The service factory.</param>
    /// <returns>A service registration builder.</returns>
    IServiceRegistrationBuilder<TService> Add<TService>(Func<IServiceProvider, TService> factory)
        where TService : class;
}

/// <summary>
/// Fluent API builder for specific service registration.
/// </summary>
/// <typeparam name="TService">The service type.</typeparam>
public interface IServiceRegistrationBuilder<TService> where TService : class
{
    /// <summary>
    /// Sets the service lifetime to singleton.
    /// </summary>
    /// <returns>The service registration builder.</returns>
    IServiceRegistrationBuilder<TService> AsSingleton();

    /// <summary>
    /// Sets the service lifetime to scoped.
    /// </summary>
    /// <returns>The service registration builder.</returns>
    IServiceRegistrationBuilder<TService> AsScoped();

    /// <summary>
    /// Sets the service lifetime to transient.
    /// </summary>
    /// <returns>The service registration builder.</returns>
    IServiceRegistrationBuilder<TService> AsTransient();

    /// <summary>
    /// Sets a service key for keyed services.
    /// </summary>
    /// <param name="key">The service key.</param>
    /// <returns>The service registration builder.</returns>
    IServiceRegistrationBuilder<TService> WithKey(object key);

    /// <summary>
    /// Replaces existing registrations.
    /// </summary>
    /// <returns>The service registration builder.</returns>
    IServiceRegistrationBuilder<TService> Replace();

    /// <summary>
    /// Adds only if not already registered.
    /// </summary>
    /// <returns>The service registration builder.</returns>
    IServiceRegistrationBuilder<TService> TryAdd();
}

/// <summary>
/// Implementation of the service registration builder.
/// </summary>
internal class ServiceRegistrationBuilder : IServiceRegistrationBuilder
{
    public IServiceCollection Services { get; }

    public ServiceRegistrationBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public IServiceRegistrationBuilder<TService> Add<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        return new ServiceRegistrationBuilder<TService>(Services, typeof(TImplementation));
    }

    public IServiceRegistrationBuilder<TService> Add<TService>(Func<IServiceProvider, TService> factory)
        where TService : class
    {
        return new ServiceRegistrationBuilder<TService>(Services, factory);
    }
}

/// <summary>
/// Implementation of the specific service registration builder.
/// </summary>
/// <typeparam name="TService">The service type.</typeparam>
internal class ServiceRegistrationBuilder<TService> : IServiceRegistrationBuilder<TService>
    where TService : class
{
    private readonly IServiceCollection _services;
    private readonly Type? _implementationType;
    private readonly Func<IServiceProvider, TService>? _factory;
    private ServiceLifetime _lifetime = ServiceLifetime.Transient;
    private object? _serviceKey;
    private bool _replace;
    private bool _tryAdd;

    public ServiceRegistrationBuilder(IServiceCollection services, Type implementationType)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _implementationType = implementationType ?? throw new ArgumentNullException(nameof(implementationType));
    }

    public ServiceRegistrationBuilder(IServiceCollection services, Func<IServiceProvider, TService> factory)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public IServiceRegistrationBuilder<TService> AsSingleton()
    {
        _lifetime = ServiceLifetime.Singleton;
        RegisterService();
        return this;
    }

    public IServiceRegistrationBuilder<TService> AsScoped()
    {
        _lifetime = ServiceLifetime.Scoped;
        RegisterService();
        return this;
    }

    public IServiceRegistrationBuilder<TService> AsTransient()
    {
        _lifetime = ServiceLifetime.Transient;
        RegisterService();
        return this;
    }

    public IServiceRegistrationBuilder<TService> WithKey(object key)
    {
        _serviceKey = key ?? throw new ArgumentNullException(nameof(key));
        return this;
    }

    public IServiceRegistrationBuilder<TService> Replace()
    {
        _replace = true;
        return this;
    }

    public IServiceRegistrationBuilder<TService> TryAdd()
    {
        _tryAdd = true;
        return this;
    }

    private void RegisterService()
    {
        ServiceDescriptor descriptor;

        if (_serviceKey != null)
        {
            // Keyed service registration
            if (_factory != null)
            {
                descriptor = ServiceDescriptor.DescribeKeyed(typeof(TService), _serviceKey, (sp, key) => _factory(sp), _lifetime);
            }
            else
            {
                descriptor = ServiceDescriptor.DescribeKeyed(typeof(TService), _serviceKey, _implementationType!, _lifetime);
            }
        }
        else
        {
            // Regular service registration
            if (_factory != null)
            {
                descriptor = ServiceDescriptor.Describe(typeof(TService), _factory, _lifetime);
            }
            else
            {
                descriptor = ServiceDescriptor.Describe(typeof(TService), _implementationType!, _lifetime);
            }
        }

        if (_replace)
        {
            _services.Replace(descriptor);
        }
        else if (_tryAdd)
        {
            _services.TryAdd(descriptor);
        }
        else
        {
            _services.Add(descriptor);
        }
    }
}