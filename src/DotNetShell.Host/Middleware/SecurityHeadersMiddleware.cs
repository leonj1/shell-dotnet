namespace DotNetShell.Host.Middleware;

/// <summary>
/// Middleware that adds security headers to HTTP responses.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityHeadersMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware delegate.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="environment">The web host environment.</param>
    public SecurityHeadersMiddleware(
        RequestDelegate next,
        ILogger<SecurityHeadersMiddleware> logger,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task that represents the asynchronous invoke operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers before processing the request
        AddSecurityHeaders(context);

        await _next(context);

        // Log security header additions in debug mode
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Security headers added to response for {Path}", context.Request.Path);
        }
    }

    private void AddSecurityHeaders(HttpContext context)
    {
        var response = context.Response;

        // X-Content-Type-Options: Prevent MIME type sniffing
        if (!response.Headers.ContainsKey("X-Content-Type-Options"))
        {
            response.Headers.Append("X-Content-Type-Options", "nosniff");
        }

        // X-Frame-Options: Prevent clickjacking
        if (!response.Headers.ContainsKey("X-Frame-Options"))
        {
            var frameOptions = _configuration["Shell:Security:XFrameOptions"] ?? "DENY";
            response.Headers.Append("X-Frame-Options", frameOptions);
        }

        // X-XSS-Protection: Enable XSS filtering
        if (!response.Headers.ContainsKey("X-XSS-Protection"))
        {
            response.Headers.Append("X-XSS-Protection", "1; mode=block");
        }

        // Referrer-Policy: Control referrer information
        if (!response.Headers.ContainsKey("Referrer-Policy"))
        {
            var referrerPolicy = _configuration["Shell:Security:ReferrerPolicy"] ?? "strict-origin-when-cross-origin";
            response.Headers.Append("Referrer-Policy", referrerPolicy);
        }

        // Content-Security-Policy: Control resource loading
        if (!response.Headers.ContainsKey("Content-Security-Policy"))
        {
            var csp = BuildContentSecurityPolicy(context);
            if (!string.IsNullOrEmpty(csp))
            {
                response.Headers.Append("Content-Security-Policy", csp);
            }
        }

        // Strict-Transport-Security: Force HTTPS (only in production and over HTTPS)
        if (!response.Headers.ContainsKey("Strict-Transport-Security") &&
            !_environment.IsDevelopment() &&
            context.Request.IsHttps)
        {
            var hstsMaxAge = _configuration.GetValue<int>("Shell:Security:HstsMaxAgeSeconds", 31536000); // 1 year default
            var includeSubDomains = _configuration.GetValue<bool>("Shell:Security:HstsIncludeSubDomains", true);
            var preload = _configuration.GetValue<bool>("Shell:Security:HstsPreload", false);

            var hstsValue = $"max-age={hstsMaxAge}";
            if (includeSubDomains)
                hstsValue += "; includeSubDomains";
            if (preload)
                hstsValue += "; preload";

            response.Headers.Append("Strict-Transport-Security", hstsValue);
        }

        // Permissions-Policy: Control browser features
        if (!response.Headers.ContainsKey("Permissions-Policy"))
        {
            var permissionsPolicy = BuildPermissionsPolicy();
            if (!string.IsNullOrEmpty(permissionsPolicy))
            {
                response.Headers.Append("Permissions-Policy", permissionsPolicy);
            }
        }

        // Cross-Origin-Embedder-Policy and Cross-Origin-Opener-Policy for enhanced security
        if (!response.Headers.ContainsKey("Cross-Origin-Embedder-Policy"))
        {
            response.Headers.Append("Cross-Origin-Embedder-Policy", "require-corp");
        }

        if (!response.Headers.ContainsKey("Cross-Origin-Opener-Policy"))
        {
            response.Headers.Append("Cross-Origin-Opener-Policy", "same-origin");
        }

        // Server header removal or customization
        if (response.Headers.ContainsKey("Server"))
        {
            response.Headers.Remove("Server");
        }

        // Add custom server header if configured
        var customServer = _configuration["Shell:Security:ServerHeader"];
        if (!string.IsNullOrEmpty(customServer))
        {
            response.Headers.Append("Server", customServer);
        }

        // Add security contact header if configured
        var securityContact = _configuration["Shell:Security:ContactEmail"];
        if (!string.IsNullOrEmpty(securityContact))
        {
            response.Headers.Append("Security-Contact", securityContact);
        }

        // Add cache control headers for sensitive endpoints
        if (IsSensitiveEndpoint(context.Request.Path))
        {
            response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
            response.Headers.Append("Pragma", "no-cache");
            response.Headers.Append("Expires", "0");
        }
    }

    private string BuildContentSecurityPolicy(HttpContext context)
    {
        var cspConfig = _configuration.GetSection("Shell:Security:ContentSecurityPolicy");

        if (!cspConfig.Exists())
        {
            // Default CSP for API-only applications
            if (_environment.IsDevelopment())
            {
                // More relaxed CSP for development with Swagger UI
                return "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self';";
            }
            else
            {
                // Strict CSP for production API
                return "default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self'; connect-src 'self'; base-uri 'self'; form-action 'self';";
            }
        }

        // Build CSP from configuration
        var cspParts = new List<string>();

        foreach (var directive in cspConfig.GetChildren())
        {
            var values = directive.Get<string[]>();
            if (values?.Any() == true)
            {
                cspParts.Add($"{directive.Key} {string.Join(" ", values)}");
            }
        }

        return string.Join("; ", cspParts);
    }

    private string BuildPermissionsPolicy()
    {
        var permissionsConfig = _configuration.GetSection("Shell:Security:PermissionsPolicy");

        if (!permissionsConfig.Exists())
        {
            // Default permissions policy for API applications
            return "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
        }

        var policies = new List<string>();

        foreach (var policy in permissionsConfig.GetChildren())
        {
            var allowlist = policy.Get<string[]>();
            var allowlistStr = allowlist?.Any() == true ? $"({string.Join(" ", allowlist)})" : "()";
            policies.Add($"{policy.Key}={allowlistStr}");
        }

        return string.Join(", ", policies);
    }

    private bool IsSensitiveEndpoint(string path)
    {
        var sensitiveEndpoints = new[]
        {
            "/auth",
            "/login",
            "/token",
            "/refresh",
            "/admin",
            "/health"
        };

        return sensitiveEndpoints.Any(endpoint =>
            path.StartsWith(endpoint, StringComparison.OrdinalIgnoreCase));
    }
}