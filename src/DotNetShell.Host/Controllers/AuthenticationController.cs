using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace DotNetShell.Host.Controllers;

/// <summary>
/// Handles authentication and JWT token generation.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthenticationController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthenticationController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationController"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="logger">The logger.</param>
    public AuthenticationController(
        IConfiguration configuration,
        ILogger<AuthenticationController> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Authenticates a user and returns a JWT token.
    /// </summary>
    /// <param name="request">The login request.</param>
    /// <returns>A JWT token if authentication is successful.</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthenticationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthenticationResponse>> Login([FromBody] LoginRequest request)
    {
        if (request == null)
        {
            return BadRequest("Invalid login request");
        }

        if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
        {
            return BadRequest("Username and password are required");
        }

        _logger.LogInformation("Login attempt for user: {Username}", request.Username);

        // TODO: Replace this with actual user authentication logic
        // This is just a demo implementation
        bool isValidUser = await ValidateUserAsync(request.Username, request.Password);

        if (!isValidUser)
        {
            _logger.LogWarning("Failed login attempt for user: {Username}", request.Username);
            return Unauthorized("Invalid username or password");
        }

        // Generate JWT token
        var token = GenerateJwtToken(request.Username);
        var refreshToken = GenerateRefreshToken();

        _logger.LogInformation("Successful login for user: {Username}", request.Username);

        return Ok(new AuthenticationResponse
        {
            Token = token,
            RefreshToken = refreshToken,
            ExpiresIn = _configuration.GetValue<int>("Shell:Services:Authentication:JWT:ExpireMinutes", 60) * 60,
            TokenType = "Bearer",
            Username = request.Username
        });
    }

    /// <summary>
    /// Refreshes an authentication token.
    /// </summary>
    /// <param name="request">The refresh token request.</param>
    /// <returns>A new JWT token if refresh is successful.</returns>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthenticationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthenticationResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.RefreshToken))
        {
            return BadRequest("Invalid refresh token request");
        }

        // TODO: Implement refresh token validation logic
        // This is just a demo implementation
        var username = await ValidateRefreshTokenAsync(request.RefreshToken);

        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized("Invalid refresh token");
        }

        // Generate new tokens
        var token = GenerateJwtToken(username);
        var refreshToken = GenerateRefreshToken();

        _logger.LogInformation("Token refreshed for user: {Username}", username);

        return Ok(new AuthenticationResponse
        {
            Token = token,
            RefreshToken = refreshToken,
            ExpiresIn = _configuration.GetValue<int>("Shell:Services:Authentication:JWT:ExpireMinutes", 60) * 60,
            TokenType = "Bearer",
            Username = username
        });
    }

    /// <summary>
    /// Validates the current authentication token.
    /// </summary>
    /// <returns>Information about the authenticated user.</returns>
    [HttpGet("validate")]
    [Authorize]
    [ProducesResponseType(typeof(UserInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<UserInfo> ValidateToken()
    {
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized();
        }

        return Ok(new UserInfo
        {
            Username = username,
            UserId = userId,
            IsAuthenticated = true,
            Claims = User.Claims.Select(c => new ClaimInfo
            {
                Type = c.Type,
                Value = c.Value
            })
        });
    }

    /// <summary>
    /// Logs out the user (client should discard the token).
    /// </summary>
    /// <returns>Logout confirmation.</returns>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult Logout()
    {
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        _logger.LogInformation("User logged out: {Username}", username);

        // TODO: If implementing token blacklisting, add token to blacklist here

        return Ok(new { Message = "Logged out successfully" });
    }

    private string GenerateJwtToken(string username)
    {
        var jwtSettings = _configuration.GetSection("Shell:Services:Authentication:JWT");
        var secretKey = jwtSettings["SecretKey"];
        var key = Encoding.ASCII.GetBytes(secretKey);
        var tokenHandler = new JwtSecurityTokenHandler();

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim("module_access", "true"),
                // Add additional claims as needed
            }),
            Expires = DateTime.UtcNow.AddMinutes(jwtSettings.GetValue<int>("ExpireMinutes", 60)),
            Issuer = jwtSettings["Issuer"],
            Audience = jwtSettings["Audience"],
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private string GenerateRefreshToken()
    {
        // Generate a random refresh token
        var randomBytes = new byte[32];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }
    }

    private async Task<bool> ValidateUserAsync(string username, string password)
    {
        // TODO: Implement actual user validation logic
        // This is just a demo implementation
        await Task.CompletedTask;
        
        // For demonstration, accept specific test credentials
        if (username == "admin" && password == "admin123")
        {
            return true;
        }
        
        if (username == "user" && password == "user123")
        {
            return true;
        }

        return false;
    }

    private async Task<string> ValidateRefreshTokenAsync(string refreshToken)
    {
        // TODO: Implement actual refresh token validation logic
        // This should check the refresh token against a store
        await Task.CompletedTask;
        
        // For demonstration, always return a username if token is not empty
        if (!string.IsNullOrEmpty(refreshToken))
        {
            return "admin"; // In production, retrieve the actual username from token store
        }

        return null;
    }
}

/// <summary>
/// Login request model.
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// Gets or sets the username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the password.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Refresh token request model.
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>
    /// Gets or sets the refresh token.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>
/// Authentication response model.
/// </summary>
public class AuthenticationResponse
{
    /// <summary>
    /// Gets or sets the JWT token.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the refresh token.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the token expiration time in seconds.
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Gets or sets the token type (usually "Bearer").
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// Gets or sets the username.
    /// </summary>
    public string Username { get; set; } = string.Empty;
}

/// <summary>
/// User information model.
/// </summary>
public class UserInfo
{
    /// <summary>
    /// Gets or sets the username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user ID.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user is authenticated.
    /// </summary>
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// Gets or sets the user's claims.
    /// </summary>
    public IEnumerable<ClaimInfo> Claims { get; set; } = Enumerable.Empty<ClaimInfo>();
}

/// <summary>
/// Claim information model.
/// </summary>
public class ClaimInfo
{
    /// <summary>
    /// Gets or sets the claim type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the claim value.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}