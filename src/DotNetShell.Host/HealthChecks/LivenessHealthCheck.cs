using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetShell.Host.HealthChecks;

/// <summary>
/// Basic liveness health check that indicates the application is running.
/// </summary>
public class LivenessHealthCheck : IHealthCheck
{
    private readonly ILogger<LivenessHealthCheck> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LivenessHealthCheck"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public LivenessHealthCheck(ILogger<LivenessHealthCheck> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Runs the health check, returning the status of the component being checked.
    /// </summary>
    /// <param name="context">A context object associated with the current execution.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the health check.</param>
    /// <returns>A <see cref="Task{HealthCheckResult}"/> that completes when the health check has finished.</returns>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Simple liveness check - if we can execute this code, the application is alive
            var data = new Dictionary<string, object>
            {
                ["timestamp"] = DateTime.UtcNow,
                ["status"] = "alive",
                ["uptime"] = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime,
                ["processId"] = Environment.ProcessId,
                ["machineName"] = Environment.MachineName,
                ["workingSet"] = GC.GetTotalMemory(false)
            };

            _logger.LogDebug("Liveness health check completed successfully");

            return Task.FromResult(HealthCheckResult.Healthy("Application is alive and responding", data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Liveness health check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy("Liveness check failed", ex));
        }
    }
}