using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace DotNetShell.Core.DependencyInjection;

/// <summary>
/// Service provider that provides isolated dependency injection containers for modules.
/// Ensures module isolation while allowing controlled access to shell infrastructure services.
/// </summary>
public class ModuleServiceProvider : IServiceProvider, IServiceScope, IAsyncDisposable, IDisposable
{
    private readonly string _moduleId;
    private readonly IServiceProvider _shellProvider;
    private readonly HierarchicalServiceProvider _hierarchicalProvider;
    private readonly ModuleIsolationPolicy _isolationPolicy;
    private readonly ConcurrentDictionary<Type, ServiceResolutionResult> _resolutionCache;
    private readonly ServiceAccessLogger _accessLogger;
    private readonly object _lockObject = new();
    private bool _disposed;

    /// <summary>
    /// Gets the module identifier.
    /// </summary>
    public string ModuleId => _moduleId;

    /// <summary>
    /// Gets the shell service provider.
    /// </summary>
    public IServiceProvider ShellProvider => _shellProvider;

    /// <summary>
    /// Gets the service provider for this scope.
    /// </summary>
    public IServiceProvider ServiceProvider => this;

    /// <summary>
    /// Gets the module isolation policy.
    /// </summary>
    public ModuleIsolationPolicy IsolationPolicy => _isolationPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleServiceProvider"/> class.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="shellProvider">The shell service provider.</param>
    /// <param name="moduleServices">The module service collection.</param>
    /// <param name="isolationPolicy">The module isolation policy.</param>
    public ModuleServiceProvider(
        string moduleId,
        IServiceProvider shellProvider,
        IServiceCollection moduleServices,
        ModuleIsolationPolicy? isolationPolicy = null)
    {
        _moduleId = moduleId ?? throw new ArgumentNullException(nameof(moduleId));
        _shellProvider = shellProvider ?? throw new ArgumentNullException(nameof(shellProvider));
        ArgumentNullException.ThrowIfNull(moduleServices);

        _isolationPolicy = isolationPolicy ?? ModuleIsolationPolicy.Default;
        _resolutionCache = new ConcurrentDictionary<Type, ServiceResolutionResult>();
        _accessLogger = new ServiceAccessLogger(moduleId);

        // Create hierarchical provider with shell as parent
        _hierarchicalProvider = new HierarchicalServiceProvider(shellProvider, moduleServices);

        // Register module-specific services
        RegisterModuleServices(moduleServices);
    }

    /// <summary>
    /// Gets a service of the specified type with isolation enforcement.
    /// </summary>
    /// <param name="serviceType">The type of service to retrieve.</param>
    /// <returns>The service instance, or null if not found or access denied.</returns>
    public object? GetService(Type serviceType)
    {
        ThrowIfDisposed();

        // Check resolution cache first
        if (_resolutionCache.TryGetValue(serviceType, out var cachedResult))
        {
            if (cachedResult.IsAllowed)
            {
                _accessLogger.LogAccess(serviceType, ServiceAccessResult.Allowed, "Cached");
                return cachedResult.Service;
            }
            else
            {
                _accessLogger.LogAccess(serviceType, ServiceAccessResult.Denied, cachedResult.DenialReason);
                return null;
            }
        }

