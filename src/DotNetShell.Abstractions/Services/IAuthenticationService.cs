using System.Security.Claims;

namespace DotNetShell.Abstractions.Services;

/// <summary>
/// Service interface for authentication operations.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Authenticates a user with the provided token.
    /// </summary>
    /// <param name="token">The authentication token.</param>
    /// <returns>The authentication result containing user information.</returns>
    Task<AuthenticationResult> AuthenticateAsync(string token);

    /// <summary>
    /// Generates an authentication token for the provided user principal.
    /// </summary>
    /// <param name="principal">The user principal to generate a token for.</param>
    /// <returns>The generated authentication token.</returns>
    Task<string> GenerateTokenAsync(ClaimsPrincipal principal);

    /// <summary>
    /// Validates the provided authentication token.
    /// </summary>
    /// <param name="token">The token to validate.</param>
    /// <returns>True if the token is valid; otherwise, false.</returns>
    Task<bool> ValidateTokenAsync(string token);

    /// <summary>
    /// Revokes the provided authentication token.
    /// </summary>
    /// <param name="token">The token to revoke.</param>
    /// <returns>A task that represents the asynchronous revocation operation.</returns>
    Task RevokeTokenAsync(string token);

    /// <summary>
    /// Generates a refresh token for the provided user principal.
    /// </summary>
    /// <param name="principal">The user principal to generate a refresh token for.</param>
    /// <returns>The generated refresh token.</returns>
    Task<string> GenerateRefreshTokenAsync(ClaimsPrincipal principal);

    /// <summary>
    /// Refreshes an access token using a refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token to use.</param>
    /// <returns>The new authentication result with access token.</returns>
    Task<AuthenticationResult> RefreshTokenAsync(string refreshToken);
}

/// <summary>
/// Represents the result of an authentication operation.
/// </summary>
public class AuthenticationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the authentication was successful.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Gets or sets the authentication error message, if any.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the authenticated user principal.
    /// </summary>
    public ClaimsPrincipal? Principal { get; set; }

    /// <summary>
    /// Gets or sets the access token.
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the refresh token.
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Gets or sets the token expiration time.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Creates a successful authentication result.
    /// </summary>
    /// <param name="principal">The authenticated user principal.</param>
    /// <param name="accessToken">The access token.</param>
    /// <param name="refreshToken">The refresh token.</param>
    /// <param name="expiresAt">The token expiration time.</param>
    /// <returns>A successful authentication result.</returns>
    public static AuthenticationResult Success(ClaimsPrincipal principal, string accessToken, string? refreshToken = null, DateTime? expiresAt = null)
    {
        return new AuthenticationResult
        {
            IsSuccess = true,
            Principal = principal,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt
        };
    }

    /// <summary>
    /// Creates a failed authentication result.
    /// </summary>
    /// <param name="errorMessage">The error message describing the failure.</param>
    /// <returns>A failed authentication result.</returns>
    public static AuthenticationResult Failure(string errorMessage)
    {
        return new AuthenticationResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}