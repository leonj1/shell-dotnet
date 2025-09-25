using System.Collections.Concurrent;

namespace DotNetShell.Core.DependencyInjection;

/// <summary>
/// Defines policies for module service isolation and access control.
/// </summary>
public class ModuleIsolationPolicy
{
    private readonly ConcurrentDictionary<string, HashSet<Type>> _moduleAllowedServices = new();
    private readonly ConcurrentDictionary<Type, ServiceAccessLevel> _serviceAccessLevels = new();
    private readonly HashSet<Type> _globallyAccessibleServices = new();
    private readonly HashSet<string> _trustedModules = new();

    /// <summary>
    /// Gets the default isolation policy.
    /// </summary>
    public static ModuleIsolationPolicy Default { get; } = CreateDefaultPolicy();

    /// <summary>
    /// Gets or sets the default access level for unspecified services.
    /// </summary>
    public ServiceAccessLevel DefaultAccessLevel { get; set; } = ServiceAccessLevel.ModuleOnly;

    /// <summary>
    /// Gets or sets whether to allow access to framework services.
    /// </summary>
    public bool AllowFrameworkServices { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to allow access to logging services.
    /// </summary>
    public bool AllowLoggingServices { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to allow access to configuration services.
    /// </summary>
    public bool AllowConfigurationServices { get; set; } = true;

    /// <summary>
    /// Checks if a module can access a specific service type.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="serviceType">The service type.</param>
    /// <returns>Access check result.</returns>
    public ServiceAccessCheckResult CheckServiceAccess(string moduleId, Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        ArgumentNullException.ThrowIfNull(serviceType);

        // Check if service is globally accessible
        if (_globallyAccessibleServices.Contains(serviceType))
        {
            return ServiceAccessCheckResult.Allow();
        }

        // Check if module is trusted (bypass restrictions)
        if (_trustedModules.Contains(moduleId))
        {
            return ServiceAccessCheckResult.Allow($"Trusted module: {moduleId}");
        }

        // Check framework services
        if (AllowFrameworkServices && IsFrameworkService(serviceType))
        {
            return ServiceAccessCheckResult.Allow("Framework service");
        }

        // Check logging services
        if (AllowLoggingServices && IsLoggingService(serviceType))
        {
            return ServiceAccessCheckResult.Allow("Logging service");
        }

        // Check configuration services
        if (AllowConfigurationServices && IsConfigurationService(serviceType))
        {
            return ServiceAccessCheckResult.Allow("Configuration service");
        }

        // Check service-specific access level
        if (_serviceAccessLevels.TryGetValue(serviceType, out var accessLevel))
        {
            return CheckAccessLevel(moduleId, serviceType, accessLevel);
        }

        // Check module-specific allowed services
        if (_moduleAllowedServices.TryGetValue(moduleId, out var allowedServices) &&
            allowedServices.Contains(serviceType))
        {
            return ServiceAccessCheckResult.Allow($"Explicitly allowed for module: {moduleId}");
        }

        // Apply default access level
        return CheckAccessLevel(moduleId, serviceType, DefaultAccessLevel);
    }

    /// <summary>
    /// Allows a module to access specific service types.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="serviceTypes">The service types to allow.</param>
    /// <returns>This policy instance for chaining.</returns>
    public ModuleIsolationPolicy AllowServicesForModule(string moduleId, params Type[] serviceTypes)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        ArgumentNullException.ThrowIfNull(serviceTypes);

        var allowedServices = _moduleAllowedServices.GetOrAdd(moduleId, _ => new HashSet<Type>());

        lock (allowedServices)
        {
            foreach (var serviceType in serviceTypes)
            {
                allowedServices.Add(serviceType);
            }
        }

        return this;
    }

    /// <summary>
    /// Sets the access level for specific service types.
    /// </summary>
    /// <param name="accessLevel">The access level.</param>
    /// <param name="serviceTypes">The service types.</param>
    /// <returns>This policy instance for chaining.</returns>
    public ModuleIsolationPolicy SetServiceAccessLevel(ServiceAccessLevel accessLevel, params Type[] serviceTypes)
    {
        ArgumentNullException.ThrowIfNull(serviceTypes);

        foreach (var serviceType in serviceTypes)
        {
            _serviceAccessLevels.AddOrUpdate(serviceType, accessLevel, (_, _) => accessLevel);
        }

        return this;
    }

    /// <summary>
    /// Marks service types as globally accessible to all modules.
    /// </summary>
    /// <param name="serviceTypes">The service types.</param>
    /// <returns>This policy instance for chaining.</returns>
    public ModuleIsolationPolicy MarkAsGloballyAccessible(params Type[] serviceTypes)
    {
        ArgumentNullException.ThrowIfNull(serviceTypes);

        lock (_globallyAccessibleServices)
        {
            foreach (var serviceType in serviceTypes)
            {
                _globallyAccessibleServices.Add(serviceType);
            }
        }

        return this;
    }

    /// <summary>
    /// Marks modules as trusted, bypassing access restrictions.
    /// </summary>
    /// <param name="moduleIds">The module identifiers.</param>
    /// <returns>This policy instance for chaining.</returns>
    public ModuleIsolationPolicy MarkAsTrusted(params string[] moduleIds)
    {
        ArgumentNullException.ThrowIfNull(moduleIds);

        lock (_trustedModules)
        {
            foreach (var moduleId in moduleIds)
            {
                _trustedModules.Add(moduleId);
            }
        }

        return this;
    }

    /// <summary>
    /// Gets all accessible service types for a module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <returns>A collection of accessible service types.</returns>
    public IEnumerable<Type> GetAccessibleServiceTypes(string moduleId)
    {
        var accessibleTypes = new HashSet<Type>();

        // Add globally accessible services
        accessibleTypes.UnionWith(_globallyAccessibleServices);

        // Add module-specific services
        if (_moduleAllowedServices.TryGetValue(moduleId, out var moduleServices))
        {
            accessibleTypes.UnionWith(moduleServices);
        }

        // Add services based on access levels and policy settings
        foreach (var kvp in _serviceAccessLevels)
        {
            var serviceType = kvp.Key;
            var accessLevel = kvp.Value;

            if (CheckAccessLevel(moduleId, serviceType, accessLevel).IsAllowed)
            {
                accessibleTypes.Add(serviceType);
            }
        }

        return accessibleTypes;
    }

    /// <summary>
    /// Creates a copy of this policy for customization.
    /// </summary>
    /// <returns>A copy of this policy.</returns>
    public ModuleIsolationPolicy Clone()
    {
        var clone = new ModuleIsolationPolicy
        {
            DefaultAccessLevel = DefaultAccessLevel,
            AllowFrameworkServices = AllowFrameworkServices,
            AllowLoggingServices = AllowLoggingServices,
            AllowConfigurationServices = AllowConfigurationServices
        };

        // Copy collections
        foreach (var kvp in _moduleAllowedServices)
        {
            clone._moduleAllowedServices.TryAdd(kvp.Key, new HashSet<Type>(kvp.Value));
        }

        foreach (var kvp in _serviceAccessLevels)
        {
            clone._serviceAccessLevels.TryAdd(kvp.Key, kvp.Value);
        }

        lock (_globallyAccessibleServices)
        {
            clone._globallyAccessibleServices.UnionWith(_globallyAccessibleServices);
        }

        lock (_trustedModules)
        {
            clone._trustedModules.UnionWith(_trustedModules);
        }

        return clone;
    }

    private ServiceAccessCheckResult CheckAccessLevel(string moduleId, Type serviceType, ServiceAccessLevel accessLevel)
    {
        return accessLevel switch
        {
            ServiceAccessLevel.Prohibited => ServiceAccessCheckResult.Deny("Service access is prohibited"),
            ServiceAccessLevel.ModuleOnly => ServiceAccessCheckResult.Deny("Service is module-only"),
            ServiceAccessLevel.CrossModule => ServiceAccessCheckResult.Allow("Cross-module access allowed"),
            ServiceAccessLevel.Global => ServiceAccessCheckResult.Allow("Global access"),
            _ => ServiceAccessCheckResult.Deny($"Unknown access level: {accessLevel}")
        };
    }

    private static bool IsFrameworkService(Type serviceType)
    {
        var assemblyName = serviceType.Assembly.GetName().Name;
        return assemblyName?.StartsWith("Microsoft.Extensions") == true ||
               assemblyName?.StartsWith("Microsoft.AspNetCore") == true ||
               assemblyName?.StartsWith("System") == true;
    }

    private static bool IsLoggingService(Type serviceType)
    {
        return serviceType.Namespace?.Contains("Logging") == true ||
               serviceType.Name.Contains("Logger") ||
               serviceType.Name.Contains("Log");
    }

    private static bool IsConfigurationService(Type serviceType)
    {
        return serviceType.Namespace?.Contains("Configuration") == true ||
               serviceType.Name.Contains("Configuration") ||
               serviceType.Name.Contains("Options");
    }

    private static ModuleIsolationPolicy CreateDefaultPolicy()
    {
        var policy = new ModuleIsolationPolicy
        {
            DefaultAccessLevel = ServiceAccessLevel.ModuleOnly,
            AllowFrameworkServices = true,
            AllowLoggingServices = true,
            AllowConfigurationServices = true
        };

        // Mark common framework types as globally accessible
        policy.MarkAsGloballyAccessible(
            typeof(IServiceProvider),
            typeof(IServiceScope),
            typeof(Microsoft.Extensions.Logging.ILogger),
            typeof(Microsoft.Extensions.Logging.ILoggerFactory),
            typeof(Microsoft.Extensions.Configuration.IConfiguration)
        );

        return policy;
    }
}

/// <summary>
/// Defines service access levels for module isolation.
/// </summary>
public enum ServiceAccessLevel
{
    /// <summary>
    /// Service access is prohibited.
    /// </summary>
    Prohibited,

