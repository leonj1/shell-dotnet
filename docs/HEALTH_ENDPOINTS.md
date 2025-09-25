# Health Check Endpoints

The DotNetShell framework provides built-in health check endpoints that are automatically available to any application using the shell. These endpoints follow Kubernetes health check patterns and are essential for container orchestration and monitoring.

## Available Endpoints

### 1. General Health Check
- **Endpoint**: `/health`
- **Purpose**: Overall application health status
- **Returns**: Aggregated health status of all registered health checks
- **Use Case**: Load balancers, monitoring systems

### 2. Liveness Check
- **Endpoint**: `/health/live`
- **Purpose**: Indicates if the application is running
- **Returns**: Basic application lifecycle status
- **Use Case**: Kubernetes liveness probes - restarts container if unhealthy

### 3. Readiness Check
- **Endpoint**: `/health/ready`
- **Purpose**: Indicates if the application is ready to serve requests
- **Returns**: Status of dependencies and required services
- **Use Case**: Kubernetes readiness probes - removes from load balancer if not ready

### 4. Startup Check
- **Endpoint**: `/health/startup`
- **Purpose**: Indicates if the application has completed startup
- **Returns**: Initialization and startup status
- **Use Case**: Kubernetes startup probes - allows longer startup times

## Response Format

All health endpoints return a standardized JSON response:

```json
{
  "status": "Healthy|Degraded|Unhealthy",
  "totalDuration": "00:00:00.123",
  "entries": {
    "liveness": {
      "status": "Healthy",
      "description": "Application is alive and responding",
      "duration": "00:00:00.001",
      "data": {
        "timestamp": "2025-09-25T11:30:00Z",
        "status": "alive",
        "uptime": "00:05:30",
        "processId": 1234,
        "machineName": "hostname",
        "workingSet": 123456789
      }
    }
  }
}
```

## Implementation Details

The health checks are implemented in `/src/DotNetShell.Host/HealthChecks/`:
- `LivenessHealthCheck.cs` - Basic liveness implementation
- `ReadinessHealthCheck.cs` - Readiness with dependency checks
- `StartupHealthCheck.cs` - Startup completion checks
- `HealthCheckResponseWriter.cs` - Standardized response formatting

## Configuration

Health check endpoints can be customized via configuration:

```json
{
  "Shell": {
    "Services": {
      "HealthChecks": {
        "Enabled": true,
        "Endpoints": {
          "Liveness": "/health/live",
          "Readiness": "/health/ready",
          "Startup": "/health/startup"
        }
      }
    }
  }
}
```

## Module Health Integration

Modules implementing `IBusinessLogicModule` can provide their own health checks through the `CheckHealthAsync` method:

```csharp
public async Task<ModuleHealthResult> CheckHealthAsync(CancellationToken cancellationToken)
{
    return new ModuleHealthResult
    {
        Status = ModuleHealthStatus.Healthy,
        Description = "Module is functioning normally"
    };
}
```

## Docker/Kubernetes Integration

### Docker Health Check
```dockerfile
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1
```

### Kubernetes Probes
```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 5000
  initialDelaySeconds: 5
  periodSeconds: 10

readinessProbe:
  httpGet:
    path: /health/ready
    port: 5000
  initialDelaySeconds: 10
  periodSeconds: 5

startupProbe:
  httpGet:
    path: /health/startup
    port: 5000
  failureThreshold: 30
  periodSeconds: 10
```

## Testing Health Endpoints

```bash
# Test general health
curl http://localhost:5000/health

# Test liveness
curl http://localhost:5000/health/live

# Test readiness
curl http://localhost:5000/health/ready

# Test startup
curl http://localhost:5000/health/startup
```

## Benefits

1. **Automatic Availability**: No additional code needed - endpoints are automatically configured
2. **Container Orchestration Ready**: Compatible with Kubernetes, Docker Swarm, etc.
3. **Monitoring Integration**: Works with Prometheus, Grafana, and other monitoring tools
4. **Standardized Format**: Consistent JSON response format across all health checks
5. **Extensible**: Modules can add their own health check logic
6. **Configurable**: Endpoints and behavior can be customized via configuration

The health check infrastructure is a core feature of the DotNetShell framework, ensuring that applications built with it are production-ready and observable by default.