        try
        {
            // Check if service access is allowed by isolation policy
            var accessCheck = _isolationPolicy.CheckServiceAccess(_moduleId, serviceType);
            if (!accessCheck.IsAllowed)
            {
                var result = new ServiceResolutionResult(null, false, accessCheck.Reason);
                _resolutionCache.TryAdd(serviceType, result);
                _accessLogger.LogAccess(serviceType, ServiceAccessResult.Denied, accessCheck.Reason);
                return null;
            }

            // Resolve service through hierarchical provider
            var service = _hierarchicalProvider.GetService(serviceType);
            var resolutionResult = new ServiceResolutionResult(service, true, null);
            _resolutionCache.TryAdd(serviceType, resolutionResult);

            if (service != null)
            {
                _accessLogger.LogAccess(serviceType, ServiceAccessResult.Allowed, "Resolved");
            }

            return service;
        }
        catch (Exception ex)
        {
            _accessLogger.LogAccess(serviceType, ServiceAccessResult.Error, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Gets a required service of the specified type.
    /// </summary>
    /// <param name="serviceType">The type of service to retrieve.</param>
    /// <returns>The service instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the service is not found or access is denied.</exception>
    public object GetRequiredService(Type serviceType)
    {
        var service = GetService(serviceType);
        if (service == null)
        {
            var accessCheck = _isolationPolicy.CheckServiceAccess(_moduleId, serviceType);
            if (!accessCheck.IsAllowed)
            {
                throw new ServiceAccessDeniedException(_moduleId, serviceType, accessCheck.Reason);
            }

            throw new InvalidOperationException($"Unable to resolve required service for type '{serviceType.Name}' in module '{_moduleId}'.");
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
    /// Gets all services of the specified type that are accessible to this module.
    /// </summary>
    /// <typeparam name="T">The type of services to retrieve.</typeparam>
    /// <returns>A collection of accessible service instances.</returns>
    public IEnumerable<T> GetServices<T>()
    {
        var services = new List<T>();
        var serviceType = typeof(T);

        // Check access permission
        var accessCheck = _isolationPolicy.CheckServiceAccess(_moduleId, serviceType);
        if (!accessCheck.IsAllowed)
        {
            _accessLogger.LogAccess(serviceType, ServiceAccessResult.Denied, accessCheck.Reason);
            return services;
        }

        try
        {
            var resolvedServices = _hierarchicalProvider.GetServices<T>();
            services.AddRange(resolvedServices);

            _accessLogger.LogAccess(serviceType, ServiceAccessResult.Allowed, $"Retrieved {services.Count} services");
        }
        catch (Exception ex)
        {
            _accessLogger.LogAccess(serviceType, ServiceAccessResult.Error, ex.Message);
        }

        return services;
    }

    /// <summary>
    /// Creates a new scope within this module's service provider.
    /// </summary>
    /// <returns>A new service scope.</returns>
    public IServiceScope CreateScope()
    {
        ThrowIfDisposed();

        var childScope = _hierarchicalProvider.CreateScope();
        return new ModuleServiceScope(_moduleId, childScope, _isolationPolicy);
    }

    /// <summary>
    /// Gets module-specific service statistics.
    /// </summary>
    /// <returns>Service access statistics.</returns>
    public ModuleServiceStatistics GetStatistics()
    {
        ThrowIfDisposed();

        return _accessLogger.GetStatistics();
    }

    /// <summary>
    /// Clears the service resolution cache.
    /// </summary>
    public void ClearCache()
    {
        ThrowIfDisposed();

        _resolutionCache.Clear();
    }

    /// <summary>
    /// Checks if a service type is accessible to this module.
    /// </summary>
    /// <param name="serviceType">The service type to check.</param>
    /// <returns>True if accessible; otherwise, false.</returns>
    public bool IsServiceAccessible(Type serviceType)
    {
        ThrowIfDisposed();

        var accessCheck = _isolationPolicy.CheckServiceAccess(_moduleId, serviceType);
        return accessCheck.IsAllowed;
    }

    /// <summary>
    /// Gets the list of accessible service types.
    /// </summary>
    /// <returns>A collection of accessible service types.</returns>
    public IEnumerable<Type> GetAccessibleServiceTypes()
    {
        ThrowIfDisposed();

        return _isolationPolicy.GetAccessibleServiceTypes(_moduleId);
    }

    private void RegisterModuleServices(IServiceCollection moduleServices)
    {
        // Add module-specific services
        moduleServices.AddSingleton<IModuleServiceScope>(provider => new ModuleServiceScope(_moduleId, this, _isolationPolicy));
        moduleServices.AddSingleton<ModuleServiceContext>(provider => new ModuleServiceContext(_moduleId, this));
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException($"ModuleServiceProvider for module '{_moduleId}'");
        }
    }

    /// <summary>
    /// Disposes the module service provider and all managed resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        lock (_lockObject)
        {
            if (_disposed) return;

            try
            {
                _hierarchicalProvider?.Dispose();
                _resolutionCache.Clear();
            }
            catch (Exception ex)
            {
                // Log disposal errors
                System.Diagnostics.Debug.WriteLine($"Error disposing ModuleServiceProvider for module '{_moduleId}': {ex.Message}");
            }
            finally
            {
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Asynchronously disposes the module service provider.
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
            if (_hierarchicalProvider != null)
            {
                await _hierarchicalProvider.DisposeAsync();
            }
            _resolutionCache.Clear();
        }
        catch (Exception ex)
        {
            // Log disposal errors
            System.Diagnostics.Debug.WriteLine($"Error disposing ModuleServiceProvider for module '{_moduleId}': {ex.Message}");
        }
    }
}

/// <summary>
/// Service scope implementation for module isolation.
/// </summary>
public class ModuleServiceScope : IServiceScope, IModuleServiceScope, IAsyncDisposable
{
    private readonly string _moduleId;
    private readonly IServiceScope _innerScope;
    private readonly ModuleIsolationPolicy _isolationPolicy;
    private ModuleServiceProvider? _moduleProvider;

    /// <summary>
    /// Gets the module identifier.
    /// </summary>
    public string ModuleId => _moduleId;

    /// <summary>
    /// Gets the service provider for this scope.
    /// </summary>
    public IServiceProvider ServiceProvider => _moduleProvider ??=
        new ModuleServiceProvider(_moduleId, _innerScope.ServiceProvider, new ServiceCollection(), _isolationPolicy);

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleServiceScope"/> class.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="innerScope">The inner service scope.</param>
    /// <param name="isolationPolicy">The isolation policy.</param>
    public ModuleServiceScope(string moduleId, IServiceScope innerScope, ModuleIsolationPolicy isolationPolicy)
    {
        _moduleId = moduleId ?? throw new ArgumentNullException(nameof(moduleId));
        _innerScope = innerScope ?? throw new ArgumentNullException(nameof(innerScope));
        _isolationPolicy = isolationPolicy ?? throw new ArgumentNullException(nameof(isolationPolicy));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleServiceScope"/> class.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="isolationPolicy">The isolation policy.</param>
    public ModuleServiceScope(string moduleId, IServiceProvider serviceProvider, ModuleIsolationPolicy isolationPolicy)
    {
        _moduleId = moduleId ?? throw new ArgumentNullException(nameof(moduleId));
        _isolationPolicy = isolationPolicy ?? throw new ArgumentNullException(nameof(isolationPolicy));

        _innerScope = new ServiceScope(serviceProvider);
    }

    /// <summary>
    /// Disposes the service scope.
    /// </summary>
    public void Dispose()
    {
        _moduleProvider?.Dispose();
        _innerScope.Dispose();
    }

    /// <summary>
    /// Asynchronously disposes the service scope.
    /// </summary>
    /// <returns>A task representing the disposal operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_moduleProvider != null)
        {
            await _moduleProvider.DisposeAsync();
        }

        if (_innerScope is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            _innerScope.Dispose();
        }
    }

    private class ServiceScope : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; }

        public ServiceScope(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public void Dispose()
        {
            if (ServiceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}

/// <summary>
/// Interface for module-specific service scopes.
/// </summary>
public interface IModuleServiceScope : IServiceScope
{
    /// <summary>
    /// Gets the module identifier.
    /// </summary>
    string ModuleId { get; }
}

/// <summary>
/// Factory for creating module service providers.
/// </summary>
public class ModuleServiceProviderFactory
{
    private readonly ConcurrentDictionary<string, ModuleServiceProvider> _moduleProviders = new();
    private readonly ModuleIsolationPolicy _defaultIsolationPolicy = ModuleIsolationPolicy.Default;

    /// <summary>
    /// Creates a module service provider.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="shellProvider">The shell service provider.</param>
    /// <param name="moduleServices">The module services.</param>
    /// <returns>A module service provider.</returns>
    public ModuleServiceProvider CreateModuleProvider(
        string moduleId,
        IServiceProvider shellProvider,
        IServiceCollection moduleServices)
    {
        return CreateModuleProvider(moduleId, shellProvider, moduleServices, null);
    }

    /// <summary>
    /// Creates a module service provider with custom isolation policy.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="shellProvider">The shell service provider.</param>
    /// <param name="moduleServices">The module services.</param>
    /// <param name="isolationPolicy">The isolation policy.</param>
    /// <returns>A module service provider.</returns>
    public ModuleServiceProvider CreateModuleProvider(
        string moduleId,
        IServiceProvider shellProvider,
        IServiceCollection moduleServices,
        ModuleIsolationPolicy? isolationPolicy)
    {
        var provider = new ModuleServiceProvider(
            moduleId,
            shellProvider,
            moduleServices,
            isolationPolicy ?? _defaultIsolationPolicy);

        _moduleProviders.TryAdd(moduleId, provider);
        return provider;
    }

    /// <summary>
    /// Gets a module service provider by identifier.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <returns>The module service provider, or null if not found.</returns>
    public ModuleServiceProvider? GetModuleProvider(string moduleId)
    {
        _moduleProviders.TryGetValue(moduleId, out var provider);
        return provider;
    }

    /// <summary>
    /// Removes and disposes a module service provider.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <returns>True if removed; otherwise, false.</returns>
    public bool RemoveModuleProvider(string moduleId)
    {
        if (_moduleProviders.TryRemove(moduleId, out var provider))
        {
            provider.Dispose();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets all registered module identifiers.
    /// </summary>
    /// <returns>A collection of module identifiers.</returns>
    public IEnumerable<string> GetRegisteredModules()
    {
        return _moduleProviders.Keys.ToArray();
    }
}

/// <summary>
/// Contains context information for module services.
/// </summary>
public class ModuleServiceContext
{
    /// <summary>
    /// Gets the module identifier.
    /// </summary>
    public string ModuleId { get; }

    /// <summary>
    /// Gets the module service provider.
    /// </summary>
    public ModuleServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleServiceContext"/> class.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="serviceProvider">The module service provider.</param>
    public ModuleServiceContext(string moduleId, ModuleServiceProvider serviceProvider)
    {
        ModuleId = moduleId ?? throw new ArgumentNullException(nameof(moduleId));
        ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        CreatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Result of service resolution attempts.
/// </summary>
internal class ServiceResolutionResult
{
    public object? Service { get; }
    public bool IsAllowed { get; }
    public string? DenialReason { get; }

    public ServiceResolutionResult(object? service, bool isAllowed, string? denialReason)
    {
        Service = service;
        IsAllowed = isAllowed;
        DenialReason = denialReason;
    }
}