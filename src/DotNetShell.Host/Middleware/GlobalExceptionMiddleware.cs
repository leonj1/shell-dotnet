using System.Net;
using System.Text.Json;

namespace DotNetShell.Host.Middleware;

/// <summary>
/// Global exception handling middleware that catches unhandled exceptions and returns standardized error responses.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlobalExceptionMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware delegate.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="environment">The web host environment.</param>
    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task that represents the asynchronous invoke operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred while processing the request");
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var errorResponse = new ErrorResponse
        {
            TraceId = context.TraceIdentifier,
            Timestamp = DateTime.UtcNow
        };

        switch (exception)
        {
            case ArgumentException argEx:
                errorResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.Error = "Bad Request";
                errorResponse.Message = argEx.Message;
                break;

            case UnauthorizedAccessException:
                errorResponse.StatusCode = (int)HttpStatusCode.Unauthorized;
                errorResponse.Error = "Unauthorized";
                errorResponse.Message = "Access denied";
                break;

            case NotImplementedException:
                errorResponse.StatusCode = (int)HttpStatusCode.NotImplemented;
                errorResponse.Error = "Not Implemented";
                errorResponse.Message = "This functionality is not yet implemented";
                break;

            case TimeoutException:
                errorResponse.StatusCode = (int)HttpStatusCode.RequestTimeout;
                errorResponse.Error = "Request Timeout";
                errorResponse.Message = "The request timed out";
                break;

            case InvalidOperationException invalidOpEx:
                errorResponse.StatusCode = (int)HttpStatusCode.Conflict;
                errorResponse.Error = "Conflict";
                errorResponse.Message = invalidOpEx.Message;
                break;

            default:
                errorResponse.StatusCode = (int)HttpStatusCode.InternalServerError;
                errorResponse.Error = "Internal Server Error";
                errorResponse.Message = _environment.IsDevelopment()
                    ? exception.Message
                    : "An error occurred while processing your request";
                break;
        }

        // Include stack trace in development
        if (_environment.IsDevelopment())
        {
            errorResponse.Details = exception.StackTrace;
        }

        context.Response.StatusCode = errorResponse.StatusCode;

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _environment.IsDevelopment(),
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        var jsonResponse = JsonSerializer.Serialize(errorResponse, jsonOptions);
        await context.Response.WriteAsync(jsonResponse);
    }
}

/// <summary>
/// Represents a standardized error response.
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Gets or sets the HTTP status code.
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Gets or sets the error type.
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional error details (only in development).
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Gets or sets the trace identifier.
    /// </summary>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the error occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }
}