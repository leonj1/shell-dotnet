using Microsoft.Extensions.DependencyInjection;

namespace DotNetShell.Core.DependencyInjection;

/// <summary>
/// Service provider that implements hierarchical dependency injection with parent-child relationships.
/// Child containers can resolve services from parent containers while maintaining isolation.
/// </summary>
public class HierarchicalServiceProvider : IServiceProvider, IServiceScope, IAsyncDisposable, IDisposable
{
    private readonly IServiceProvider _parentProvider;
    private readonly IServiceProvider _childProvider;
    private readonly IServiceScope _childScope;
    private readonly object _lockObject = new();
    private readonly HashSet<Type> _childServiceTypes;
    private readonly ServiceLifetimeManager _lifetimeManager;
    private bool _disposed;

    /// <summary>
    /// Gets the parent service provider.
    /// </summary>
    public IServiceProvider ParentProvider => _parentProvider;

    /// <summary>
    /// Gets the child service provider.
    /// </summary>
    public IServiceProvider ChildProvider => _childProvider;

    /// <summary>
    /// Gets the service provider for this scope.
    /// </summary>
    public IServiceProvider ServiceProvider => this;

    /// <summary>
    /// Initializes a new instance of the <see cref="HierarchicalServiceProvider"/> class.
    /// </summary>
    /// <param name="parentProvider">The parent service provider.</param>
    /// <param name="childServices">The child service collection.</param>
    public HierarchicalServiceProvider(IServiceProvider parentProvider, IServiceCollection childServices)
    {
        _parentProvider = parentProvider ?? throw new ArgumentNullException(nameof(parentProvider));
        ArgumentNullException.ThrowIfNull(childServices);

        // Track child service types for resolution optimization
        _childServiceTypes = new HashSet<Type>(childServices.Select(s => s.ServiceType));

        // Add parent provider as a service in child container for potential injection
        var childServicesWithParent = new ServiceCollection();
        foreach (var service in childServices)
        {
            childServicesWithParent.Add(service);
        }
        childServicesWithParent.AddSingleton(_parentProvider);

        // Build child provider
        _childScope = childServicesWithParent.BuildServiceProvider().CreateScope();
        _childProvider = _childScope.ServiceProvider;

        // Initialize lifetime manager
        _lifetimeManager = new ServiceLifetimeManager();
    }

    /// <summary>
    /// Gets a service of the specified type.
    /// First tries to resolve from child container, then falls back to parent.
    /// </summary>
    /// <param name="serviceType">The type of service to retrieve.</param>
    /// <returns>The service instance, or null if not found.</returns>
    public object? GetService(Type serviceType)
    {
        ThrowIfDisposed();

        try
        {
            // First try to resolve from child container for better performance
            // if we know the service is registered there
            if (_childServiceTypes.Contains(serviceType))
            {
                var childService = _childProvider.GetService(serviceType);
                if (childService != null)
                {
                    TrackService(childService);
                    return childService;
                }
            }

            // Try child provider first for comprehensive search
            var service = _childProvider.GetService(serviceType);
            if (service != null)
            {
                TrackService(service);
                return service;
            }

            // Fall back to parent provider
            service = _parentProvider.GetService(serviceType);
            if (service != null)
            {
                // Don't track parent services as they're managed by parent container
                return service;
            }

            return null;
        }
        catch (Exception ex)
        {
            // Log exception and continue to parent
            // In a real implementation, you'd use proper logging
            System.Diagnostics.Debug.WriteLine($"Error resolving service {serviceType.Name}: {ex.Message}");

            // Try parent as fallback
            return _parentProvider.GetService(serviceType);
        }
    }

    /// <summary>
    /// Gets a required service of the specified type.
    /// </summary>
    /// <param name="serviceType">The type of service to retrieve.</param>
    /// <returns>The service instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the service is not found.</exception>
    public object GetRequiredService(Type serviceType)
    {
        var service = GetService(serviceType);
        if (service == null)
        {
            throw new InvalidOperationException($"Unable to resolve service for type '{serviceType.Name}'.");
        }
        return service;
    }

