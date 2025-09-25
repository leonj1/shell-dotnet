using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DotNetShell.Abstractions.Services;

/// <summary>
/// Service interface for telemetry operations including distributed tracing, metrics collection, and observability.
/// </summary>
public interface ITelemetryService
{
    /// <summary>
    /// Starts a new distributed tracing span for the specified operation.
    /// </summary>
    /// <param name="operationName">The name of the operation being traced.</param>
    /// <param name="kind">The kind of span (default is Internal).</param>
    /// <param name="parentContext">Optional parent span context.</param>
    /// <returns>A span representing the operation.</returns>
    ISpan StartSpan(string operationName, SpanKind kind = SpanKind.Internal, ISpanContext? parentContext = null);

    /// <summary>
    /// Starts a new Activity for OpenTelemetry integration.
    /// </summary>
    /// <param name="name">The activity name.</param>
    /// <param name="kind">The activity kind (default is Internal).</param>
    /// <param name="parentContext">Optional parent activity context.</param>
    /// <param name="tags">Optional tags to add to the activity.</param>
    /// <returns>A new Activity instance.</returns>
    Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal, ActivityContext parentContext = default, IEnumerable<KeyValuePair<string, object?>>? tags = null);

    /// <summary>
    /// Records a metric value.
    /// </summary>
    /// <param name="name">The metric name.</param>
    /// <param name="value">The metric value.</param>
    /// <param name="unit">Optional unit of measurement.</param>
    /// <param name="description">Optional metric description.</param>
    /// <param name="tags">Optional tags for the metric.</param>
    void RecordMetric(string name, double value, string? unit = null, string? description = null, IDictionary<string, object>? tags = null);

    /// <summary>
    /// Records a counter metric (monotonically increasing value).
    /// </summary>
    /// <param name="name">The counter name.</param>
    /// <param name="value">The value to add to the counter.</param>
    /// <param name="unit">Optional unit of measurement.</param>
    /// <param name="description">Optional counter description.</param>
    /// <param name="tags">Optional tags for the counter.</param>
    void RecordCounter(string name, double value = 1.0, string? unit = null, string? description = null, IDictionary<string, object>? tags = null);

    /// <summary>
    /// Records a histogram metric for distribution analysis.
    /// </summary>
    /// <param name="name">The histogram name.</param>
    /// <param name="value">The value to record in the histogram.</param>
    /// <param name="unit">Optional unit of measurement.</param>
    /// <param name="description">Optional histogram description.</param>
    /// <param name="tags">Optional tags for the histogram.</param>
    void RecordHistogram(string name, double value, string? unit = null, string? description = null, IDictionary<string, object>? tags = null);

    /// <summary>
    /// Records a gauge metric (value that can go up or down).
    /// </summary>
    /// <param name="name">The gauge name.</param>
    /// <param name="value">The current gauge value.</param>
    /// <param name="unit">Optional unit of measurement.</param>
    /// <param name="description">Optional gauge description.</param>
    /// <param name="tags">Optional tags for the gauge.</param>
    void RecordGauge(string name, double value, string? unit = null, string? description = null, IDictionary<string, object>? tags = null);

    /// <summary>
    /// Records a custom event with structured data.
    /// </summary>
    /// <param name="name">The event name.</param>
    /// <param name="properties">Optional event properties.</param>
    /// <param name="measurements">Optional event measurements.</param>
    void RecordEvent(string name, IDictionary<string, object>? properties = null, IDictionary<string, double>? measurements = null);

    /// <summary>
    /// Starts a timer for measuring operation duration.
    /// </summary>
    /// <param name="name">The timer name.</param>
    /// <param name="tags">Optional tags for the timer.</param>
    /// <returns>A timer that records duration when disposed.</returns>
    ITimer StartTimer(string name, IDictionary<string, object>? tags = null);

    /// <summary>
    /// Records a duration measurement.
    /// </summary>
    /// <param name="name">The measurement name.</param>
    /// <param name="duration">The duration to record.</param>
    /// <param name="tags">Optional tags for the measurement.</param>
    void RecordDuration(string name, TimeSpan duration, IDictionary<string, object>? tags = null);

    /// <summary>
    /// Records an exception with telemetry data.
    /// </summary>
    /// <param name="exception">The exception to record.</param>
    /// <param name="properties">Optional additional properties.</param>
    void RecordException(Exception exception, IDictionary<string, object>? properties = null);

    /// <summary>
    /// Records a dependency call (external service call).
    /// </summary>
    /// <param name="dependencyName">The name of the dependency.</param>
    /// <param name="commandName">The command or operation name.</param>
    /// <param name="startTime">When the call started.</param>
    /// <param name="duration">How long the call took.</param>
    /// <param name="success">Whether the call was successful.</param>
    /// <param name="properties">Optional additional properties.</param>
    void RecordDependency(string dependencyName, string commandName, DateTimeOffset startTime, TimeSpan duration, bool success, IDictionary<string, object>? properties = null);

    /// <summary>
    /// Records a request telemetry (incoming request).
    /// </summary>
    /// <param name="name">The request name.</param>
    /// <param name="startTime">When the request started.</param>
    /// <param name="duration">How long the request took.</param>
    /// <param name="responseCode">The response code.</param>
    /// <param name="success">Whether the request was successful.</param>
    /// <param name="properties">Optional additional properties.</param>
    void RecordRequest(string name, DateTimeOffset startTime, TimeSpan duration, string responseCode, bool success, IDictionary<string, object>? properties = null);

    /// <summary>
    /// Sets a global tag that will be included in all telemetry data.
    /// </summary>
    /// <param name="key">The tag key.</param>
    /// <param name="value">The tag value.</param>
    void SetGlobalTag(string key, object value);

    /// <summary>
    /// Removes a global tag.
    /// </summary>
    /// <param name="key">The tag key to remove.</param>
    void RemoveGlobalTag(string key);

    /// <summary>
    /// Gets all current global tags.
    /// </summary>
    /// <returns>A read-only dictionary of global tags.</returns>
    IReadOnlyDictionary<string, object> GetGlobalTags();

    /// <summary>
    /// Creates a telemetry context that can be passed across async boundaries.
    /// </summary>
    /// <returns>A telemetry context.</returns>
    ITelemetryContext CreateContext();

    /// <summary>
    /// Sets the current telemetry context.
    /// </summary>
    /// <param name="context">The telemetry context to set.</param>
    /// <returns>A disposable that restores the previous context when disposed.</returns>
    IDisposable SetContext(ITelemetryContext context);

    /// <summary>
    /// Flushes all pending telemetry data.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the flush operation.</returns>
    Task FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current telemetry context.
    /// </summary>
    ITelemetryContext? CurrentContext { get; }

    /// <summary>
    /// Gets a value indicating whether telemetry collection is enabled.
    /// </summary>
    bool IsEnabled { get; }
}

