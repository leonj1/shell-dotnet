# .NET Core Shell - Usage Examples & Scenarios

## Quick Start Guide

### For Business Logic Developers

#### 1. Basic API Module

**Scenario**: Create a simple product catalog API that uses the shell's infrastructure.

```bash
# Create new module project
dotnet new classlib -n ProductCatalog.Module -f net9.0
cd ProductCatalog.Module

# Add shell abstractions reference
dotnet add package DotNetShell.Abstractions --version 1.0.0
```

**Module Implementation**:

```csharp
// ProductCatalogModule.cs
using DotNetShell.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ProductCatalog.Module;

public class ProductCatalogModule : IBusinessLogicModule
{
    public async Task OnInitializeAsync(IServiceCollection services)
    {
        // Register your services
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductRepository, ProductRepository>();

        // Add your controllers
        services.AddControllers()
            .AddApplicationPart(typeof(ProductCatalogModule).Assembly);

        // Configure your specific needs
        services.AddAutoMapper(typeof(ProductCatalogModule).Assembly);

        await Task.CompletedTask;
    }

    public async Task OnConfigureAsync(IApplicationBuilder app)
    {
        // Configure middleware specific to your module
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });

        await Task.CompletedTask;
    }

    public async Task OnStartAsync(CancellationToken cancellationToken)
    {
        // Perform startup tasks (e.g., warm-up cache, validate connections)
        Console.WriteLine("ProductCatalog module started successfully");
        await Task.CompletedTask;
    }

    public async Task OnStopAsync(CancellationToken cancellationToken)
    {
        // Cleanup resources
        Console.WriteLine("ProductCatalog module shutting down");
        await Task.CompletedTask;
    }
}
```

**Controller Example**:

```csharp
// Controllers/ProductController.cs
using DotNetShell.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace ProductCatalog.Module.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly ILoggingService _logger;
    private readonly ITelemetryService _telemetry;
    private readonly ICacheService _cache;

    public ProductController(
        IProductService productService,
        ILoggingService logger,
        ITelemetryService telemetry,
        ICacheService cache)
    {
        _productService = productService;
        _logger = logger;
        _telemetry = telemetry;
        _cache = cache;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetProducts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        using var activity = _telemetry.StartActivity("GetProducts");

        try
        {
            // Check cache first
            var cacheKey = $"products:{page}:{pageSize}";
            var cached = await _cache.GetAsync<IEnumerable<ProductDto>>(cacheKey);
            if (cached != null)
            {
                _telemetry.RecordMetric("cache.hit", 1, new() { ["key"] = cacheKey });
                return Ok(cached);
            }

            // Get from service
            _logger.LogInfo("Fetching products - Page: {Page}, Size: {PageSize}", page, pageSize);
            var products = await _productService.GetProductsAsync(page, pageSize);

            // Cache the result
            await _cache.SetAsync(cacheKey, products, TimeSpan.FromMinutes(5));

            _telemetry.RecordMetric("products.fetched", products.Count());
            return Ok(products);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching products");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return StatusCode(500, "An error occurred while fetching products");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProductDto>> GetProduct(int id)
    {
        using var timer = _telemetry.StartTimer("product.fetch.duration");

        var product = await _productService.GetProductAsync(id);
        if (product == null)
        {
            _logger.LogWarning("Product not found: {ProductId}", id);
            return NotFound();
        }

        return Ok(product);
    }

    [HttpPost]
    [Authorize(Policy = "ProductAdmin")]
    public async Task<ActionResult<ProductDto>> CreateProduct(CreateProductDto dto)
    {
        _logger.LogInfo("Creating new product: {ProductName}", dto.Name);

        try
        {
            var product = await _productService.CreateProductAsync(dto);

            _telemetry.RecordEvent("product.created", new()
            {
                ["productId"] = product.Id,
                ["productName"] = product.Name
            });

            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Product validation failed: {Errors}", ex.Errors);
            return BadRequest(ex.Errors);
        }
    }
}
```

