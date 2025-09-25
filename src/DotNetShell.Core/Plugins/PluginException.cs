using System.Runtime.Serialization;

namespace DotNetShell.Core.Plugins;

/// <summary>
/// Base exception for all plugin-related errors.
/// </summary>
[Serializable]
public abstract class PluginException : Exception
{
    /// <summary>
    /// Gets the plugin identifier associated with this exception.
    /// </summary>
    public string? PluginId { get; }

    /// <summary>
    /// Gets the plugin path associated with this exception.
    /// </summary>
    public string? PluginPath { get; }

    /// <summary>
    /// Gets additional context information about the exception.
    /// </summary>
    public Dictionary<string, object> Context { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginException"/> class.
    /// </summary>
    protected PluginException() : base()
    {
        Context = new Dictionary<string, object>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    protected PluginException(string message) : base(message)
    {
        Context = new Dictionary<string, object>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    protected PluginException(string message, Exception innerException) : base(message, innerException)
    {
        Context = new Dictionary<string, object>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="pluginPath">The plugin path.</param>
    protected PluginException(string message, string? pluginId, string? pluginPath) : base(message)
    {
        PluginId = pluginId;
        PluginPath = pluginPath;
        Context = new Dictionary<string, object>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="pluginPath">The plugin path.</param>
    /// <param name="innerException">The inner exception.</param>
    protected PluginException(string message, string? pluginId, string? pluginPath, Exception innerException)
        : base(message, innerException)
    {
        PluginId = pluginId;
        PluginPath = pluginPath;
        Context = new Dictionary<string, object>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginException"/> class.
    /// </summary>
    /// <param name="info">Serialization info.</param>
    /// <param name="context">Streaming context.</param>
    protected PluginException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
        PluginId = info.GetString(nameof(PluginId));
        PluginPath = info.GetString(nameof(PluginPath));
        Context = new Dictionary<string, object>();
    }

    /// <summary>
    /// Sets serialization data for this exception.
    /// </summary>
    /// <param name="info">Serialization info.</param>
    /// <param name="context">Streaming context.</param>
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(PluginId), PluginId);
        info.AddValue(nameof(PluginPath), PluginPath);
    }

    /// <summary>
    /// Adds context information to the exception.
    /// </summary>
    /// <param name="key">The context key.</param>
    /// <param name="value">The context value.</param>
    public void AddContext(string key, object value)
    {
        Context[key] = value;
    }
}

/// <summary>
/// Exception thrown when a plugin fails to load.
/// </summary>
[Serializable]
public class PluginLoadException : PluginException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginLoadException"/> class.
    /// </summary>
    public PluginLoadException() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginLoadException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public PluginLoadException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginLoadException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public PluginLoadException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginLoadException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="pluginPath">The plugin path.</param>
    public PluginLoadException(string message, string? pluginId, string? pluginPath)
        : base(message, pluginId, pluginPath) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginLoadException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="pluginPath">The plugin path.</param>
    /// <param name="innerException">The inner exception.</param>
    public PluginLoadException(string message, string? pluginId, string? pluginPath, Exception innerException)
        : base(message, pluginId, pluginPath, innerException) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginLoadException"/> class.
    /// </summary>
    /// <param name="info">Serialization info.</param>
    /// <param name="context">Streaming context.</param>
    protected PluginLoadException(SerializationInfo info, StreamingContext context) : base(info, context) { }
}

/// <summary>
/// Exception thrown when a plugin fails to unload.
/// </summary>
[Serializable]
public class PluginUnloadException : PluginException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginUnloadException"/> class.
    /// </summary>
    public PluginUnloadException() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginUnloadException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public PluginUnloadException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginUnloadException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public PluginUnloadException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginUnloadException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="pluginPath">The plugin path.</param>
    public PluginUnloadException(string message, string? pluginId, string? pluginPath)
        : base(message, pluginId, pluginPath) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginUnloadException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="pluginPath">The plugin path.</param>
    /// <param name="innerException">The inner exception.</param>
    public PluginUnloadException(string message, string? pluginId, string? pluginPath, Exception innerException)
        : base(message, pluginId, pluginPath, innerException) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginUnloadException"/> class.
    /// </summary>
    /// <param name="info">Serialization info.</param>
    /// <param name="context">Streaming context.</param>
    protected PluginUnloadException(SerializationInfo info, StreamingContext context) : base(info, context) { }
}

/// <summary>
/// Exception thrown when plugin validation fails.
/// </summary>
[Serializable]
public class PluginValidationException : PluginException
{
    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IReadOnlyList<string> ValidationErrors { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginValidationException"/> class.
    /// </summary>
    public PluginValidationException() : base()
    {
        ValidationErrors = new List<string>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginValidationException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public PluginValidationException(string message) : base(message)
    {
        ValidationErrors = new List<string>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginValidationException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="validationErrors">The validation errors.</param>
    public PluginValidationException(string message, IEnumerable<string> validationErrors) : base(message)
    {
        ValidationErrors = validationErrors.ToList().AsReadOnly();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginValidationException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="pluginPath">The plugin path.</param>
    /// <param name="validationErrors">The validation errors.</param>
    public PluginValidationException(string message, string? pluginId, string? pluginPath, IEnumerable<string> validationErrors)
        : base(message, pluginId, pluginPath)
    {
        ValidationErrors = validationErrors.ToList().AsReadOnly();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginValidationException"/> class.
    /// </summary>
    /// <param name="info">Serialization info.</param>
    /// <param name="context">Streaming context.</param>
    protected PluginValidationException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
        ValidationErrors = new List<string>();
    }
}

/// <summary>
/// Exception thrown when plugin initialization fails.
/// </summary>
[Serializable]
public class PluginInitializationException : PluginException
{
    /// <summary>
    /// Gets the phase during which initialization failed.
    /// </summary>
    public string? InitializationPhase { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginInitializationException"/> class.
    /// </summary>
    public PluginInitializationException() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginInitializationException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public PluginInitializationException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginInitializationException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public PluginInitializationException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginInitializationException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="pluginPath">The plugin path.</param>
    /// <param name="initializationPhase">The initialization phase that failed.</param>
    public PluginInitializationException(string message, string? pluginId, string? pluginPath, string? initializationPhase)
        : base(message, pluginId, pluginPath)
    {
        InitializationPhase = initializationPhase;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginInitializationException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="pluginPath">The plugin path.</param>
    /// <param name="initializationPhase">The initialization phase that failed.</param>
    /// <param name="innerException">The inner exception.</param>
    public PluginInitializationException(string message, string? pluginId, string? pluginPath, string? initializationPhase, Exception innerException)
        : base(message, pluginId, pluginPath, innerException)
    {
        InitializationPhase = initializationPhase;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginInitializationException"/> class.
    /// </summary>
    /// <param name="info">Serialization info.</param>
    /// <param name="context">Streaming context.</param>
    protected PluginInitializationException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
        InitializationPhase = info.GetString(nameof(InitializationPhase));
    }

    /// <summary>
    /// Sets serialization data for this exception.
    /// </summary>
    /// <param name="info">Serialization info.</param>
    /// <param name="context">Streaming context.</param>
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(InitializationPhase), InitializationPhase);
    }
}

/// <summary>
/// Exception thrown when plugin discovery fails.
/// </summary>
[Serializable]
public class PluginDiscoveryException : PluginException
{
    /// <summary>
    /// Gets the discovery path that failed.
    /// </summary>
    public string? DiscoveryPath { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginDiscoveryException"/> class.
    /// </summary>
    public PluginDiscoveryException() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginDiscoveryException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public PluginDiscoveryException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginDiscoveryException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public PluginDiscoveryException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginDiscoveryException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="discoveryPath">The discovery path that failed.</param>
    public PluginDiscoveryException(string message, string? discoveryPath) : base(message)
    {
        DiscoveryPath = discoveryPath;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginDiscoveryException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="discoveryPath">The discovery path that failed.</param>
    /// <param name="innerException">The inner exception.</param>
    public PluginDiscoveryException(string message, string? discoveryPath, Exception innerException)
        : base(message, innerException)
    {
        DiscoveryPath = discoveryPath;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginDiscoveryException"/> class.
    /// </summary>
    /// <param name="info">Serialization info.</param>
    /// <param name="context">Streaming context.</param>
    protected PluginDiscoveryException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
        DiscoveryPath = info.GetString(nameof(DiscoveryPath));
    }

    /// <summary>
    /// Sets serialization data for this exception.
    /// </summary>
    /// <param name="info">Serialization info.</param>
    /// <param name="context">Streaming context.</param>
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(DiscoveryPath), DiscoveryPath);
    }
}

/// <summary>
/// Exception thrown when service access is denied by the isolation policy.
/// </summary>
[Serializable]
public class ServiceAccessDeniedException : PluginException
{
    /// <summary>
    /// Gets the service type that was denied access.
    /// </summary>
    public Type? ServiceType { get; }

    /// <summary>
    /// Gets the reason for access denial.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAccessDeniedException"/> class.
    /// </summary>
    public ServiceAccessDeniedException() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAccessDeniedException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public ServiceAccessDeniedException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAccessDeniedException"/> class.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="serviceType">The service type that was denied.</param>
    /// <param name="reason">The reason for denial.</param>
    public ServiceAccessDeniedException(string? pluginId, Type serviceType, string? reason)
        : base($"Access to service '{serviceType.Name}' was denied for plugin '{pluginId}': {reason}")
    {
        ServiceType = serviceType;
        Reason = reason;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAccessDeniedException"/> class.
    /// </summary>
    /// <param name="info">Serialization info.</param>
    /// <param name="context">Streaming context.</param>
    protected ServiceAccessDeniedException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
        ServiceType = (Type?)info.GetValue(nameof(ServiceType), typeof(Type));
        Reason = info.GetString(nameof(Reason));
    }

    /// <summary>
    /// Sets serialization data for this exception.
    /// </summary>
    /// <param name="info">Serialization info.</param>
    /// <param name="context">Streaming context.</param>
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(ServiceType), ServiceType);
        info.AddValue(nameof(Reason), Reason);
    }
}