    /// <summary>
    /// Gets a service of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of service to retrieve.</typeparam>
    /// <returns>The service instance, or default if not found.</returns>
    public T? GetService<T>()
    {
        return (T?)GetService(typeof(T));
    }

    /// <summary>
    /// Gets a required service of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of service to retrieve.</typeparam>
    /// <returns>The service instance.</returns>
    public T GetRequiredService<T>() where T : notnull
    {
        return (T)GetRequiredService(typeof(T));
    }

    /// <summary>
    /// Gets all services of the specified type from both child and parent providers.
    /// </summary>
    /// <typeparam name="T">The type of services to retrieve.</typeparam>
    /// <returns>A collection of service instances.</returns>
    public IEnumerable<T> GetServices<T>()
    {
        var services = new List<T>();

        // Get services from child provider
        var childServices = _childProvider.GetServices<T>();
        services.AddRange(childServices);

        // Get services from parent provider
        var parentServices = _parentProvider.GetServices<T>();
        services.AddRange(parentServices);

        // Track child services
        foreach (var service in childServices)
        {
            if (service != null)
            {
                TrackService(service);
            }
        }

        return services.Distinct();
    }

    /// <summary>
    /// Creates a new scope that inherits from this hierarchical provider.
    /// </summary>
    /// <returns>A new service scope.</returns>
    public IServiceScope CreateScope()
    {
        ThrowIfDisposed();

        // Create a child scope from the child provider
        var childScope = _childProvider.CreateScope();

        // Create a new hierarchical provider with the scoped child provider
        return new HierarchicalServiceScope(_parentProvider, childScope);
    }

    /// <summary>
    /// Checks if a service type is registered in this provider hierarchy.
    /// </summary>
    /// <param name="serviceType">The service type to check.</param>
    /// <returns>True if the service is registered; otherwise, false.</returns>
    public bool IsServiceRegistered(Type serviceType)
    {
        ThrowIfDisposed();

        // Check child provider first
        if (_childProvider.GetService(serviceType) != null)
        {
            return true;
        }

        // Check parent provider
        return _parentProvider.GetService(serviceType) != null;
    }

    /// <summary>
    /// Gets the service resolution path for debugging purposes.
    /// </summary>
    /// <param name="serviceType">The service type.</param>
    /// <returns>The resolution path information.</returns>
    public ServiceResolutionPath GetResolutionPath(Type serviceType)
    {
        ThrowIfDisposed();

        var path = new ServiceResolutionPath(serviceType);

        // Check child provider
        var childService = _childProvider.GetService(serviceType);
        if (childService != null)
        {
            path.ResolvedFrom = "Child";
            path.ActualType = childService.GetType();
            return path;
        }

        // Check parent provider
        var parentService = _parentProvider.GetService(serviceType);
        if (parentService != null)
        {
            path.ResolvedFrom = "Parent";
            path.ActualType = parentService.GetType();
            return path;
        }

        path.ResolvedFrom = "None";
        return path;
    }

    private void TrackService(object service)
    {
        if (service is IDisposable || service is IAsyncDisposable)
        {
            _lifetimeManager.TrackDisposable(service);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(HierarchicalServiceProvider));
        }
    }

    /// <summary>
    /// Disposes the service provider and all tracked resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        lock (_lockObject)
        {
            if (_disposed) return;

            try
            {
                _lifetimeManager.Dispose();
                _childScope.Dispose();
            }
            catch (Exception ex)
            {
                // Log disposal errors but don't throw
                System.Diagnostics.Debug.WriteLine($"Error during disposal: {ex.Message}");
            }
            finally
            {
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Asynchronously disposes the service provider and all tracked resources.
    /// </summary>
    /// <returns>A task representing the disposal operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        lock (_lockObject)
        {
            if (_disposed) return;
            _disposed = true;
        }

        try
        {
            await _lifetimeManager.DisposeAsync();

            if (_childScope is IAsyncDisposable asyncDisposableScope)
            {
                await asyncDisposableScope.DisposeAsync();
            }
            else
            {
                _childScope.Dispose();
            }
        }
        catch (Exception ex)
        {
            // Log disposal errors but don't throw
            System.Diagnostics.Debug.WriteLine($"Error during async disposal: {ex.Message}");
        }
    }
}