**Local Development**:

```bash
# Clone and run the shell locally
git clone https://github.com/company/dotnet-shell.git
cd dotnet-shell

# Create modules directory
mkdir modules

# Copy your module
cp ../ProductCatalog.Module/bin/Debug/net9.0/ProductCatalog.Module.dll ./modules/

# Configure the shell (appsettings.Development.json)
{
  "Shell": {
    "Modules": {
      "Sources": [
        {
          "Type": "Directory",
          "Path": "./modules"
        }
      ]
    }
  }
}

# Run the shell
dotnet run --project src/DotNetShell.Host
```

#### 2. Background Service Module

**Scenario**: Create a background service for order processing.

```csharp
// OrderProcessingModule.cs
using DotNetShell.Abstractions;

namespace OrderProcessing.Module;

public class OrderProcessingModule : IBusinessLogicModule
{
    public async Task OnInitializeAsync(IServiceCollection services)
    {
        // Register background service
        services.AddHostedService<OrderProcessingService>();

        // Register dependencies
        services.AddScoped<IOrderProcessor, OrderProcessor>();
        services.AddScoped<IOrderRepository, OrderRepository>();

        // Add message bus handlers
        services.AddScoped<IMessageHandler<OrderCreatedEvent>, OrderCreatedHandler>();

        await Task.CompletedTask;
    }

    public async Task OnConfigureAsync(IApplicationBuilder app)
    {
        // Background services don't typically need middleware
        await Task.CompletedTask;
    }

    public async Task OnStartAsync(CancellationToken cancellationToken)
    {
        // Subscribe to message bus events
        var messageBus = app.ApplicationServices.GetRequiredService<IMessageBus>();
        await messageBus.SubscribeAsync<OrderCreatedEvent>(
            "order.created",
            cancellationToken);
    }

    public async Task OnStopAsync(CancellationToken cancellationToken)
    {
        // Unsubscribe from events
        var messageBus = app.ApplicationServices.GetRequiredService<IMessageBus>();
        await messageBus.UnsubscribeAsync("order.created", cancellationToken);
    }
}

// OrderProcessingService.cs
public class OrderProcessingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILoggingService _logger;
    private readonly ITelemetryService _telemetry;

    public OrderProcessingService(
        IServiceProvider serviceProvider,
        ILoggingService logger,
        ITelemetryService telemetry)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _telemetry = telemetry;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInfo("Order processing service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            using var activity = _telemetry.StartActivity("ProcessOrders");

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IOrderProcessor>();

                var ordersProcessed = await processor.ProcessPendingOrdersAsync(stoppingToken);

                _telemetry.RecordMetric("orders.processed", ordersProcessed);

                // Wait before next iteration
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing orders");
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                // Back off on error
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
```

#### 3. Event-Driven Module

**Scenario**: Create a module that responds to events from other modules.

