using Microsoft.Extensions.Logging;

namespace DotNetShell.Abstractions.Services;

/// <summary>
/// Service interface for structured logging operations with correlation and contextual data support.
/// </summary>
public interface ILoggingService
{
    /// <summary>
    /// Logs a trace message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional message arguments for string formatting.</param>
    void LogTrace(string message, params object[] args);

    /// <summary>
    /// Logs a debug message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional message arguments for string formatting.</param>
    void LogDebug(string message, params object[] args);

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional message arguments for string formatting.</param>
    void LogInfo(string message, params object[] args);

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional message arguments for string formatting.</param>
    void LogWarning(string message, params object[] args);

    /// <summary>
    /// Logs an error message with an exception.
    /// </summary>
    /// <param name="ex">The exception to log.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional message arguments for string formatting.</param>
    void LogError(Exception ex, string message, params object[] args);

    /// <summary>
    /// Logs an error message without an exception.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional message arguments for string formatting.</param>
    void LogError(string message, params object[] args);

    /// <summary>
    /// Logs a critical error message with an exception.
    /// </summary>
    /// <param name="ex">The exception to log.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional message arguments for string formatting.</param>
    void LogCritical(Exception ex, string message, params object[] args);

    /// <summary>
    /// Logs a critical error message without an exception.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional message arguments for string formatting.</param>
    void LogCritical(string message, params object[] args);

    /// <summary>
    /// Creates a logging scope with state information that will be included in all log entries within the scope.
    /// </summary>
    /// <typeparam name="TState">The type of the state object.</typeparam>
    /// <param name="state">The state object to include in the scope.</param>
    /// <returns>A disposable object that represents the scope.</returns>
    IDisposable BeginScope<TState>(TState state) where TState : notnull;

    /// <summary>
    /// Creates a logging scope with a simple key-value pair.
    /// </summary>
    /// <param name="key">The key for the scope data.</param>
    /// <param name="value">The value for the scope data.</param>
    /// <returns>A disposable object that represents the scope.</returns>
    IDisposable BeginScope(string key, object value);

    /// <summary>
    /// Creates a logging scope with multiple key-value pairs.
    /// </summary>
    /// <param name="properties">The properties to include in the scope.</param>
    /// <returns>A disposable object that represents the scope.</returns>
    IDisposable BeginScope(IDictionary<string, object> properties);

    /// <summary>
    /// Logs structured data with a specific log level.
    /// </summary>
    /// <typeparam name="T">The type of the structured data.</typeparam>
    /// <param name="level">The log level.</param>
    /// <param name="message">The message template.</param>
    /// <param name="data">The structured data to log.</param>
    void LogStructured<T>(LogLevel level, string message, T data);

    /// <summary>
    /// Logs structured data as information.
    /// </summary>
    /// <typeparam name="T">The type of the structured data.</typeparam>
    /// <param name="message">The message template.</param>
    /// <param name="data">The structured data to log.</param>
    void LogStructured<T>(string message, T data);

    /// <summary>
    /// Creates a logger with additional context that will be included in all log entries.
    /// </summary>
    /// <param name="context">The context data to add.</param>
    /// <returns>A new logging service instance with the added context.</returns>
    ILoggingService WithContext(IDictionary<string, object> context);

    /// <summary>
    /// Creates a logger with additional context using a key-value pair.
    /// </summary>
    /// <param name="key">The context key.</param>
    /// <param name="value">The context value.</param>
    /// <returns>A new logging service instance with the added context.</returns>
    ILoggingService WithContext(string key, object value);

    /// <summary>
    /// Creates a logger with a correlation ID for tracking related operations.
    /// </summary>
    /// <param name="correlationId">The correlation ID to associate with log entries.</param>
    /// <returns>A new logging service instance with the correlation ID.</returns>
    ILoggingService WithCorrelationId(string correlationId);

    /// <summary>
    /// Creates a logger with a user context for tracking user-related operations.
    /// </summary>
    /// <param name="userId">The user ID to associate with log entries.</param>
    /// <param name="userName">Optional user name to include.</param>
    /// <returns>A new logging service instance with the user context.</returns>
    ILoggingService WithUser(string userId, string? userName = null);

    /// <summary>
    /// Creates a logger with a module context for tracking module-specific operations.
    /// </summary>
    /// <param name="moduleName">The module name to associate with log entries.</param>
    /// <param name="moduleVersion">Optional module version to include.</param>
    /// <returns>A new logging service instance with the module context.</returns>
    ILoggingService WithModule(string moduleName, string? moduleVersion = null);

    /// <summary>
    /// Logs a business event with structured data.
    /// </summary>
    /// <param name="eventName">The name of the business event.</param>
    /// <param name="eventData">The structured data associated with the event.</param>
    /// <param name="level">The log level for the event (default is Information).</param>
    void LogBusinessEvent(string eventName, object? eventData = null, LogLevel level = LogLevel.Information);

    /// <summary>
    /// Logs a performance metric.
    /// </summary>
    /// <param name="metricName">The name of the performance metric.</param>
    /// <param name="value">The metric value.</param>
    /// <param name="unit">The unit of measurement (optional).</param>
    /// <param name="tags">Additional tags for the metric (optional).</param>
    void LogPerformanceMetric(string metricName, double value, string? unit = null, IDictionary<string, string>? tags = null);

