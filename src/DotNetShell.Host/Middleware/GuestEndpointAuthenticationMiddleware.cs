using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetShell.Host.Middleware;

/// <summary>
/// Middleware that enforces JWT authentication for all guest module endpoints.
/// Excludes shell-specific endpoints like health checks and root endpoint.
/// </summary>
public class GuestEndpointAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GuestEndpointAuthenticationMiddleware> _logger;
    private readonly IConfiguration _configuration;
    private readonly string[] _excludedPaths;

    /// <summary>
    /// Initializes a new instance of the <see cref="GuestEndpointAuthenticationMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>
    public GuestEndpointAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<GuestEndpointAuthenticationMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        // Define paths that should be excluded from authentication
        _excludedPaths = new[]
        {
            "/health",
            "/health/live",
            "/health/ready",
            "/health/startup",
            "/swagger",
            "/",
            "/api/auth" // Authentication endpoints should be accessible without auth
        };
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLower();

        // Check if authentication is enabled
        var authEnabled = _configuration.GetValue<bool>("Shell:Services:Authentication:Enabled", false);
        var enforceForGuestEndpoints = _configuration.GetValue<bool>("Shell:Services:Authentication:EnforceForGuestEndpoints", true);

        if (!authEnabled || !enforceForGuestEndpoints)
        {
            // Authentication is disabled, proceed without checking
            await _next(context);
            return;
        }

        // Check if the path should be excluded from authentication
        if (IsPathExcluded(path))
        {
            await _next(context);
            return;
        }

        // Check if the endpoint has [AllowAnonymous] attribute
        var endpoint = context.GetEndpoint();
        if (endpoint != null)
        {
            var allowAnonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>();
            if (allowAnonymous != null)
            {
                await _next(context);
                return;
            }
        }

        // For all other endpoints (guest module endpoints), enforce authentication
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogWarning("Unauthenticated request to guest endpoint: {Path}", path);

            // Trigger authentication challenge
            await context.ChallengeAsync(JwtBearerDefaults.AuthenticationScheme);
            return;
        }

        // User is authenticated, proceed with the request
        _logger.LogDebug("Authenticated request to guest endpoint: {Path} by user: {User}", 
            path, context.User.Identity.Name);

        await _next(context);
    }

    /// <summary>
    /// Checks if a path should be excluded from authentication.
    /// </summary>
    /// <param name="path">The request path.</param>
    /// <returns>True if the path should be excluded; otherwise, false.</returns>
    private bool IsPathExcluded(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        // Check exact matches
        if (_excludedPaths.Contains(path))
            return true;

        // Check if path starts with any excluded path prefix
        if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check if path starts with /api/auth (authentication endpoints)
        if (path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase))
            return true;

        // Additional exclusions can be configured
        var additionalExclusions = _configuration.GetSection("Shell:Services:Authentication:ExcludedPaths")
            .Get<string[]>();

        if (additionalExclusions != null)
        {
            foreach (var exclusion in additionalExclusions)
            {
                if (path.Equals(exclusion, StringComparison.OrdinalIgnoreCase) ||
                    (exclusion.EndsWith("*") && path.StartsWith(exclusion.TrimEnd('*'), StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
        }

        return false;
    }
}

/// <summary>
/// Extension methods for adding the guest endpoint authentication middleware.
/// </summary>
public static class GuestEndpointAuthenticationMiddlewareExtensions
{
    /// <summary>
    /// Adds the guest endpoint authentication middleware to the application pipeline.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseGuestEndpointAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GuestEndpointAuthenticationMiddleware>();
    }
}