```csharp
// NotificationModule.cs
using DotNetShell.Abstractions;

namespace Notification.Module;

public class NotificationModule : IBusinessLogicModule
{
    public async Task OnInitializeAsync(IServiceCollection services)
    {
        // Register notification services
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<ISmsService, SmsService>();
        services.AddScoped<IPushNotificationService, PushNotificationService>();

        // Register event handlers
        services.AddScoped<IEventHandler<OrderCompletedEvent>, OrderCompletedNotificationHandler>();
        services.AddScoped<IEventHandler<UserRegisteredEvent>, WelcomeEmailHandler>();

        await Task.CompletedTask;
    }

    public async Task OnConfigureAsync(IApplicationBuilder app)
    {
        // Configure event subscriptions
        var eventBus = app.ApplicationServices.GetRequiredService<IEventBus>();

        await eventBus.SubscribeAsync<OrderCompletedEvent>(
            async (evt) =>
            {
                using var scope = app.ApplicationServices.CreateScope();
                var handler = scope.ServiceProvider
                    .GetRequiredService<IEventHandler<OrderCompletedEvent>>();
                await handler.HandleAsync(evt);
            });

        await eventBus.SubscribeAsync<UserRegisteredEvent>(
            async (evt) =>
            {
                using var scope = app.ApplicationServices.CreateScope();
                var handler = scope.ServiceProvider
                    .GetRequiredService<IEventHandler<UserRegisteredEvent>>();
                await handler.HandleAsync(evt);
            });
    }
}

// OrderCompletedNotificationHandler.cs
public class OrderCompletedNotificationHandler : IEventHandler<OrderCompletedEvent>
{
    private readonly IEmailService _emailService;
    private readonly ILoggingService _logger;
    private readonly ITelemetryService _telemetry;

    public OrderCompletedNotificationHandler(
        IEmailService emailService,
        ILoggingService logger,
        ITelemetryService telemetry)
    {
        _emailService = emailService;
        _logger = logger;
        _telemetry = telemetry;
    }

    public async Task HandleAsync(OrderCompletedEvent evt)
    {
        using var span = _telemetry.StartSpan("SendOrderCompletedNotification");

        _logger.LogInfo("Sending order completed notification for order {OrderId}", evt.OrderId);

        try
        {
            await _emailService.SendAsync(new EmailMessage
            {
                To = evt.CustomerEmail,
                Subject = $"Order {evt.OrderId} Completed",
                Body = GenerateEmailBody(evt),
                Template = "OrderCompleted"
            });

            _telemetry.RecordEvent("notification.sent", new()
            {
                ["type"] = "email",
                ["event"] = "order.completed",
                ["orderId"] = evt.OrderId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send order completed notification");
            span.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
```

### For Platform/DevOps Teams

#### 1. Shell Deployment with Docker

```dockerfile
# Dockerfile for shell with modules
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "DotNetShell.Host/DotNetShell.Host.csproj"
RUN dotnet build "DotNetShell.Host/DotNetShell.Host.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DotNetShell.Host/DotNetShell.Host.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create modules directory
RUN mkdir -p /app/modules

# Install tools for module management
RUN apt-get update && apt-get install -y curl jq

# Health check
HEALTHCHECK --interval=30s --timeout=3s \
    CMD curl -f http://localhost/health/ready || exit 1

ENTRYPOINT ["dotnet", "DotNetShell.Host.dll"]
```

**Docker Compose Setup**:

```yaml
# docker-compose.yml
version: '3.8'

services:
  shell:
    build: .
    ports:
      - "5000:80"
      - "5001:443"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - Shell__Modules__Source=/modules
      - Shell__Authentication__JWT__Secret=${JWT_SECRET}
      - ConnectionStrings__Default=${DB_CONNECTION}
    volumes:
      - ./modules:/modules:ro
      - ./config:/app/config:ro
    depends_on:
      - redis
      - postgres
      - jaeger

  redis:
    image: redis:alpine
    ports:
      - "6379:6379"
    volumes:
      - redis_data:/data

  postgres:
    image: postgres:15
    environment:
      - POSTGRES_DB=shelldb
      - POSTGRES_USER=shell
      - POSTGRES_PASSWORD=${DB_PASSWORD}
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

  jaeger:
    image: jaegertracing/all-in-one:latest
    ports:
      - "5775:5775/udp"
      - "6831:6831/udp"
      - "6832:6832/udp"
      - "5778:5778"
      - "16686:16686"
      - "14268:14268"
      - "14250:14250"
      - "9411:9411"

  prometheus:
    image: prom/prometheus
    ports:
      - "9090:9090"
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus_data:/prometheus
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'

  grafana:
    image: grafana/grafana
    ports:
      - "3000:3000"
    volumes:
      - grafana_data:/var/lib/grafana
      - ./grafana/dashboards:/etc/grafana/provisioning/dashboards
      - ./grafana/datasources:/etc/grafana/provisioning/datasources

volumes:
  redis_data:
  postgres_data:
  prometheus_data:
  grafana_data:
```