    /// <summary>
    /// Service is only accessible within the same module.
    /// </summary>
    ModuleOnly,

    /// <summary>
    /// Service can be accessed across modules with explicit permission.
    /// </summary>
    CrossModule,

    /// <summary>
    /// Service is globally accessible to all modules.
    /// </summary>
    Global
}

/// <summary>
/// Result of a service access check.
/// </summary>
public class ServiceAccessCheckResult
{
    /// <summary>
    /// Gets a value indicating whether access is allowed.
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// Gets the reason for the access decision.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Creates an allow result.
    /// </summary>
    /// <param name="reason">The reason for allowing access.</param>
    /// <returns>An allow result.</returns>
    public static ServiceAccessCheckResult Allow(string? reason = null)
    {
        return new ServiceAccessCheckResult
        {
            IsAllowed = true,
            Reason = reason ?? "Access allowed"
        };
    }

    /// <summary>
    /// Creates a deny result.
    /// </summary>
    /// <param name="reason">The reason for denying access.</param>
    /// <returns>A deny result.</returns>
    public static ServiceAccessCheckResult Deny(string reason)
    {
        return new ServiceAccessCheckResult
        {
            IsAllowed = false,
            Reason = reason ?? "Access denied"
        };
    }
}

/// <summary>
/// Exception thrown when service access is denied for a module.
/// </summary>
public class ServiceAccessDeniedException : Exception
{
    /// <summary>
    /// Gets the module identifier.
    /// </summary>
    public string ModuleId { get; }