/// <summary>
/// Represents a distributed tracing span.
/// </summary>
public interface ISpan : IDisposable
{
    /// <summary>
    /// Gets the span ID.
    /// </summary>
    string SpanId { get; }

    /// <summary>
    /// Gets the trace ID that this span belongs to.
    /// </summary>
    string TraceId { get; }

    /// <summary>
    /// Gets or sets the span name.
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// Gets the span kind.
    /// </summary>
    SpanKind Kind { get; }

    /// <summary>
    /// Gets or sets the span status.
    /// </summary>
    SpanStatus Status { get; set; }

    /// <summary>
    /// Gets the span start time.
    /// </summary>
    DateTimeOffset StartTime { get; }

    /// <summary>
    /// Gets the span end time (null if not ended).
    /// </summary>
    DateTimeOffset? EndTime { get; }

    /// <summary>
    /// Sets an attribute on the span.
    /// </summary>
    /// <param name="key">The attribute key.</param>
    /// <param name="value">The attribute value.</param>
    void SetAttribute(string key, object value);

    /// <summary>
    /// Sets multiple attributes on the span.
    /// </summary>
    /// <param name="attributes">The attributes to set.</param>
    void SetAttributes(IDictionary<string, object> attributes);

    /// <summary>
    /// Adds an event to the span.
    /// </summary>
    /// <param name="name">The event name.</param>
    /// <param name="attributes">Optional event attributes.</param>
    void AddEvent(string name, IDictionary<string, object>? attributes = null);

    /// <summary>
    /// Records an exception in the span.
    /// </summary>
    /// <param name="exception">The exception to record.</param>
    void RecordException(Exception exception);

    /// <summary>
    /// Ends the span.
    /// </summary>
    void End();