#### 2. Kubernetes Deployment

```yaml
# shell-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: dotnet-shell
  namespace: production
  labels:
    app: dotnet-shell
    version: v1
spec:
  replicas: 3
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 1
      maxUnavailable: 0
  selector:
    matchLabels:
      app: dotnet-shell
  template:
    metadata:
      labels:
        app: dotnet-shell
        version: v1
      annotations:
        prometheus.io/scrape: "true"
        prometheus.io/port: "9090"
    spec:
      serviceAccountName: dotnet-shell

      initContainers:
      - name: module-downloader
        image: busybox
        command: ['sh', '-c']
        args:
        - |
          echo "Downloading modules..."
          wget -O /modules/module1.dll https://artifacts.company.com/modules/module1.dll
          wget -O /modules/module2.dll https://artifacts.company.com/modules/module2.dll
          echo "Modules downloaded successfully"
        volumeMounts:
        - name: modules
          mountPath: /modules

      containers:
      - name: shell
        image: company.azurecr.io/dotnet-shell:latest
        ports:
        - containerPort: 80
          name: http
        - containerPort: 443
          name: https
        - containerPort: 9090
          name: metrics

        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: Shell__Modules__Source
          value: "/modules"
        - name: Shell__Authentication__JWT__Authority
          valueFrom:
            configMapKeyRef:
              name: shell-config
              key: jwt.authority
        - name: Shell__Authentication__JWT__Audience
          valueFrom:
            configMapKeyRef:
              name: shell-config
              key: jwt.audience
        - name: ConnectionStrings__Default
          valueFrom:
            secretKeyRef:
              name: shell-secrets
              key: db.connection
        - name: Shell__Redis__ConnectionString
          valueFrom:
            secretKeyRef:
              name: shell-secrets
              key: redis.connection

        volumeMounts:
        - name: modules
          mountPath: /modules
          readOnly: true
        - name: config
          mountPath: /app/config
          readOnly: true
        - name: secrets
          mountPath: /app/secrets
          readOnly: true

        livenessProbe:
          httpGet:
            path: /health/live
            port: 80
          initialDelaySeconds: 5
          periodSeconds: 10
          timeoutSeconds: 3

        readinessProbe:
          httpGet:
            path: /health/ready
            port: 80
          initialDelaySeconds: 10
          periodSeconds: 5
          timeoutSeconds: 3

        startupProbe:
          httpGet:
            path: /health/startup
            port: 80
          initialDelaySeconds: 0
          periodSeconds: 5
          timeoutSeconds: 3
          failureThreshold: 30

        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "1Gi"
            cpu: "1000m"

      volumes:
      - name: modules
        emptyDir: {}
      - name: config
        configMap:
          name: shell-config
      - name: secrets
        secret:
          secretName: shell-secrets

---
apiVersion: v1
kind: Service
metadata:
  name: dotnet-shell
  namespace: production
spec:
  type: ClusterIP
  ports:
  - port: 80
    targetPort: 80
    protocol: TCP
    name: http
  - port: 443
    targetPort: 443
    protocol: TCP
    name: https
  selector:
    app: dotnet-shell

---
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: dotnet-shell-hpa
  namespace: production
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: dotnet-shell
  minReplicas: 3
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
  behavior:
    scaleDown:
      stabilizationWindowSeconds: 300
      policies:
      - type: Percent
        value: 10
        periodSeconds: 60
    scaleUp:
      stabilizationWindowSeconds: 0
      policies:
      - type: Percent
        value: 100
        periodSeconds: 60
```

#### 3. CI/CD Pipeline

**GitHub Actions Workflow**:

```yaml
# .github/workflows/shell-ci-cd.yml
name: Shell CI/CD

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]
  release:
    types: [published]

env:
  DOTNET_VERSION: '9.0'
  REGISTRY: company.azurecr.io
  IMAGE_NAME: dotnet-shell

jobs:
  build-and-test:
    runs-on: ubuntu-latest

    steps:
    - uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}

    - name: Restore dependencies
      run: dotnet restore

    - name: Build
      run: dotnet build --no-restore --configuration Release

    - name: Run tests
      run: dotnet test --no-build --configuration Release --verbosity normal --collect:"XPlat Code Coverage"

    - name: Upload coverage
      uses: codecov/codecov-action@v3
      with:
        files: ./**/coverage.cobertura.xml

    - name: Run security scan
      run: |
        dotnet tool install --global security-scan
        security-scan ./src

    - name: SonarCloud Scan
      uses: SonarSource/sonarcloud-github-action@master
      env:
        GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        SONAR_TOKEN: ${{ secrets.SONAR_TOKEN }}

    - name: Package
      run: dotnet publish ./src/DotNetShell.Host/DotNetShell.Host.csproj -c Release -o ./publish

    - name: Upload artifacts
      uses: actions/upload-artifact@v3
      with:
        name: shell-artifacts
        path: ./publish

  docker-build:
    needs: build-and-test
    runs-on: ubuntu-latest
    if: github.event_name != 'pull_request'

    steps:
    - uses: actions/checkout@v3

    - name: Download artifacts
      uses: actions/download-artifact@v3
      with:
        name: shell-artifacts
        path: ./publish

    - name: Set up Docker Buildx
      uses: docker/setup-buildx-action@v2

    - name: Log in to registry
      uses: docker/login-action@v2
      with:
        registry: ${{ env.REGISTRY }}
        username: ${{ secrets.AZURE_CLIENT_ID }}
        password: ${{ secrets.AZURE_CLIENT_SECRET }}

    - name: Extract metadata
      id: meta
      uses: docker/metadata-action@v4
      with:
        images: ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}
        tags: |
          type=ref,event=branch
          type=ref,event=pr
          type=semver,pattern={{version}}
          type=semver,pattern={{major}}.{{minor}}
          type=sha

    - name: Build and push Docker image
      uses: docker/build-push-action@v4
      with:
        context: .
        push: true
        tags: ${{ steps.meta.outputs.tags }}
        labels: ${{ steps.meta.outputs.labels }}
        cache-from: type=gha
        cache-to: type=gha,mode=max

  deploy-staging:
    needs: docker-build
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/develop'
    environment: staging

    steps:
    - name: Deploy to Kubernetes
      uses: azure/k8s-deploy@v4
      with:
        manifests: |
          k8s/shell-deployment.yaml
          k8s/shell-service.yaml
        images: |
          ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:develop
        namespace: staging

  deploy-production:
    needs: docker-build
    runs-on: ubuntu-latest
    if: github.event_name == 'release'
    environment: production

    steps:
    - name: Deploy to Kubernetes
      uses: azure/k8s-deploy@v4
      with:
        manifests: |
          k8s/shell-deployment.yaml
          k8s/shell-service.yaml
        images: |
          ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:${{ github.event.release.tag_name }}
        namespace: production
```

### Advanced Scenarios

#### 1. Multi-Tenant Module

```csharp
// MultiTenantModule.cs
public class MultiTenantModule : IBusinessLogicModule
{
    public async Task OnInitializeAsync(IServiceCollection services)
    {
        // Add multi-tenancy support
        services.AddMultiTenant<TenantInfo>()
            .WithStrategy<HostStrategy>()
            .WithStore<DatabaseTenantStore>();

        // Register tenant-scoped services
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ITenantDataService, TenantDataService>();

        // Configure tenant isolation
        services.AddDbContext<TenantDbContext>((serviceProvider, options) =>
        {
            var tenantContext = serviceProvider.GetRequiredService<ITenantContext>();
            var connectionString = GetTenantConnectionString(tenantContext.TenantId);
            options.UseSqlServer(connectionString);
        });
    }
}
```

