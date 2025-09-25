namespace DotNetShell.Abstractions.Models;

/// <summary>
/// Represents the health status of a service or component.
/// </summary>
public class ServiceHealthStatus
{
    /// <summary>
    /// Gets the service name.
    /// </summary>
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the health status of the service.
    /// </summary>
    public HealthStatusLevel Status { get; init; }

    /// <summary>
    /// Gets the description of the health status.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets additional health data and metrics.
    /// </summary>
    public IDictionary<string, object> Data { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets the timestamp when the health check was performed.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the duration of the health check.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets any exception that occurred during the health check.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Gets the tags associated with this service.
    /// </summary>
    public IList<string> Tags { get; init; } = new List<string>();

    /// <summary>
    /// Creates a healthy status result.
    /// </summary>
    /// <param name="serviceName">The service name.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="data">Optional health data.</param>
    /// <param name="duration">Optional health check duration.</param>
    /// <param name="tags">Optional tags.</param>
    /// <returns>A healthy status result.</returns>
    public static ServiceHealthStatus Healthy(string serviceName, string? description = null, IDictionary<string, object>? data = null, TimeSpan duration = default, IList<string>? tags = null)
    {
        return new ServiceHealthStatus
        {
            ServiceName = serviceName,
            Status = HealthStatusLevel.Healthy,
            Description = description,
            Data = data ?? new Dictionary<string, object>(),
            Duration = duration,
            Tags = tags ?? new List<string>()
        };
    }

    /// <summary>
    /// Creates a degraded status result.
    /// </summary>
    /// <param name="serviceName">The service name.</param>
    /// <param name="description">The description of the degraded status.</param>
    /// <param name="data">Optional health data.</param>
    /// <param name="duration">Optional health check duration.</param>
    /// <param name="tags">Optional tags.</param>
    /// <returns>A degraded status result.</returns>
    public static ServiceHealthStatus Degraded(string serviceName, string description, IDictionary<string, object>? data = null, TimeSpan duration = default, IList<string>? tags = null)
    {
        return new ServiceHealthStatus
        {
            ServiceName = serviceName,
            Status = HealthStatusLevel.Degraded,
            Description = description,
            Data = data ?? new Dictionary<string, object>(),
            Duration = duration,
            Tags = tags ?? new List<string>()
        };
    }

    /// <summary>
    /// Creates an unhealthy status result.
    /// </summary>
    /// <param name="serviceName">The service name.</param>
    /// <param name="description">The description of the unhealthy status.</param>
    /// <param name="exception">Optional exception that caused the unhealthy status.</param>
    /// <param name="data">Optional health data.</param>
    /// <param name="duration">Optional health check duration.</param>
    /// <param name="tags">Optional tags.</param>
    /// <returns>An unhealthy status result.</returns>
    public static ServiceHealthStatus Unhealthy(string serviceName, string description, Exception? exception = null, IDictionary<string, object>? data = null, TimeSpan duration = default, IList<string>? tags = null)
    {
        return new ServiceHealthStatus
        {
            ServiceName = serviceName,
            Status = HealthStatusLevel.Unhealthy,
            Description = description,
            Exception = exception,
            Data = data ?? new Dictionary<string, object>(),
            Duration = duration,
            Tags = tags ?? new List<string>()
        };
    }
}

/// <summary>
/// Enumeration of health status levels.
/// </summary>
public enum HealthStatusLevel
{
    /// <summary>
    /// The service is healthy and functioning normally.
    /// </summary>
    Healthy,

    /// <summary>
    /// The service is functioning but with degraded performance or capabilities.
    /// </summary>
    Degraded,

    /// <summary>
    /// The service is not functioning properly.
    /// </summary>
    Unhealthy
}

/// <summary>
/// Represents a composite health status containing multiple service health statuses.
/// </summary>
public class CompositeServiceHealthStatus
{
    /// <summary>
    /// Gets the overall health status.
    /// </summary>
    public HealthStatusLevel OverallStatus { get; init; }

    /// <summary>
    /// Gets the individual service health statuses.
    /// </summary>
    public IDictionary<string, ServiceHealthStatus> Services { get; init; } = new Dictionary<string, ServiceHealthStatus>();

    /// <summary>
    /// Gets the timestamp when the composite health check was performed.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the total duration of all health checks.
    /// </summary>
    public TimeSpan TotalDuration { get; init; }

    /// <summary>
    /// Gets the number of healthy services.
    /// </summary>
    public int HealthyServices => Services.Values.Count(s => s.Status == HealthStatusLevel.Healthy);

    /// <summary>
    /// Gets the number of degraded services.
    /// </summary>
    public int DegradedServices => Services.Values.Count(s => s.Status == HealthStatusLevel.Degraded);

    /// <summary>
    /// Gets the number of unhealthy services.
    /// </summary>
    public int UnhealthyServices => Services.Values.Count(s => s.Status == HealthStatusLevel.Unhealthy);

    /// <summary>
    /// Gets the total number of services checked.
    /// </summary>
    public int TotalServices => Services.Count;

    /// <summary>
    /// Creates a composite health status from individual service statuses.
    /// </summary>
    /// <param name="serviceStatuses">The individual service health statuses.</param>
    /// <returns>A composite health status.</returns>
    public static CompositeServiceHealthStatus Create(IDictionary<string, ServiceHealthStatus> serviceStatuses)
    {
        var overallStatus = DetermineOverallStatus(serviceStatuses.Values);
        var totalDuration = serviceStatuses.Values.Aggregate(TimeSpan.Zero, (sum, status) => sum.Add(status.Duration));

        return new CompositeServiceHealthStatus
        {
            OverallStatus = overallStatus,
            Services = serviceStatuses,
            TotalDuration = totalDuration
        };
    }

    private static HealthStatusLevel DetermineOverallStatus(IEnumerable<ServiceHealthStatus> statuses)
    {
        var statusList = statuses.ToList();

        if (statusList.Any(s => s.Status == HealthStatusLevel.Unhealthy))
            return HealthStatusLevel.Unhealthy;

        if (statusList.Any(s => s.Status == HealthStatusLevel.Degraded))
            return HealthStatusLevel.Degraded;

        return HealthStatusLevel.Healthy;
    }
}

/// <summary>
/// Health check configuration for a service.
/// </summary>
public class ServiceHealthCheckConfiguration
{
    /// <summary>
    /// Gets or sets the service name.
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the health check interval.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the health check timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the delay before starting health checks.
    /// </summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Gets or sets the number of consecutive failures required to mark the service as unhealthy.
    /// </summary>
    public int FailureThreshold { get; set; } = 3;

    /// <summary>
    /// Gets or sets the number of consecutive successes required to mark the service as healthy after being unhealthy.
    /// </summary>
    public int SuccessThreshold { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether the health check is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the tags for this health check.
    /// </summary>
    public IList<string> Tags { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets additional configuration properties.
    /// </summary>
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
}