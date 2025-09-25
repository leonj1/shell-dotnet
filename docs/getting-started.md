# Getting Started with .NET Core Shell Framework

This guide will help you get up and running with the .NET Core Shell Framework quickly.

## Prerequisites

- .NET 9.0 SDK or later
- Visual Studio 2022 17.8+ / Visual Studio Code / JetBrains Rider
- Docker (optional, for containerized development)

## Quick Start

### 1. Clone the Repository

```bash
git clone https://github.com/your-org/shell-dotnet-core.git
cd shell-dotnet-core
```

### 2. Build the Solution

```bash
# Restore dependencies
dotnet restore

# Build the entire solution
dotnet build

# Run tests to verify everything works
dotnet test
```

### 3. Run the Shell Host

```bash
# Navigate to the host project
cd src/DotNetShell.Host

# Run the application
dotnet run
```

The application will start and be available at:
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001
- Swagger UI: https://localhost:5001/swagger

### 4. Verify Installation

Open your browser and navigate to:
- **Root endpoint**: https://localhost:5001/ - Should return application info
- **Health check**: https://localhost:5001/health/live - Should return "Healthy"
- **API documentation**: https://localhost:5001/swagger - Interactive API documentation

## Project Structure

```
shell-dotnet-core/
├── src/                          # Source code
│   ├── DotNetShell.Host/        # Main hosting application
│   ├── DotNetShell.Abstractions/ # Interface definitions
│   ├── DotNetShell.Core/        # Core implementations
│   ├── DotNetShell.Auth/        # Authentication services
│   ├── DotNetShell.Logging/     # Logging implementations
│   ├── DotNetShell.Telemetry/   # Telemetry services
│   └── DotNetShell.Extensions/  # Extension methods
├── samples/                      # Example implementations
│   └── SampleBusinessLogic/     # Sample business module
├── tests/                        # Test projects
│   ├── unit/                    # Unit tests
│   └── integration/             # Integration tests
├── docs/                         # Documentation
└── DotNetShell.sln              # Solution file
```

## Creating Your First Business Module

### 1. Create a New Project

```bash
# Create a new class library project
dotnet new classlib -n MyBusinessModule
cd MyBusinessModule

# Add reference to abstractions
dotnet add reference ../src/DotNetShell.Abstractions/DotNetShell.Abstractions.csproj
```

### 2. Implement the Module Interface

```csharp
using DotNetShell.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace MyBusinessModule;

public class MyBusinessModule : IBusinessLogicModule
{
    public string Name => "MyBusinessModule";
    public Version Version => new Version(1, 0, 0);
    public string Description => "My custom business logic module";

    public Task OnInitializeAsync(IServiceCollection services)
    {
        // Register your services here
        services.AddScoped<IMyService, MyService>();
        return Task.CompletedTask;
    }

    public Task OnConfigureAsync(IApplicationBuilder app)
    {
        // Configure middleware here
        return Task.CompletedTask;
    }

    public Task OnStartAsync(CancellationToken cancellationToken)
    {
        // Startup logic here
        return Task.CompletedTask;
    }

    public Task OnStopAsync(CancellationToken cancellationToken)
    {
        // Cleanup logic here
        return Task.CompletedTask;
    }
}
```

### 3. Create Your Services

```csharp
public interface IMyService
{
    Task<string> GetDataAsync();
}

public class MyService : IMyService
{
    public Task<string> GetDataAsync()
    {
        return Task.FromResult("Hello from MyBusinessModule!");
    }
}
```

### 4. Add Controllers (Optional)

```csharp
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class MyController : ControllerBase
{
    private readonly IMyService _myService;

    public MyController(IMyService myService)
    {
        _myService = myService;
    }

    [HttpGet]
    public async Task<ActionResult<string>> Get()
    {
        var data = await _myService.GetDataAsync();
        return Ok(data);
    }
}
```

### 5. Build and Deploy Your Module

```bash
# Build your module
dotnet build -c Release

# Copy the compiled DLL to the modules directory
cp bin/Release/net9.0/MyBusinessModule.dll ../modules/
```

### 6. Test Your Module

Restart the Shell Host and verify your module is loaded:

```bash
# Check logs for module loading messages
# Access your endpoints at https://localhost:5001/api/my
```

## Configuration

The framework uses a hierarchical configuration system:

### appsettings.json Structure

```json
{
  "Shell": {
    "Version": "1.0.0",
    "Modules": {
      "Source": "./modules",
      "AutoLoad": true
    },
    "Services": {
      "Authentication": {
        "Enabled": true,
        "DefaultProvider": "JWT"
      },
      "Logging": {
        "MinLevel": "Information"
      }
    }
  }
}
```

### Environment-Specific Configuration

- `appsettings.Development.json` - Development settings
- `appsettings.Production.json` - Production settings
- `appsettings.Local.json` - Local overrides (ignored by git)

### Environment Variables

Prefix environment variables with `SHELL_`:

```bash
export SHELL_Services__Authentication__Enabled=false
export SHELL_Modules__Source=/app/plugins
```

## Development Workflow

### Running in Development Mode

```bash
# Set development environment
export ASPNETCORE_ENVIRONMENT=Development

# Run with hot reload
dotnet watch run --project src/DotNetShell.Host
```

### Debugging

1. **Visual Studio**: Set `DotNetShell.Host` as startup project and press F5
2. **Visual Studio Code**: Use the provided launch configurations
3. **Command Line**: Use `dotnet run` with `--launch-profile` option

### Testing

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/unit/DotNetShell.Host.UnitTests/

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Next Steps

- [Module Development Guide](module-development.md)
- [Configuration Guide](configuration.md)
- [Security Guide](security.md)
- [Deployment Guide](deployment.md)
- [API Reference](api-reference.md)

## Troubleshooting

### Common Issues

**Issue**: Module not loading
- Check that the DLL is in the correct modules directory
- Verify the module implements `IBusinessLogicModule`
- Check logs for loading errors

**Issue**: Port already in use
- Change ports in `appsettings.json` under `Shell:Kestrel:Endpoints`
- Or use environment variables: `ASPNETCORE_URLS=https://localhost:6001`

**Issue**: Authentication not working
- Verify JWT secret is configured
- Check token format and expiration
- Review authentication middleware configuration

### Getting Help

- **Documentation**: Browse the [docs/](.) directory
- **Issues**: Create an issue on GitHub
- **Discussions**: Use GitHub Discussions for questions
- **Stack Overflow**: Tag questions with `dotnet-shell`

---

**Welcome to the .NET Core Shell Framework! Happy coding! 🚀**