/// <summary>
/// Service scope implementation for hierarchical service providers.
/// </summary>
internal class HierarchicalServiceScope : IServiceScope, IAsyncDisposable
{
    private readonly IServiceProvider _parentProvider;
    private readonly IServiceScope _childScope;
    private HierarchicalServiceProvider? _hierarchicalProvider;

    public IServiceProvider ServiceProvider => _hierarchicalProvider ??=
        new HierarchicalServiceProvider(_parentProvider, new ServiceCollection());

    public HierarchicalServiceScope(IServiceProvider parentProvider, IServiceScope childScope)
    {
        _parentProvider = parentProvider ?? throw new ArgumentNullException(nameof(parentProvider));
        _childScope = childScope ?? throw new ArgumentNullException(nameof(childScope));
    }

    public void Dispose()
    {
        _hierarchicalProvider?.Dispose();
        _childScope.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_hierarchicalProvider != null)
        {
            await _hierarchicalProvider.DisposeAsync();
        }

        if (_childScope is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            _childScope.Dispose();
        }
    }
}

/// <summary>
/// Contains information about service resolution for debugging.
/// </summary>
public class ServiceResolutionPath
{
    /// <summary>
    /// Gets the requested service type.
    /// </summary>
    public Type RequestedType { get; }

    /// <summary>
    /// Gets where the service was resolved from.
    /// </summary>
    public string ResolvedFrom { get; set; } = string.Empty;

    /// <summary>
    /// Gets the actual type of the resolved service.
    /// </summary>
    public Type? ActualType { get; set; }

    /// <summary>
    /// Gets the resolution timestamp.
    /// </summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceResolutionPath"/> class.
    /// </summary>
    /// <param name="requestedType">The requested service type.</param>
    public ServiceResolutionPath(Type requestedType)
    {
        RequestedType = requestedType ?? throw new ArgumentNullException(nameof(requestedType));
    }

    /// <summary>
    /// Returns a string representation of the resolution path.
    /// </summary>
    /// <returns>String representation.</returns>
    public override string ToString()
    {
        return $"{RequestedType.Name} -> {ResolvedFrom} ({ActualType?.Name ?? "Not resolved"})";
    }
}

/// <summary>
/// Factory for creating hierarchical service providers.
/// </summary>
public class HierarchicalServiceProviderFactory
{
    /// <summary>
    /// Creates a hierarchical service provider.
    /// </summary>
    /// <param name="parentProvider">The parent service provider.</param>
    /// <param name="childServices">The child service collection.</param>
    /// <returns>A new hierarchical service provider.</returns>
    public HierarchicalServiceProvider Create(IServiceProvider parentProvider, IServiceCollection childServices)
    {
        return new HierarchicalServiceProvider(parentProvider, childServices);
    }

    /// <summary>
    /// Creates a hierarchical service provider with configuration.
    /// </summary>
    /// <param name="parentProvider">The parent service provider.</param>
    /// <param name="configure">Configuration action for child services.</param>
    /// <returns>A new hierarchical service provider.</returns>
    public HierarchicalServiceProvider Create(IServiceProvider parentProvider, Action<IServiceCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var childServices = new ServiceCollection();
        configure(childServices);

        return new HierarchicalServiceProvider(parentProvider, childServices);
    }
}