    /// <summary>
    /// Gets all attributes set on the span.
    /// </summary>
    IReadOnlyDictionary<string, object> Attributes { get; }

    /// <summary>
    /// Gets the span context for propagating trace information.
    /// </summary>
    ISpanContext Context { get; }
}

/// <summary>
/// Represents span context information for trace propagation.
/// </summary>
public interface ISpanContext
{
    /// <summary>
    /// Gets the trace ID.
    /// </summary>
    string TraceId { get; }

    /// <summary>
    /// Gets the span ID.
    /// </summary>
    string SpanId { get; }

    /// <summary>
    /// Gets the trace flags.
    /// </summary>
    byte TraceFlags { get; }

    /// <summary>
    /// Gets the trace state.
    /// </summary>
    string? TraceState { get; }

    /// <summary>
    /// Gets a value indicating whether the context is valid.
    /// </summary>
    bool IsValid { get; }
}

/// <summary>
/// Represents a timer for measuring durations.
/// </summary>
public interface ITimer : IDisposable
{
    /// <summary>
    /// Gets the timer name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the elapsed time since the timer was started.
    /// </summary>
    TimeSpan Elapsed { get; }

    /// <summary>
    /// Gets the tags associated with the timer.
    /// </summary>
    IReadOnlyDictionary<string, object> Tags { get; }

    /// <summary>
    /// Stops the timer and records the measurement.
    /// </summary>
    void Stop();
}

/// <summary>
/// Represents a telemetry context that can be propagated across async operations.
/// </summary>
public interface ITelemetryContext
{
    /// <summary>
    /// Gets the correlation ID for this context.
    /// </summary>
    string? CorrelationId { get; }

    /// <summary>
    /// Gets the current span context, if any.
    /// </summary>
    ISpanContext? SpanContext { get; }

    /// <summary>
    /// Gets the baggage (key-value pairs propagated with the trace).
    /// </summary>
    IReadOnlyDictionary<string, string> Baggage { get; }

    /// <summary>
    /// Creates a new context with additional baggage.
    /// </summary>
    /// <param name="key">The baggage key.</param>
    /// <param name="value">The baggage value.</param>
    /// <returns>A new context with the added baggage.</returns>
    ITelemetryContext WithBaggage(string key, string value);

    /// <summary>
    /// Creates a new context with a correlation ID.
    /// </summary>
    /// <param name="correlationId">The correlation ID.</param>
    /// <returns>A new context with the correlation ID.</returns>
    ITelemetryContext WithCorrelationId(string correlationId);
}

/// <summary>
/// Enumeration of span kinds for distributed tracing.
/// </summary>
public enum SpanKind
{
    /// <summary>
    /// Internal span within the same process.
    /// </summary>
    Internal,

    /// <summary>
    /// Server span (receives a request).
    /// </summary>
    Server,

    /// <summary>
    /// Client span (sends a request).
    /// </summary>
    Client,

    /// <summary>
    /// Producer span (sends a message).
    /// </summary>
    Producer,

    /// <summary>
    /// Consumer span (receives a message).
    /// </summary>
    Consumer
}

/// <summary>
/// Enumeration of span status values.
/// </summary>
public enum SpanStatus
{
    /// <summary>
    /// The operation completed successfully.
    /// </summary>
    Ok,

    /// <summary>
    /// The operation was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// An unknown error occurred.
    /// </summary>
    UnknownError,

    /// <summary>
    /// Invalid argument error.
    /// </summary>
    InvalidArgument,

    /// <summary>
    /// Deadline exceeded error.
    /// </summary>
    DeadlineExceeded,

    /// <summary>
    /// Not found error.
    /// </summary>
    NotFound,

    /// <summary>
    /// Already exists error.
    /// </summary>
    AlreadyExists,

    /// <summary>
    /// Permission denied error.
    /// </summary>
    PermissionDenied,

    /// <summary>
    /// Resource exhausted error.
    /// </summary>
    ResourceExhausted,

    /// <summary>
    /// Failed precondition error.
    /// </summary>
    FailedPrecondition,

    /// <summary>
    /// Aborted error.
    /// </summary>
    Aborted,

    /// <summary>
    /// Out of range error.
    /// </summary>
    OutOfRange,

    /// <summary>
    /// Unimplemented error.
    /// </summary>
    Unimplemented,

