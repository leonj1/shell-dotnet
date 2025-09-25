# Shell vs Minimal Host Comparison

## Overview

This document explains the difference between using the full DotNetShell.Host and creating a minimal host for demonstration purposes.

## Architecture Comparison

### Full DotNetShell.Host (Production Architecture)

The full shell provides a complete enterprise-ready hosting environment:

```
DotNetShell.Host
├── Built-in Features
│   ├── Health Endpoints (/health, /health/live, /health/ready, /health/startup)
│   ├── Swagger/OpenAPI Documentation
│   ├── Authentication & Authorization
│   ├── Serilog Logging
│   ├── Telemetry & Metrics
│   ├── Configuration Hot-reload
│   ├── Security Headers
│   ├── Request Logging
│   └── Global Exception Handling
│
└── Module Loading
    ├── Auto-discovery
    ├── Dependency Resolution
    ├── Lifecycle Management
    └── Health Check Aggregation
```

**Use Case**: Production deployments where modules are loaded dynamically

**Run Command**: `make run-shell`

### Minimal Host (Demo Only)

A simplified host created just to demonstrate a single module:

```
Minimal Host (Dockerfile.example)
├── Basic Features Only
│   ├── ASP.NET Core Controllers
│   ├── Swagger (manually added)
│   └── Module initialization
│
└── NO Built-in Shell Features
    ├── ❌ No health endpoints
    ├── ❌ No authentication
    ├── ❌ No Serilog
    ├── ❌ No telemetry
    └── ❌ No module hot-reload
```

**Use Case**: Quick demos and testing individual modules

**Run Command**: `make run-example`

## Feature Comparison

| Feature | Full Shell | Minimal Host |
|---------|------------|--------------|
| **Health Endpoints** | ✅ Automatic (`/health/*`) | ❌ Not included |
| **Swagger UI** | ✅ Built-in | 🔧 Manually added |
| **Logging** | ✅ Serilog with sinks | 📝 Console only |
| **Authentication** | ✅ JWT + custom providers | ❌ None |
| **Module Loading** | ✅ Dynamic discovery | 🔧 Hardcoded |
| **Configuration** | ✅ Hot-reload | ❌ Static |
| **Telemetry** | ✅ OpenTelemetry ready | ❌ None |
| **Security Headers** | ✅ Automatic | ❌ None |
| **Exception Handling** | ✅ Global middleware | ❌ Basic only |

## Code Differences

### Health Endpoints

**Full Shell** (Automatic):
```csharp
// In ShellStartup.cs - automatically configured
endpoints.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteResponse
});
```

**Minimal Host** (Not Available):
```csharp
// No health endpoints - would need manual configuration
// This is intentionally omitted to show the module doesn't provide them
```

### Module Loading

**Full Shell**:
```csharp
// Modules are discovered and loaded automatically
// from configured directories
var moduleLoader = services.GetRequiredService<IModuleLoader>();
await moduleLoader.LoadModulesAsync();
```

**Minimal Host**:
```csharp
// Module is manually instantiated and initialized
var sampleModule = new SampleModule();
await sampleModule.OnInitializeAsync(builder.Services);
```

## When to Use Each

### Use Full Shell (`make run-shell`) When:
- Deploying to production
- Need health checks for Kubernetes/Docker
- Require authentication and authorization
- Want proper logging and telemetry
- Building a microservices platform
- Need module hot-reload capability

### Use Minimal Host (`make run-example`) When:
- Quick module demonstration
- Testing module API endpoints
- Learning module development
- Debugging a specific module
- Creating documentation examples

## Migration Path

To migrate from minimal host to full shell:

1. **Remove manual configuration** - Delete health checks, Swagger setup
2. **Place module in modules directory** - Shell will auto-load it
3. **Configure via appsettings.json** - Not hardcoded in Program.cs
4. **Use shell's features** - Authentication, logging, telemetry
5. **Deploy with shell** - Use `DotNetShell.Host.dll` as entry point

## Best Practices

1. **Production = Full Shell** - Always use the full shell in production
2. **Modules shouldn't define infrastructure** - No health endpoints in modules
3. **Use interfaces** - Modules should depend on abstractions
4. **Shell provides, modules consume** - Infrastructure comes from shell
5. **Document requirements** - Clearly state if using full shell or minimal host

## Summary

- **Modules** should NEVER define health endpoints or infrastructure
- **Shell** provides ALL infrastructure including health endpoints
- **Minimal host** is only for quick demos, not production
- **Health endpoints** are a shell responsibility, available automatically
- **Production deployments** should always use the full DotNetShell.Host