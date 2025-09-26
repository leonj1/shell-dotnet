# Authentication and Authorization for Guest Modules

The .NET Shell Framework now includes automatic JWT authentication enforcement for all guest module endpoints. This ensures that all business logic endpoints are protected by default while allowing specific endpoints to be made publicly accessible when needed.

## Overview

The authentication system uses JWT (JSON Web Tokens) to secure API endpoints. When enabled, all guest module endpoints automatically require a valid JWT token, except for specifically excluded endpoints.

## Key Features

- **Automatic Protection**: All guest module endpoints require authentication by default
- **Flexible Configuration**: Easy to configure which endpoints should be excluded
- **JWT-based**: Industry-standard JWT tokens for stateless authentication
- **Refresh Tokens**: Support for token refresh to maintain sessions
- **AllowAnonymous Support**: Individual endpoints can opt-out using `[AllowAnonymous]`

## Configuration

Authentication is configured in `appsettings.json`:

```json
{
  "Shell": {
    "Services": {
      "Authentication": {
        "Enabled": true,
        "EnforceForGuestEndpoints": true,
        "ExcludedPaths": [
          "/api/public/*",
          "/api/webhooks/*"
        ],
        "JWT": {
          "Issuer": "DotNetShell",
          "Audience": "DotNetShell.API",
          "SecretKey": "YourSecretKeyHere",
          "ExpireMinutes": 60,
          "RefreshTokenExpireDays": 7
        }
      }
    }
  }
}
```

### Configuration Options

- **Enabled**: Master switch for authentication (true/false)
- **EnforceForGuestEndpoints**: Whether to enforce authentication for guest module endpoints (true/false)
- **ExcludedPaths**: Array of paths that should not require authentication (supports wildcards with `*`)
- **JWT.SecretKey**: Secret key for signing JWT tokens (should be at least 32 characters)
- **JWT.ExpireMinutes**: Token expiration time in minutes
- **JWT.RefreshTokenExpireDays**: Refresh token expiration in days

## Default Excluded Endpoints

The following endpoints are excluded from authentication by default:

- `/` - Root endpoint
- `/health/*` - All health check endpoints
- `/swagger/*` - Swagger documentation (in development/staging)
- `/api/auth/*` - Authentication endpoints (login, refresh, etc.)

## Authentication Flow

### 1. Obtaining a Token

Send a POST request to `/api/authentication/login`:

```bash
curl -X POST https://localhost:5001/api/authentication/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "password": "admin123"
  }'
```

Response:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4...",
  "expiresIn": 3600,
  "tokenType": "Bearer",
  "username": "admin"
}
```

### 2. Using the Token

Include the token in the Authorization header for subsequent requests:

```bash
curl -X GET https://localhost:5001/api/sample/message \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

### 3. Refreshing the Token

When the token expires, use the refresh token to get a new one:

```bash
curl -X POST https://localhost:5001/api/authentication/refresh \
  -H "Content-Type: application/json" \
  -d '{
    "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4..."
  }'
```

## Implementing in Guest Modules

### Protected by Default

All endpoints in guest modules are automatically protected:

```csharp
[ApiController]
[Route("api/[controller]")]
public class ProductController : ControllerBase
{
    // This endpoint requires authentication automatically
    [HttpGet]
    public ActionResult<IEnumerable<Product>> GetProducts()
    {
        // Implementation
    }
}
```

### Allowing Anonymous Access

To make specific endpoints publicly accessible, use the `[AllowAnonymous]` attribute:

```csharp
[ApiController]
[Route("api/[controller]")]
public class ProductController : ControllerBase
{
    // This endpoint is publicly accessible
    [HttpGet("public")]
    [AllowAnonymous]
    public ActionResult<IEnumerable<Product>> GetPublicProducts()
    {
        // Implementation
    }

    // This endpoint requires authentication
    [HttpGet("private")]
    public ActionResult<IEnumerable<Product>> GetPrivateProducts()
    {
        // Implementation
    }
}
```

### Accessing User Information

In authenticated endpoints, you can access user information from the `User` property:

```csharp
[HttpGet("my-profile")]
public ActionResult<UserProfile> GetMyProfile()
{
    var username = User.FindFirst(ClaimTypes.Name)?.Value;
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    
    // Use the user information
    return Ok(new UserProfile { Username = username, Id = userId });
}
```

## Testing Authentication

### 1. Test Without Authentication

Try accessing a protected endpoint without a token:

```bash
curl -v https://localhost:5001/api/sample/message
```

Expected: 401 Unauthorized response

### 2. Test With Authentication

First, get a token:

```bash
# Login
TOKEN=$(curl -s -X POST https://localhost:5001/api/authentication/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}' \
  | jq -r '.token')

# Use the token
curl https://localhost:5001/api/sample/message \
  -H "Authorization: Bearer $TOKEN"
```

Expected: 200 OK with response data

### 3. Test Anonymous Endpoints

Health endpoints should work without authentication:

```bash
curl https://localhost:5001/api/sample/health
```

Expected: 200 OK with health status

## Security Considerations

1. **Secret Key**: Always use a strong, unique secret key in production (at least 32 characters)
2. **HTTPS**: Always use HTTPS in production to protect tokens in transit
3. **Token Storage**: Store tokens securely on the client side (use secure cookies or encrypted storage)
4. **Token Expiration**: Keep token expiration times short (30-60 minutes) and use refresh tokens
5. **Refresh Token Security**: Store refresh tokens securely and implement rotation if possible

## Troubleshooting

### Common Issues

1. **401 Unauthorized on all endpoints**
   - Check if authentication is enabled in configuration
   - Verify the token is being sent in the Authorization header
   - Check if the token has expired

2. **Token validation fails**
   - Ensure the secret key matches between token generation and validation
   - Check if the issuer and audience settings match

3. **Cannot access health endpoints**
   - Health endpoints should have `[AllowAnonymous]` attribute
   - Check if the path is in the excluded paths list

## Advanced Configuration

### Custom Authentication Providers

The framework supports multiple authentication providers. You can add Azure AD, OAuth 2.0, or other providers:

```csharp
// In ShellStartup.cs
services.AddAuthentication()
    .AddJwtBearer()
    .AddAzureAD(options => Configuration.Bind("AzureAd", options));
```

### Custom Authorization Policies

Create custom policies for fine-grained access control:

```csharp
services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy =>
        policy.RequireRole("Admin"));
    
    options.AddPolicy("RequireModuleAccess", policy =>
        policy.RequireClaim("module_access", "true"));
});
```

Use in controllers:

```csharp
[Authorize(Policy = "RequireAdminRole")]
[HttpPost]
public ActionResult CreateProduct(Product product)
{
    // Only admins can access this
}
```

## Demo Credentials

For development and testing, the following demo credentials are available:

- Username: `admin`, Password: `admin123`
- Username: `user`, Password: `user123`

**Note**: Replace the authentication logic in `AuthenticationController.ValidateUserAsync` with your actual user validation logic in production.
