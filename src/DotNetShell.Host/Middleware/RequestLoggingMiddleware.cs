using System.Diagnostics;
using System.Text;

namespace DotNetShell.Host.Middleware;

/// <summary>
/// Middleware that logs HTTP requests and responses with timing and correlation information.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestLoggingMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware delegate.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>
    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task that represents the asynchronous invoke operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        // Skip logging for health check endpoints to reduce noise
        if (ShouldSkipLogging(context))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var requestInfo = await CaptureRequestInfoAsync(context);

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            await LogRequestAsync(context, requestInfo, stopwatch.ElapsedMilliseconds);
        }
    }

    private bool ShouldSkipLogging(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant();

        // Skip health check endpoints
        if (path?.StartsWith("/health") == true)
            return true;

        // Skip swagger endpoints in development
        if (path?.StartsWith("/swagger") == true)
            return true;

        // Skip static files
        if (path?.Contains(".") == true &&
            (path.EndsWith(".css") || path.EndsWith(".js") || path.EndsWith(".ico") || path.EndsWith(".png") || path.EndsWith(".jpg")))
            return true;

        return false;
    }

    private async Task<RequestInfo> CaptureRequestInfoAsync(HttpContext context)
    {
        var request = context.Request;
        var requestInfo = new RequestInfo
        {
            Method = request.Method,
            Path = request.Path,
            QueryString = request.QueryString.ToString(),
            UserAgent = request.Headers.UserAgent.ToString(),
            RemoteIpAddress = GetClientIpAddress(context),
            ContentType = request.ContentType,
            ContentLength = request.ContentLength,
            Scheme = request.Scheme,
            Host = request.Host.ToString(),
            TraceId = context.TraceIdentifier,
            UserId = GetUserId(context),
            Timestamp = DateTime.UtcNow
        };

        // Capture request body for POST/PUT requests (with size limit)
        if (ShouldCaptureRequestBody(request))
        {
            requestInfo.Body = await CaptureRequestBodyAsync(request);
        }

        return requestInfo;
    }

    private async Task LogRequestAsync(HttpContext context, RequestInfo requestInfo, long elapsedMs)
    {
        var response = context.Response;

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["TraceId"] = context.TraceIdentifier,
            ["UserId"] = requestInfo.UserId ?? "anonymous",
            ["RemoteIp"] = requestInfo.RemoteIpAddress ?? "unknown",
            ["UserAgent"] = requestInfo.UserAgent ?? "unknown"
        });

        var logLevel = GetLogLevel(response.StatusCode);

        _logger.Log(logLevel,
            "HTTP {Method} {Path}{QueryString} responded {StatusCode} in {ElapsedMs}ms - {ContentType} {ContentLength}bytes from {RemoteIp}",
            requestInfo.Method,
            requestInfo.Path,
            requestInfo.QueryString,
            response.StatusCode,
            elapsedMs,
            response.ContentType ?? "unknown",
            response.ContentLength ?? 0,
            requestInfo.RemoteIpAddress ?? "unknown");

        // Log additional details at debug level
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Request details: {@RequestInfo}", requestInfo);
        }

        // Log slow requests as warnings
        var slowRequestThreshold = _configuration.GetValue<int>("Shell:Logging:SlowRequestThresholdMs", 5000);
        if (elapsedMs > slowRequestThreshold)
        {
            _logger.LogWarning("Slow request detected: {Method} {Path} took {ElapsedMs}ms",
                requestInfo.Method, requestInfo.Path, elapsedMs);
        }
    }

    private string? GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded headers first (for reverse proxy scenarios)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }

    private string? GetUserId(HttpContext context)
    {
        return context.User?.Identity?.IsAuthenticated == true
            ? context.User.FindFirst("sub")?.Value ?? context.User.Identity.Name
            : null;
    }

    private bool ShouldCaptureRequestBody(HttpRequest request)
    {
        // Only capture body for POST, PUT, PATCH requests
        if (!string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.Method, "PUT", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.Method, "PATCH", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Don't capture large payloads
        var maxBodySize = _configuration.GetValue<long>("Shell:Logging:MaxRequestBodySize", 1024 * 16); // 16KB default
        if (request.ContentLength > maxBodySize)
        {
            return false;
        }

        // Only capture JSON and form data
        var contentType = request.ContentType?.ToLowerInvariant();
        return contentType?.Contains("application/json") == true ||
               contentType?.Contains("application/x-www-form-urlencoded") == true ||
               contentType?.Contains("text/") == true;
    }

    private async Task<string?> CaptureRequestBodyAsync(HttpRequest request)
    {
        try
        {
            request.EnableBuffering();

            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0; // Reset for next middleware

            return string.IsNullOrEmpty(body) ? null : body;
        }
        catch (Exception)
        {
            // If we can't read the body, don't fail the request
            return null;
        }
    }

    private static LogLevel GetLogLevel(int statusCode)
    {
        return statusCode switch
        {
            >= 400 and < 500 => LogLevel.Warning,
            >= 500 => LogLevel.Error,
            _ => LogLevel.Information
        };
    }
}

/// <summary>
/// Information about an HTTP request for logging purposes.
/// </summary>
public class RequestInfo
{
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? QueryString { get; set; }
    public string? UserAgent { get; set; }
    public string? RemoteIpAddress { get; set; }
    public string? ContentType { get; set; }
    public long? ContentLength { get; set; }
    public string Scheme { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? Body { get; set; }
    public DateTime Timestamp { get; set; }
}