    /// <summary>
    /// Internal error.
    /// </summary>
    InternalError,

    /// <summary>
    /// Unavailable error.
    /// </summary>
    Unavailable,

    /// <summary>
    /// Data loss error.
    /// </summary>
    DataLoss,

    /// <summary>
    /// Unauthenticated error.
    /// </summary>
    Unauthenticated
}

/// <summary>
/// Extension methods for ITelemetryService to provide additional convenience methods.
/// </summary>
public static class TelemetryServiceExtensions
{
    /// <summary>
    /// Records an HTTP request metric.
    /// </summary>
    /// <param name="telemetry">The telemetry service.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The request path.</param>
    /// <param name="statusCode">The response status code.</param>
    /// <param name="duration">The request duration.</param>
    public static void RecordHttpRequest(this ITelemetryService telemetry, string method, string path, int statusCode, TimeSpan duration)
    {
        var tags = new Dictionary<string, object>
        {
            { "http.method", method },
            { "http.route", path },
            { "http.status_code", statusCode }
        };

        telemetry.RecordHistogram("http.request.duration", duration.TotalMilliseconds, "ms", "HTTP request duration", tags);
        telemetry.RecordCounter("http.requests.total", 1.0, "requests", "Total HTTP requests", tags);
    }

    /// <summary>
    /// Records a database operation metric.
    /// </summary>
    /// <param name="telemetry">The telemetry service.</param>
    /// <param name="operation">The database operation (SELECT, INSERT, etc.).</param>
    /// <param name="table">The table name.</param>
    /// <param name="duration">The operation duration.</param>
    /// <param name="success">Whether the operation was successful.</param>
    public static void RecordDatabaseOperation(this ITelemetryService telemetry, string operation, string table, TimeSpan duration, bool success)
    {
        var tags = new Dictionary<string, object>
        {
            { "db.operation", operation },
            { "db.table", table },
            { "db.success", success }
        };

        telemetry.RecordHistogram("db.operation.duration", duration.TotalMilliseconds, "ms", "Database operation duration", tags);
        telemetry.RecordCounter("db.operations.total", 1.0, "operations", "Total database operations", tags);
    }

    /// <summary>
    /// Records a cache operation metric.
    /// </summary>
    /// <param name="telemetry">The telemetry service.</param>
    /// <param name="operation">The cache operation (GET, SET, DELETE, etc.).</param>
    /// <param name="hit">Whether it was a cache hit (for GET operations).</param>
    /// <param name="duration">The operation duration.</param>
    public static void RecordCacheOperation(this ITelemetryService telemetry, string operation, bool? hit, TimeSpan duration)
    {
        var tags = new Dictionary<string, object>
        {
            { "cache.operation", operation }
        };

        if (hit.HasValue)
        {
            tags["cache.hit"] = hit.Value;
        }

        telemetry.RecordHistogram("cache.operation.duration", duration.TotalMilliseconds, "ms", "Cache operation duration", tags);
        telemetry.RecordCounter("cache.operations.total", 1.0, "operations", "Total cache operations", tags);
    }

    /// <summary>
    /// Creates a span for an HTTP client request.
    /// </summary>
    /// <param name="telemetry">The telemetry service.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="uri">The request URI.</param>
    /// <returns>A client span for the HTTP request.</returns>
    public static ISpan StartHttpClientSpan(this ITelemetryService telemetry, string method, Uri uri)
    {
        var span = telemetry.StartSpan($"HTTP {method}", SpanKind.Client);
        span.SetAttribute("http.method", method);
        span.SetAttribute("http.url", uri.ToString());
        span.SetAttribute("http.scheme", uri.Scheme);
        span.SetAttribute("net.peer.name", uri.Host);
        span.SetAttribute("net.peer.port", uri.Port);
        return span;
    }

    /// <summary>
    /// Creates a span for a database operation.
    /// </summary>
    /// <param name="telemetry">The telemetry service.</param>
    /// <param name="operation">The database operation.</param>
    /// <param name="table">The table name.</param>
    /// <returns>A client span for the database operation.</returns>
    public static ISpan StartDatabaseSpan(this ITelemetryService telemetry, string operation, string table)
    {
        var span = telemetry.StartSpan($"DB {operation}", SpanKind.Client);
        span.SetAttribute("db.operation", operation);
        span.SetAttribute("db.sql.table", table);
        return span;
    }
}