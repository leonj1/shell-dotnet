using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace DotNetShell.Host.HealthChecks;

/// <summary>
/// Custom health check response writer that provides detailed JSON responses.
/// </summary>
public static class HealthCheckResponseWriter
{
    /// <summary>
    /// Writes a detailed JSON health check response.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="report">The health check report.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    public static async Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var response = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            timestamp = DateTime.UtcNow,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                duration = entry.Value.Duration.TotalMilliseconds,
                description = entry.Value.Description,
                data = entry.Value.Data,
                exception = entry.Value.Exception?.Message,
                tags = entry.Value.Tags
            }).ToArray()
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}