    /// <summary>
    /// Logs the start of an operation and returns a disposable that logs the completion and duration when disposed.
    /// </summary>
    /// <param name="operationName">The name of the operation.</param>
    /// <param name="context">Optional context data for the operation.</param>
    /// <returns>A disposable operation tracker.</returns>
    IDisposable LogOperation(string operationName, IDictionary<string, object>? context = null);

    /// <summary>
    /// Logs an audit event for security and compliance tracking.
    /// </summary>
    /// <param name="action">The action that was performed.</param>
    /// <param name="resource">The resource that was accessed.</param>
    /// <param name="userId">The user who performed the action.</param>
    /// <param name="success">Whether the action was successful.</param>
    /// <param name="additionalData">Additional audit data.</param>
    void LogAuditEvent(string action, string resource, string userId, bool success, IDictionary<string, object>? additionalData = null);

    /// <summary>
    /// Gets a value indicating whether logging is enabled for the specified level.
    /// </summary>
    /// <param name="level">The log level to check.</param>
    /// <returns>True if logging is enabled for the level; otherwise, false.</returns>
    bool IsEnabled(LogLevel level);

    /// <summary>
    /// Gets the current correlation ID associated with this logger instance.
    /// </summary>
    string? CorrelationId { get; }

    /// <summary>
    /// Gets the current context properties associated with this logger instance.
    /// </summary>
    IReadOnlyDictionary<string, object> Context { get; }
}

/// <summary>
/// Represents a structured log entry with additional metadata.
/// </summary>
public class StructuredLogEntry
{
    /// <summary>
    /// Gets or sets the log level.
    /// </summary>
    public LogLevel Level { get; set; }

    /// <summary>
    /// Gets or sets the message template.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the log entry was created.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the exception information, if any.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets or sets the structured properties associated with the log entry.
    /// </summary>
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the scope information for the log entry.
    /// </summary>
    public IList<IDictionary<string, object>> Scopes { get; set; } = new List<IDictionary<string, object>>();

    /// <summary>
    /// Gets or sets the correlation ID for the log entry.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the module name that created the log entry.
    /// </summary>
    public string? ModuleName { get; set; }

    /// <summary>
    /// Gets or sets the user ID associated with the log entry.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the category name for the logger.
    /// </summary>
    public string? Category { get; set; }
}

/// <summary>
/// Extension methods for ILoggingService to provide additional convenience methods.
/// </summary>
public static class LoggingServiceExtensions
{
    /// <summary>
    /// Logs an HTTP request with structured data.
    /// </summary>
    /// <param name="logger">The logging service.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The request path.</param>
    /// <param name="statusCode">The response status code.</param>
    /// <param name="duration">The request duration.</param>
    /// <param name="correlationId">Optional correlation ID.</param>
    public static void LogHttpRequest(this ILoggingService logger, string method, string path, int statusCode, TimeSpan duration, string? correlationId = null)
    {
        var context = new Dictionary<string, object>
        {
            { "HttpMethod", method },
            { "RequestPath", path },
            { "StatusCode", statusCode },
            { "Duration", duration.TotalMilliseconds },
            { "DurationUnit", "ms" }
        };

        if (!string.IsNullOrEmpty(correlationId))
        {
            context["CorrelationId"] = correlationId;
        }

        logger.LogStructured(LogLevel.Information, "HTTP {Method} {Path} responded {StatusCode} in {Duration}ms", context);
    }

    /// <summary>
    /// Logs a database query with execution details.
    /// </summary>
    /// <param name="logger">The logging service.</param>
    /// <param name="query">The SQL query or description.</param>
    /// <param name="duration">The execution duration.</param>
    /// <param name="recordCount">The number of records affected/returned.</param>
    /// <param name="level">The log level (default is Debug).</param>
    public static void LogDatabaseQuery(this ILoggingService logger, string query, TimeSpan duration, int? recordCount = null, LogLevel level = LogLevel.Debug)
    {
        var context = new Dictionary<string, object>
        {
            { "Query", query },
            { "Duration", duration.TotalMilliseconds },
            { "DurationUnit", "ms" }
        };

        if (recordCount.HasValue)
        {
            context["RecordCount"] = recordCount.Value;
        }

        logger.LogStructured(level, "Database query executed in {Duration}ms", context);
    }

    /// <summary>
    /// Logs an external API call with details.
    /// </summary>
    /// <param name="logger">The logging service.</param>
    /// <param name="serviceName">The name of the external service.</param>
    /// <param name="endpoint">The API endpoint called.</param>
    /// <param name="method">The HTTP method used.</param>
    /// <param name="statusCode">The response status code.</param>
    /// <param name="duration">The call duration.</param>
    /// <param name="level">The log level (default is Information).</param>
    public static void LogExternalApiCall(this ILoggingService logger, string serviceName, string endpoint, string method, int statusCode, TimeSpan duration, LogLevel level = LogLevel.Information)
    {
        var context = new Dictionary<string, object>
        {
            { "ServiceName", serviceName },
            { "Endpoint", endpoint },
            { "HttpMethod", method },
            { "StatusCode", statusCode },
            { "Duration", duration.TotalMilliseconds },
            { "DurationUnit", "ms" }
        };

        logger.LogStructured(level, "External API call to {ServiceName} {Method} {Endpoint} responded {StatusCode} in {Duration}ms", context);
    }
}