#### 2. GraphQL Module

```csharp
// GraphQLModule.cs
public class GraphQLModule : IBusinessLogicModule
{
    public async Task OnInitializeAsync(IServiceCollection services)
    {
        services
            .AddGraphQLServer()
            .AddQueryType<Query>()
            .AddMutationType<Mutation>()
            .AddSubscriptionType<Subscription>()
            .AddAuthorization()
            .AddProjections()
            .AddFiltering()
            .AddSorting()
            .AddDataLoader()
            .AddDiagnosticEventListener<GraphQLDiagnosticEventListener>();
    }

    public async Task OnConfigureAsync(IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGraphQL();
            endpoints.MapGraphQLPlayground();
        });
    }
}
```

#### 3. Real-time Module with SignalR

```csharp
// RealTimeModule.cs
public class RealTimeModule : IBusinessLogicModule
{
    public async Task OnInitializeAsync(IServiceCollection services)
    {
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = true;
        })
        .AddJsonProtocol()
        .AddMessagePackProtocol()
        .AddStackExchangeRedis(Configuration.GetConnectionString("Redis"));

        services.AddScoped<INotificationHub, NotificationHub>();
    }

    public async Task OnConfigureAsync(IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapHub<NotificationHub>("/hubs/notifications");
            endpoints.MapHub<ChatHub>("/hubs/chat");
        });
    }
}
```

## Monitoring & Observability Examples

### Grafana Dashboard Configuration

```json
{
  "dashboard": {
    "title": "DotNet Shell Monitoring",
    "panels": [
      {
        "title": "Request Rate",
        "targets": [
          {
            "expr": "rate(http_requests_total[5m])"
          }
        ]
      },
      {
        "title": "Response Time P95",
        "targets": [
          {
            "expr": "histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))"
          }
        ]
      },
      {
        "title": "Module Load Time",
        "targets": [
          {
            "expr": "module_load_duration_seconds"
          }
        ]
      },
      {
        "title": "Active Modules",
        "targets": [
          {
            "expr": "shell_active_modules"
          }
        ]
      }
    ]
  }
}
```

### Alert Rules

```yaml
# prometheus-alerts.yml
groups:
- name: shell-alerts
  rules:
  - alert: HighErrorRate
    expr: rate(http_requests_total{status=~"5.."}[5m]) > 0.05
    for: 5m
    labels:
      severity: critical
    annotations:
      summary: High error rate detected
      description: "Error rate is {{ $value }}% for {{ $labels.instance }}"

  - alert: ModuleLoadFailure
    expr: increase(module_load_failures_total[5m]) > 0
    for: 1m
    labels:
      severity: warning
    annotations:
      summary: Module load failure detected
      description: "Module {{ $labels.module }} failed to load"

  - alert: HighMemoryUsage
    expr: process_resident_memory_bytes / 1024 / 1024 > 1000
    for: 10m
    labels:
      severity: warning
    annotations:
      summary: High memory usage
      description: "Memory usage is {{ $value }}MB"
```

## Troubleshooting Guide

### Common Issues and Solutions

#### Module Not Loading

```bash
# Check module discovery
curl http://localhost/api/admin/modules

# Check logs
docker logs shell-container | grep -i "module"

# Validate module assembly
dotnet ./modules/MyModule.dll --info

# Check permissions
ls -la ./modules/
```

#### Authentication Issues

```bash
# Test authentication endpoint
curl -X POST http://localhost/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{"username":"test","password":"test"}'

# Validate JWT token
curl http://localhost/api/auth/validate \
  -H "Authorization: Bearer YOUR_TOKEN"

# Check authentication configuration
curl http://localhost/api/admin/config/authentication
```

#### Performance Issues

```bash
# Get performance metrics
curl http://localhost/metrics

# Enable detailed tracing
export Shell__Telemetry__DetailedTracing=true

# Profile the application
dotnet-trace collect -p $(pidof dotnet) --duration 00:00:30

# Analyze memory usage
dotnet-dump collect -p $(pidof dotnet)
dotnet-dump analyze core_dump
```