    /// <summary>
    /// Gets the service type that was denied.
    /// </summary>
    public Type ServiceType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAccessDeniedException"/> class.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="serviceType">The service type.</param>
    /// <param name="reason">The denial reason.</param>
    public ServiceAccessDeniedException(string moduleId, Type serviceType, string? reason = null)
        : base($"Module '{moduleId}' is denied access to service '{serviceType.Name}'. Reason: {reason ?? "Access policy violation"}")
    {
        ModuleId = moduleId;
        ServiceType = serviceType;
    }
}

/// <summary>
/// Enumeration of service access results.
/// </summary>
public enum ServiceAccessResult
{
    /// <summary>
    /// Access was allowed.
    /// </summary>
    Allowed,

    /// <summary>
    /// Access was denied.
    /// </summary>
    Denied,

    /// <summary>
    /// An error occurred during access check.
    /// </summary>
    Error
}

/// <summary>
/// Logs service access attempts for modules.
/// </summary>
internal class ServiceAccessLogger
{
    private readonly string _moduleId;
    private readonly ConcurrentDictionary<Type, ServiceAccessEntry> _accessLog = new();

    public ServiceAccessLogger(string moduleId)
    {
        _moduleId = moduleId;
    }

    public void LogAccess(Type serviceType, ServiceAccessResult result, string reason)
    {
        var entry = _accessLog.GetOrAdd(serviceType, _ => new ServiceAccessEntry(serviceType));
        entry.RecordAccess(result, reason);
    }

