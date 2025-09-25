using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace DotNetShell.Core.DependencyInjection;

/// <summary>
/// Manages service lifetimes and disposal tracking for dependency injection containers.
/// </summary>
public class ServiceLifetimeManager : IDisposable, IAsyncDisposable
{
    private readonly ConcurrentBag<WeakReference> _disposableServices = new();
    private readonly ConcurrentBag<WeakReference> _asyncDisposableServices = new();
    private readonly ConcurrentDictionary<Type, ServiceLifetimeInfo> _serviceLifetimes = new();
    private readonly Timer _cleanupTimer;
    private readonly object _disposalLock = new();
    private bool _disposed;

    /// <summary>
    /// Gets the number of tracked disposable services.
    /// </summary>
    public int TrackedDisposableCount => CountAliveReferences(_disposableServices);

    /// <summary>
    /// Gets the number of tracked async disposable services.
    /// </summary>
    public int TrackedAsyncDisposableCount => CountAliveReferences(_asyncDisposableServices);

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceLifetimeManager"/> class.
    /// </summary>
    public ServiceLifetimeManager()
    {
        // Set up periodic cleanup of dead weak references
        _cleanupTimer = new Timer(CleanupDeadReferences, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Tracks a disposable service for automatic disposal.
    /// </summary>
    /// <param name="service">The service to track.</param>
    public void TrackDisposable(object service)
    {
        ArgumentNullException.ThrowIfNull(service);

        if (_disposed) return;

        if (service is IAsyncDisposable)
        {
            _asyncDisposableServices.Add(new WeakReference(service));
        }
        else if (service is IDisposable)
        {
            _disposableServices.Add(new WeakReference(service));
        }

        // Track service lifetime information
        var serviceType = service.GetType();
        _serviceLifetimes.AddOrUpdate(
            serviceType,
            _ => new ServiceLifetimeInfo(serviceType),
            (_, info) => { info.IncrementInstanceCount(); return info; });
    }

    /// <summary>
    /// Gets lifetime information for a service type.
    /// </summary>
    /// <param name="serviceType">The service type.</param>
    /// <returns>Service lifetime information, or null if not tracked.</returns>
    public ServiceLifetimeInfo? GetLifetimeInfo(Type serviceType)
    {
        _serviceLifetimes.TryGetValue(serviceType, out var info);
        return info;
    }

    /// <summary>
    /// Gets lifetime information for all tracked service types.
    /// </summary>
    /// <returns>A collection of service lifetime information.</returns>
    public IEnumerable<ServiceLifetimeInfo> GetAllLifetimeInfo()
    {
        return _serviceLifetimes.Values.ToArray();
    }

    /// <summary>
    /// Validates service lifetimes for potential issues.
    /// </summary>
    /// <param name="services">The service collection to validate.</param>
    /// <returns>Validation results.</returns>
    public ServiceLifetimeValidationResult ValidateServiceLifetimes(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var result = new ServiceLifetimeValidationResult();
        var serviceMap = BuildServiceMap(services);

        foreach (var service in services)
        {
            ValidateServiceLifetime(service, serviceMap, result);
        }

        return result;
    }

    /// <summary>
    /// Creates a custom service lifetime scope.
    /// </summary>
    /// <param name="scopeName">The scope name.</param>
    /// <returns>A custom lifetime scope.</returns>
    public IServiceLifetimeScope CreateCustomScope(string scopeName)
    {
        ArgumentNullException.ThrowIfNull(scopeName);
        ThrowIfDisposed();

        return new ServiceLifetimeScope(scopeName, this);
    }

    /// <summary>
    /// Disposes all tracked disposable services synchronously.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        lock (_disposalLock)
        {
            if (_disposed) return;

            _disposed = true;
            _cleanupTimer?.Dispose();

            // Dispose all tracked services
            DisposeTrackedServices();

            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Asynchronously disposes all tracked disposable services.
    /// </summary>
    /// <returns>A task representing the disposal operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        lock (_disposalLock)
        {
            if (_disposed) return;
            _disposed = true;
        }

        _cleanupTimer?.Dispose();

        // Dispose async disposables first
        await DisposeTrackedAsyncServices();

        // Then dispose regular disposables
        DisposeTrackedServices();

        GC.SuppressFinalize(this);
    }

    private void ValidateServiceLifetime(
        ServiceDescriptor service,
        Dictionary<Type, List<ServiceDescriptor>> serviceMap,
        ServiceLifetimeValidationResult result)
    {
        // Check for singleton services depending on shorter-lived services
        if (service.Lifetime == ServiceLifetime.Singleton && service.ImplementationType != null)
        {
            var dependencies = GetServiceDependencies(service.ImplementationType);
            foreach (var dependency in dependencies)
            {
                if (serviceMap.TryGetValue(dependency, out var dependencyServices))
                {
                    var shortLivedDeps = dependencyServices.Where(d => d.Lifetime != ServiceLifetime.Singleton);
                    foreach (var dep in shortLivedDeps)
                    {
                        result.AddWarning($"Singleton service '{service.ServiceType.Name}' depends on " +
                                        $"{dep.Lifetime.ToString().ToLower()} service '{dependency.Name}'. " +
                                        "This may cause issues with service disposal and lifetime management.");
                    }
                }
            }
        }

        // Check for circular dependencies (simplified check)
        CheckForCircularDependencies(service, serviceMap, result, new HashSet<Type>());
    }

    private void CheckForCircularDependencies(
        ServiceDescriptor service,
        Dictionary<Type, List<ServiceDescriptor>> serviceMap,
        ServiceLifetimeValidationResult result,
        HashSet<Type> visitedTypes)
    {
        if (service.ImplementationType == null) return;

        if (visitedTypes.Contains(service.ServiceType))
        {
            result.AddError($"Circular dependency detected involving service '{service.ServiceType.Name}'");
            return;
        }

        visitedTypes.Add(service.ServiceType);

        var dependencies = GetServiceDependencies(service.ImplementationType);
        foreach (var dependency in dependencies)
        {
            if (serviceMap.TryGetValue(dependency, out var dependencyServices))
            {
                foreach (var dep in dependencyServices)
                {
                    CheckForCircularDependencies(dep, serviceMap, result, new HashSet<Type>(visitedTypes));
                }
            }
        }
    }

    private static Dictionary<Type, List<ServiceDescriptor>> BuildServiceMap(IServiceCollection services)
    {
        var serviceMap = new Dictionary<Type, List<ServiceDescriptor>>();

        foreach (var service in services)
        {
            if (!serviceMap.ContainsKey(service.ServiceType))
            {
                serviceMap[service.ServiceType] = new List<ServiceDescriptor>();
            }
            serviceMap[service.ServiceType].Add(service);
        }

        return serviceMap;
    }

    private static IEnumerable<Type> GetServiceDependencies(Type implementationType)
    {
        var constructors = implementationType.GetConstructors();
        var dependencies = new List<Type>();

        foreach (var constructor in constructors)
        {
            var parameters = constructor.GetParameters();
            dependencies.AddRange(parameters.Select(p => p.ParameterType));
        }

        return dependencies.Distinct();
    }

    private void DisposeTrackedServices()
    {
        var exceptions = new List<Exception>();

        // Dispose regular disposables
        foreach (var weakRef in _disposableServices)
        {
            if (weakRef.Target is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        }

        if (exceptions.Count > 0)
        {
            throw new AggregateException("Errors occurred during service disposal", exceptions);
        }
    }

    private async ValueTask DisposeTrackedAsyncServices()
    {
        var exceptions = new List<Exception>();

        // Dispose async disposables
        foreach (var weakRef in _asyncDisposableServices)
        {
            if (weakRef.Target is IAsyncDisposable asyncDisposable)
            {
                try
                {
                    await asyncDisposable.DisposeAsync();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        }

        if (exceptions.Count > 0)
        {
            throw new AggregateException("Errors occurred during async service disposal", exceptions);
        }
    }

    private void CleanupDeadReferences(object? state)
    {
        if (_disposed) return;

        // This is a simple approach - in production you might want more sophisticated cleanup
        // For now, we just trigger GC to clean up dead references
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static int CountAliveReferences(ConcurrentBag<WeakReference> references)
    {
        return references.Count(wr => wr.IsAlive);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ServiceLifetimeManager));
        }
    }
}

/// <summary>
/// Contains information about service lifetime and usage.
/// </summary>
public class ServiceLifetimeInfo
{
    private int _instanceCount;
    private readonly object _lock = new();

    /// <summary>
    /// Gets the service type.
    /// </summary>
    public Type ServiceType { get; }

    /// <summary>
    /// Gets the number of instances created.
    /// </summary>
    public int InstanceCount
    {
        get
        {
            lock (_lock)
            {
                return _instanceCount;
            }
        }
    }

    /// <summary>
    /// Gets the first time an instance was created.
    /// </summary>
    public DateTime FirstInstanceCreated { get; private set; } = DateTime.MinValue;

    /// <summary>
    /// Gets the last time an instance was created.
    /// </summary>
    public DateTime LastInstanceCreated { get; private set; } = DateTime.MinValue;

    /// <summary>
    /// Gets additional metadata about the service.
    /// </summary>
    public Dictionary<string, object> Metadata { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceLifetimeInfo"/> class.
    /// </summary>
    /// <param name="serviceType">The service type.</param>
    public ServiceLifetimeInfo(Type serviceType)
    {
        ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
        FirstInstanceCreated = DateTime.UtcNow;
        LastInstanceCreated = DateTime.UtcNow;
    }

    /// <summary>
    /// Increments the instance count.
    /// </summary>
    internal void IncrementInstanceCount()
    {
        lock (_lock)
        {
            _instanceCount++;
            LastInstanceCreated = DateTime.UtcNow;
        }
    }
}

/// <summary>
/// Result of service lifetime validation.
/// </summary>
public class ServiceLifetimeValidationResult
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
    /// Gets a value indicating whether validation passed without errors.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

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
}

/// <summary>
/// Interface for custom service lifetime scopes.
/// </summary>
public interface IServiceLifetimeScope : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets the scope name.
    /// </summary>
    string ScopeName { get; }

    /// <summary>
    /// Gets the scope creation time.
    /// </summary>
    DateTime CreatedAt { get; }

    /// <summary>
    /// Tracks a service within this scope.
    /// </summary>
    /// <param name="service">The service to track.</param>
    void TrackService(object service);
}

/// <summary>
/// Implementation of custom service lifetime scope.
/// </summary>
internal class ServiceLifetimeScope : IServiceLifetimeScope
{
    private readonly ServiceLifetimeManager _manager;
    private readonly List<WeakReference> _scopedServices = new();
    private bool _disposed;

    /// <summary>
    /// Gets the scope name.
    /// </summary>
    public string ScopeName { get; }

    /// <summary>
    /// Gets the scope creation time.
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceLifetimeScope"/> class.
    /// </summary>
    /// <param name="scopeName">The scope name.</param>
    /// <param name="manager">The lifetime manager.</param>
    public ServiceLifetimeScope(string scopeName, ServiceLifetimeManager manager)
    {
        ScopeName = scopeName ?? throw new ArgumentNullException(nameof(scopeName));
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Tracks a service within this scope.
    /// </summary>
    /// <param name="service">The service to track.</param>
    public void TrackService(object service)
    {
        ArgumentNullException.ThrowIfNull(service);
        ThrowIfDisposed();

        _scopedServices.Add(new WeakReference(service));
        _manager.TrackDisposable(service);
    }

    /// <summary>
    /// Disposes all services tracked in this scope.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        var exceptions = new List<Exception>();

        foreach (var weakRef in _scopedServices)
        {
            if (weakRef.Target is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        }

        if (exceptions.Count > 0)
        {
            throw new AggregateException($"Errors occurred disposing services in scope '{ScopeName}'", exceptions);
        }
    }

    /// <summary>
    /// Asynchronously disposes all services tracked in this scope.
    /// </summary>
    /// <returns>A task representing the disposal operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        var exceptions = new List<Exception>();

        foreach (var weakRef in _scopedServices)
        {
            if (weakRef.Target is IAsyncDisposable asyncDisposable)
            {
                try
                {
                    await asyncDisposable.DisposeAsync();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
            else if (weakRef.Target is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        }

        if (exceptions.Count > 0)
        {
            throw new AggregateException($"Errors occurred disposing services in scope '{ScopeName}'", exceptions);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException($"ServiceLifetimeScope '{ScopeName}'");
        }
    }
}

/// <summary>
/// Builder for configuring service lifetime management.
/// </summary>
public class ServiceLifetimeManagerBuilder
{
    private readonly IServiceCollection _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceLifetimeManagerBuilder"/> class.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public ServiceLifetimeManagerBuilder(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// Adds automatic disposal tracking for all disposable services.
    /// </summary>
    /// <returns>This builder instance.</returns>
    public ServiceLifetimeManagerBuilder EnableDisposalTracking()
    {
        _services.AddSingleton<ServiceLifetimeManager>();
        return this;
    }

    /// <summary>
    /// Enables service lifetime validation.
    /// </summary>
    /// <returns>This builder instance.</returns>
    public ServiceLifetimeManagerBuilder EnableLifetimeValidation()
    {
        // Register validation as a hosted service or startup task
        _services.AddSingleton<ServiceLifetimeValidator>();
        return this;
    }

    /// <summary>
    /// Configures memory leak detection.
    /// </summary>
    /// <param name="configure">Configuration action.</param>
    /// <returns>This builder instance.</returns>
    public ServiceLifetimeManagerBuilder ConfigureMemoryLeakDetection(Action<MemoryLeakDetectionOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new MemoryLeakDetectionOptions();
        configure(options);

        _services.AddSingleton(options);
        _services.AddSingleton<MemoryLeakDetector>();
        return this;
    }
}

/// <summary>
/// Service for validating service lifetimes at startup.
/// </summary>
public class ServiceLifetimeValidator
{
    private readonly ServiceLifetimeManager _lifetimeManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceLifetimeValidator"/> class.
    /// </summary>
    /// <param name="lifetimeManager">The lifetime manager.</param>
    public ServiceLifetimeValidator(ServiceLifetimeManager lifetimeManager)
    {
        _lifetimeManager = lifetimeManager ?? throw new ArgumentNullException(nameof(lifetimeManager));
    }

    /// <summary>
    /// Validates service registrations.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>Validation results.</returns>
    public ServiceLifetimeValidationResult ValidateServices(IServiceCollection services)
    {
        return _lifetimeManager.ValidateServiceLifetimes(services);
    }
}

/// <summary>
/// Options for memory leak detection.
/// </summary>
public class MemoryLeakDetectionOptions
{
    /// <summary>
    /// Gets or sets the interval for memory checks.
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the threshold for memory usage warnings.
    /// </summary>
    public long MemoryThresholdBytes { get; set; } = 500 * 1024 * 1024; // 500MB

    /// <summary>
    /// Gets or sets whether to automatically trigger GC.
    /// </summary>
    public bool AutoTriggerGC { get; set; } = true;
}

/// <summary>
/// Detector for potential memory leaks.
/// </summary>
public class MemoryLeakDetector : IDisposable
{
    private readonly MemoryLeakDetectionOptions _options;
    private readonly Timer _timer;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryLeakDetector"/> class.
    /// </summary>
    /// <param name="options">The detection options.</param>
    public MemoryLeakDetector(MemoryLeakDetectionOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _timer = new Timer(CheckMemoryUsage, null, _options.CheckInterval, _options.CheckInterval);
    }

    private void CheckMemoryUsage(object? state)
    {
        if (_disposed) return;

        try
        {
            var memoryUsage = GC.GetTotalMemory(false);

            if (memoryUsage > _options.MemoryThresholdBytes)
            {
                // Log warning
                Debug.WriteLine($"High memory usage detected: {memoryUsage / 1024 / 1024}MB");

                if (_options.AutoTriggerGC)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during memory check: {ex.Message}");
        }
    }

    /// <summary>
    /// Disposes the memory leak detector.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer?.Dispose();
    }
}