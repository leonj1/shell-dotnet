using Microsoft.Extensions.DependencyInjection;

namespace DotNetShell.Core.DependencyInjection;

/// <summary>
/// Attribute used to mark classes for automatic service registration.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class ServiceRegistrationAttribute : Attribute
{
    /// <summary>
    /// Gets the service type to register the class as.
    /// </summary>
    public Type? ServiceType { get; init; }

    /// <summary>
    /// Gets the service lifetime for registration.
    /// </summary>
    public ServiceLifetime Lifetime { get; init; } = ServiceLifetime.Transient;

    /// <summary>
    /// Gets the implementation factory method name.
    /// </summary>
    public string? FactoryMethod { get; init; }

    /// <summary>
    /// Gets a value indicating whether to register as multiple services.
    /// When true, the class will be registered for all implemented interfaces.
    /// </summary>
    public bool RegisterAsMultipleServices { get; init; }

    /// <summary>
    /// Gets a value indicating whether to replace existing registrations.
    /// </summary>
    public bool Replace { get; init; }

    /// <summary>
    /// Gets the service key for keyed services.
    /// </summary>
    public object? ServiceKey { get; init; }

    /// <summary>
    /// Gets additional service types to register the class for.
    /// </summary>
    public Type[]? AdditionalServiceTypes { get; init; }

    /// <summary>
    /// Gets the registration condition expression.
    /// </summary>
    public string? Condition { get; init; }

    /// <summary>
    /// Gets the priority for resolving conflicts when multiple implementations exist.
    /// Higher values take precedence.
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceRegistrationAttribute"/> class.
    /// </summary>
    public ServiceRegistrationAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceRegistrationAttribute"/> class.
    /// </summary>
    /// <param name="serviceType">The service type to register as.</param>
    public ServiceRegistrationAttribute(Type serviceType)
    {
        ServiceType = serviceType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceRegistrationAttribute"/> class.
    /// </summary>
    /// <param name="serviceType">The service type to register as.</param>
    /// <param name="lifetime">The service lifetime.</param>
    public ServiceRegistrationAttribute(Type serviceType, ServiceLifetime lifetime)
    {
        ServiceType = serviceType;
        Lifetime = lifetime;
    }
}

/// <summary>
/// Attribute used to mark service implementations as singletons.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class SingletonServiceAttribute : ServiceRegistrationAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SingletonServiceAttribute"/> class.
    /// </summary>
    public SingletonServiceAttribute() : base()
    {
        Lifetime = ServiceLifetime.Singleton;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SingletonServiceAttribute"/> class.
    /// </summary>
    /// <param name="serviceType">The service type to register as.</param>
    public SingletonServiceAttribute(Type serviceType) : base(serviceType, ServiceLifetime.Singleton)
    {
    }
}

/// <summary>
/// Attribute used to mark service implementations as scoped.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class ScopedServiceAttribute : ServiceRegistrationAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScopedServiceAttribute"/> class.
    /// </summary>
    public ScopedServiceAttribute() : base()
    {
        Lifetime = ServiceLifetime.Scoped;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ScopedServiceAttribute"/> class.
    /// </summary>
    /// <param name="serviceType">The service type to register as.</param>
    public ScopedServiceAttribute(Type serviceType) : base(serviceType, ServiceLifetime.Scoped)
    {
    }
}

/// <summary>
/// Attribute used to mark service implementations as transient.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class TransientServiceAttribute : ServiceRegistrationAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransientServiceAttribute"/> class.
    /// </summary>
    public TransientServiceAttribute() : base()
    {
        Lifetime = ServiceLifetime.Transient;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransientServiceAttribute"/> class.
    /// </summary>
    /// <param name="serviceType">The service type to register as.</param>
    public TransientServiceAttribute(Type serviceType) : base(serviceType, ServiceLifetime.Transient)
    {
    }
}

/// <summary>
/// Attribute used to exclude a class from automatic service registration.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class ExcludeFromServiceRegistrationAttribute : Attribute
{
    /// <summary>
    /// Gets the reason for exclusion.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExcludeFromServiceRegistrationAttribute"/> class.
    /// </summary>
    public ExcludeFromServiceRegistrationAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExcludeFromServiceRegistrationAttribute"/> class.
    /// </summary>
    /// <param name="reason">The reason for exclusion.</param>
    public ExcludeFromServiceRegistrationAttribute(string reason)
    {
        Reason = reason;
    }
}

/// <summary>
/// Attribute used to specify dependencies that should be injected into service constructors.
/// </summary>
[AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public class InjectAttribute : Attribute
{
    /// <summary>
    /// Gets the service key for keyed service injection.
    /// </summary>
    public object? ServiceKey { get; init; }

    /// <summary>
    /// Gets a value indicating whether the dependency is optional.
    /// </summary>
    public bool Optional { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InjectAttribute"/> class.
    /// </summary>
    public InjectAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InjectAttribute"/> class.
    /// </summary>
    /// <param name="serviceKey">The service key for keyed injection.</param>
    public InjectAttribute(object serviceKey)
    {
        ServiceKey = serviceKey;
    }
}