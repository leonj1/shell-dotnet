using DotNetShell.Host;
using DotNetShell.Host.Startup;
using DotNetShell.Host.HealthChecks;
using DotNetShell.Core.Configuration;
using DotNetShell.Abstractions.Services;
using Serilog;
using System.Reflection;

// Configure early logging for startup
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting DotNet Shell Host application");

    // Create the host builder with enhanced configuration
    var builder = Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((context, config) =>
        {
            // Clear default configuration to use our custom configuration system
            config.Sources.Clear();

            // Use the Shell configuration builder
            var shellConfigBuilder = new ShellConfigurationBuilder(context.HostingEnvironment);
            shellConfigBuilder.AddDefaults(new ShellConfigurationOptions
            {
                CommandLineArgs = args,
                ReloadOnChange = true,
                ValidateOnStartup = true
            });

            // Add module configurations
            shellConfigBuilder.AddModuleConfigurations("./modules");

            // Add feature flags (using local file for development)
            if (context.HostingEnvironment.IsDevelopment())
            {
                shellConfigBuilder.AddFeatureFlags("./feature-flags.json");
            }

            // Build the configuration
            var configuration = shellConfigBuilder.Build();
            config.AddConfiguration(configuration);
        })
        .ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.UseStartup<ShellStartup>();
            webBuilder.ConfigureKestrel((context, serverOptions) =>
            {
                var kestrelSection = context.Configuration.GetSection("Shell:Kestrel");
                serverOptions.Configure(kestrelSection);

                // Configure HTTP/2 support
                serverOptions.ConfigureHttpsDefaults(httpsOptions =>
                {
                    httpsOptions.SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13;
                });

                // Configure limits from configuration
                var limitsSection = kestrelSection.GetSection("Limits");
                if (limitsSection.Exists())
                {
                    serverOptions.Limits.MaxConcurrentConnections = limitsSection.GetValue<int?>("MaxConcurrentConnections");
                    serverOptions.Limits.MaxConcurrentUpgradedConnections = limitsSection.GetValue<int?>("MaxConcurrentUpgradedConnections");
                    serverOptions.Limits.MaxRequestBodySize = limitsSection.GetValue<long?>("MaxRequestBodySize");

                    var requestTimeout = limitsSection.GetValue<string>("RequestHeadersTimeout");
                    if (TimeSpan.TryParse(requestTimeout, out var timeout))
                        serverOptions.Limits.RequestHeadersTimeout = timeout;

                    var drainTimeout = limitsSection.GetValue<string>("ResponseDrainTimeout");
                    if (TimeSpan.TryParse(drainTimeout, out var drainTimeoutValue))
                        serverOptions.Limits.ResponseDrainTimeout = drainTimeoutValue;
                }

                // Configure endpoints
                var endpointsSection = kestrelSection.GetSection("Endpoints");
                if (endpointsSection.Exists())
                {
                    var httpUrl = endpointsSection.GetValue<string>("Http:Url");
                    if (!string.IsNullOrEmpty(httpUrl) && Uri.TryCreate(httpUrl, UriKind.Absolute, out var httpUri))
                    {
                        serverOptions.Listen(System.Net.IPAddress.Any, httpUri.Port);
                    }

                    var httpsUrl = endpointsSection.GetValue<string>("Https:Url");
                    if (!string.IsNullOrEmpty(httpsUrl) && Uri.TryCreate(httpsUrl, UriKind.Absolute, out var httpsUri))
                    {
                        serverOptions.Listen(System.Net.IPAddress.Any, httpsUri.Port, listenOptions =>
                        {
                            listenOptions.UseHttps();
                            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
                        });
                    }
                }
            });
        })
        .UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .Enrich.WithProperty("ApplicationName", Assembly.GetExecutingAssembly().GetName().Name)
                .Enrich.WithProperty("ApplicationVersion", Assembly.GetExecutingAssembly().GetName().Version?.ToString());
        });

    // Build and run the host
    var host = builder.Build();

    Log.Information("Host built successfully, starting application");

    // Mark startup as complete for health checks
    StartupHealthCheck.MarkStartupComplete();
    Log.Information("Startup marked as complete");

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}