# SampleBusinessLogic Module

## Overview

The SampleBusinessLogic project is a reference implementation demonstrating how to create a plugin module for the DotNetShell framework. It showcases best practices for building modular, enterprise-grade .NET applications using the shell's plugin architecture.

## Purpose

This example serves multiple purposes:

1. **Learning Resource** - Provides a working example for developers new to the DotNetShell framework
2. **Template** - Can be used as a starting point for creating new business modules
3. **Best Practices** - Demonstrates proper implementation patterns including:
   - Module lifecycle management
   - Dependency injection
   - RESTful API design
   - Health checks
   - Repository pattern
   - Service layer architecture

## Project Structure

```
SampleBusinessLogic/
├── Controllers/
│   └── SampleController.cs      # REST API endpoints
├── Services/
│   ├── ISampleService.cs        # Service interface & models
│   └── SampleService.cs         # Business logic implementation
├── Repository/
│   ├── ISampleRepository.cs     # Repository interface
│   └── SampleRepository.cs      # Data access implementation
├── SampleModule.cs               # Module definition & lifecycle
└── SampleBusinessLogic.csproj   # Project configuration
```

## How It Works

### 1. Module Definition (`SampleModule.cs`)

The module implements `IBusinessLogicModule` which provides:

- **Module Metadata**: Name, version, description, author
- **Lifecycle Hooks**:
  - `ValidateAsync()` - Pre-flight validation
  - `OnInitializeAsync()` - Service registration
  - `OnConfigureAsync()` - Middleware configuration
  - `OnStartAsync()` - Module startup
  - `OnStopAsync()` - Graceful shutdown
  - `CheckHealthAsync()` - Health monitoring

### 2. Service Layer (`Services/`)

- **ISampleService**: Defines the business logic contract
- **SampleService**: Implements business operations
- **Models**: `SampleData` and `SampleResult` for data transfer

### 3. Repository Layer (`Repository/`)

- **ISampleRepository**: Data access abstraction
- **SampleRepository**: In-memory implementation (production would use database)

### 4. API Layer (`Controllers/`)

**SampleController** provides REST endpoints:
- `GET /api/sample/message` - Returns a sample message
- `POST /api/sample/process` - Processes sample data
- `GET /api/sample/health` - Module health status

## How to Run

### Option 1: Using Make (Recommended)

```bash
# From the project root directory
make run-example

# The API will be available at:
# http://localhost:5050
# http://localhost:5050/swagger
# http://localhost:5050/health

# To stop:
make stop-example
```

### Option 2: Using Docker Directly

```bash
# From the project root directory
docker build -f Dockerfile.example -t sample-module .
docker run -p 5050:5000 sample-module

# Access the API at http://localhost:5050
```

### Option 3: Using .NET CLI (Development)

```bash
# Build the module
dotnet build samples/SampleBusinessLogic/SampleBusinessLogic.csproj

# The module DLL can then be loaded by the Shell Host
# Place the compiled DLL in the Shell's modules directory
```

## Testing the API

### Basic Endpoints

```bash
# Get the main page
curl http://localhost:5050

# Get module information
curl http://localhost:5050/module-info

# Check health
curl http://localhost:5050/health
```

### Sample API Endpoints

```bash
# Get a sample message
curl http://localhost:5050/api/sample/message

# Process sample data
curl -X POST http://localhost:5050/api/sample/process \
  -H "Content-Type: application/json" \
  -d '{
    "id": "123",
    "name": "Test Data",
    "value": 42
  }'

# Check module health
curl http://localhost:5050/api/sample/health
```

### Using Swagger UI

Navigate to `http://localhost:5050/swagger` for interactive API documentation.

## Key Features Demonstrated

### 1. Dependency Injection
```csharp
services.AddScoped<ISampleService, SampleService>();
services.AddTransient<ISampleRepository, SampleRepository>();
```

### 2. Health Checks
```csharp
public Task<ModuleHealthResult> CheckHealthAsync(...)
{
    return new ModuleHealthResult
    {
        Status = ModuleHealthStatus.Healthy,
        Description = "Module is functioning normally"
    };
}
```

### 3. Configuration Management
```csharp
public Task OnConfigurationChangedAsync(IReadOnlyDictionary<string, object> newConfiguration, ...)
{
    // React to configuration changes at runtime
}
```

### 4. Proper Error Handling
```csharp
try
{
    var result = await _sampleService.ProcessDataAsync(data);
    return result.Success ? Ok(result) : BadRequest(result.Message);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to process data");
    return StatusCode(500, "An error occurred");
}
```

## Extending the Example

To create your own module based on this example:

1. **Copy the Structure**: Use this project as a template
2. **Rename Components**: Change namespace and class names
3. **Implement Your Logic**: Replace sample implementations with your business logic
4. **Update Module Metadata**: Modify name, version, description in your module class
5. **Add Dependencies**: Include any additional NuGet packages needed
6. **Register Services**: Add your services in `OnInitializeAsync()`

## Integration with Shell Host

When loaded by the DotNetShell.Host, this module:

1. Automatically registers its services with the DI container
2. Exposes its API endpoints through the host
3. Participates in the application lifecycle
4. Reports health status to monitoring systems
5. Can be hot-reloaded without stopping the host

## Production Considerations

For production use, consider:

1. **Database Integration**: Replace in-memory repository with actual database
2. **Authentication**: Implement proper authentication/authorization
3. **Validation**: Add comprehensive input validation
4. **Logging**: Enhance logging for production debugging
5. **Metrics**: Add telemetry and performance metrics
6. **Error Handling**: Implement global exception handling
7. **Configuration**: Use external configuration sources
8. **Security**: Apply security headers and CORS policies

## Troubleshooting

### Module Not Loading
- Ensure the module assembly is in the correct directory
- Check module validation in logs
- Verify all dependencies are available

### API Not Accessible
- Confirm the host is running on the correct port
- Check firewall settings
- Verify controller registration

### Health Check Failing
- Review module initialization logs
- Check service dependencies
- Ensure required configuration is present

## Additional Resources

- [DotNetShell Documentation](../../README.md)
- [Module Development Guide](../../docs/MODULE_DEVELOPMENT.md)
- [API Guidelines](../../docs/API_GUIDELINES.md)
- [Health Check Documentation](../../HEALTH_ENDPOINTS.md)

## License

This sample is provided as part of the DotNetShell framework under the MIT License.