## Migration Guide

### From Monolithic Application

1. **Extract Business Logic**
   ```bash
   # Create new module project from existing code
   dotnet new classlib -n LegacyApp.Module

   # Move business logic files
   mv ../LegacyApp/Services/* ./Services/
   mv ../LegacyApp/Controllers/* ./Controllers/
   ```

2. **Replace Infrastructure**
   ```csharp
   // Before (Direct implementation)
   ILogger<MyService> logger = loggerFactory.CreateLogger<MyService>();

   // After (Using shell abstraction)
   ILoggingService logger = serviceProvider.GetRequiredService<ILoggingService>();
   ```

3. **Update Configuration**
   ```json
   // Move from appsettings.json to module configuration
   {
     "Modules": {
       "LegacyApp": {
         "ConnectionString": "...",
         "ApiSettings": {
           "Timeout": 30
         }
       }
     }
   }
   ```

### From Microservices

1. **Consolidate Services**
   ```bash
   # Create modules for each microservice
   dotnet new classlib -n OrderService.Module
   dotnet new classlib -n PaymentService.Module
   dotnet new classlib -n InventoryService.Module
   ```

2. **Replace Inter-Service Communication**
   ```csharp
   // Before (HTTP calls between services)
   var response = await httpClient.GetAsync("http://order-service/api/orders");

   // After (Direct method calls or events)
   var orders = await orderService.GetOrdersAsync();
   // Or use event bus
   await eventBus.PublishAsync(new OrderCreatedEvent { ... });
   ```

3. **Unified Deployment**
   ```yaml
   # Single deployment instead of multiple
   kubectl apply -f shell-with-all-modules.yaml
   ```

## Performance Optimization Tips

### 1. Module Loading Optimization

```csharp
// Lazy load modules
public class LazyModuleLoader
{
    private readonly Dictionary<string, Lazy<IBusinessLogicModule>> _modules;

    public async Task<IBusinessLogicModule> GetModuleAsync(string name)
    {
        if (_modules.TryGetValue(name, out var lazyModule))
        {
            return lazyModule.Value;
        }
        return null;
    }
}
```

### 2. Response Caching

```csharp
// Add response caching to frequently accessed endpoints
[HttpGet]
[ResponseCache(Duration = 300, Location = ResponseCacheLocation.Client)]
public async Task<IActionResult> GetCatalog()
{
    // Implementation
}
```

### 3. Database Connection Pooling

```json
{
  "ConnectionStrings": {
    "Default": "...;Min Pool Size=10;Max Pool Size=100;Connection Lifetime=300"
  }
}
```

## Security Best Practices

### 1. Module Isolation

```csharp
// Implement module sandboxing
public class ModuleSandbox
{
    public async Task<T> ExecuteInSandboxAsync<T>(Func<Task<T>> action)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            return await Task.Run(action, cts.Token);
        }
        catch (Exception ex)
        {
            // Log and handle sandbox violations
            throw new SandboxViolationException("Module exceeded execution limits", ex);
        }
    }
}
```

### 2. Input Validation

```csharp
// Always validate module inputs
public class ModuleInputValidator
{
    public async Task ValidateAsync<T>(T input) where T : class
    {
        var validator = new DataAnnotationsValidator();
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(input, new ValidationContext(input), results, true))
        {
            throw new ValidationException(results);
        }
    }
}
```

### 3. Secret Management

```bash
# Use environment variables or secret stores
export Shell__Authentication__JWT__Secret=$(az keyvault secret show --name jwt-secret --vault-name myvault --query value -o tsv)
```

## Conclusion

These examples demonstrate the versatility and power of the .NET Core Shell architecture. Whether you're building simple APIs, complex event-driven systems, or migrating existing applications, the shell provides a robust foundation with enterprise-grade infrastructure out of the box.