    public ModuleServiceStatistics GetStatistics()
    {
        var totalAttempts = _accessLog.Values.Sum(e => e.TotalAttempts);
        var allowedCount = _accessLog.Values.Sum(e => e.AllowedCount);
        var deniedCount = _accessLog.Values.Sum(e => e.DeniedCount);
        var errorCount = _accessLog.Values.Sum(e => e.ErrorCount);

        return new ModuleServiceStatistics(_moduleId)
        {
            TotalAccessAttempts = totalAttempts,
            AllowedAccesses = allowedCount,
            DeniedAccesses = deniedCount,
            ErroredAccesses = errorCount,
            AccessedServiceTypes = _accessLog.Keys.ToList()
        };
    }

    private class ServiceAccessEntry
    {
        public Type ServiceType { get; }
        public int TotalAttempts { get; private set; }
        public int AllowedCount { get; private set; }
        public int DeniedCount { get; private set; }
        public int ErrorCount { get; private set; }
        public DateTime LastAccess { get; private set; }

        public ServiceAccessEntry(Type serviceType)
        {
            ServiceType = serviceType;
        }

        public void RecordAccess(ServiceAccessResult result, string reason)
        {
            TotalAttempts++;
            LastAccess = DateTime.UtcNow;

            switch (result)
            {
                case ServiceAccessResult.Allowed:
                    AllowedCount++;
                    break;
                case ServiceAccessResult.Denied:
                    DeniedCount++;
                    break;
                case ServiceAccessResult.Error:
                    ErrorCount++;
                    break;
            }
        }
    }
}

/// <summary>
/// Contains statistics about module service access.
/// </summary>
public class ModuleServiceStatistics
{
    /// <summary>
    /// Gets the module identifier.
    /// </summary>
    public string ModuleId { get; }

    /// <summary>
    /// Gets the total number of service access attempts.
    /// </summary>
    public int TotalAccessAttempts { get; init; }

    /// <summary>
    /// Gets the number of allowed accesses.
    /// </summary>
    public int AllowedAccesses { get; init; }

    /// <summary>
    /// Gets the number of denied accesses.
    /// </summary>
    public int DeniedAccesses { get; init; }

    /// <summary>
    /// Gets the number of errored accesses.
    /// </summary>
    public int ErroredAccesses { get; init; }

    /// <summary>
    /// Gets the accessed service types.
    /// </summary>
    public IList<Type> AccessedServiceTypes { get; init; } = new List<Type>();

    /// <summary>
    /// Gets the statistics generation timestamp.
    /// </summary>
    public DateTime GeneratedAt { get; } = DateTime.UtcNow;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleServiceStatistics"/> class.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    public ModuleServiceStatistics(string moduleId)
    {
        ModuleId = moduleId ?? throw new ArgumentNullException(nameof(moduleId));
    }
}