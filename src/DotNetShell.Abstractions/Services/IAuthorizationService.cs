using System.Security.Claims;

namespace DotNetShell.Abstractions.Services;

/// <summary>
/// Service interface for authorization operations including role-based, policy-based, and attribute-based access control.
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// Authorizes a user to perform an action on a specific resource.
    /// </summary>
    /// <param name="user">The user to authorize.</param>
    /// <param name="resource">The resource being accessed.</param>
    /// <param name="action">The action being performed on the resource.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The authorization result indicating whether access is granted.</returns>
    Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, string resource, string action, CancellationToken cancellationToken = default);

    /// <summary>
    /// Authorizes a user against a specific policy.
    /// </summary>
    /// <param name="user">The user to authorize.</param>
    /// <param name="policy">The policy name to evaluate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The authorization result indicating whether the policy is satisfied.</returns>
    Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, string policy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Authorizes a user against a specific policy with additional context.
    /// </summary>
    /// <param name="user">The user to authorize.</param>
    /// <param name="policy">The policy name to evaluate.</param>
    /// <param name="resource">The resource being accessed (optional context).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The authorization result indicating whether the policy is satisfied.</returns>
    Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, string policy, object? resource, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has a specific permission.
    /// </summary>
    /// <param name="user">The user to check.</param>
    /// <param name="permission">The permission to check for.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if the user has the permission; otherwise, false.</returns>
    Task<bool> HasPermissionAsync(ClaimsPrincipal user, string permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user is in a specific role.
    /// </summary>
    /// <param name="user">The user to check.</param>
    /// <param name="role">The role to check for.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if the user is in the role; otherwise, false.</returns>
    Task<bool> IsInRoleAsync(ClaimsPrincipal user, string role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all permissions for a specific user.
    /// </summary>
    /// <param name="user">The user to get permissions for.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An enumerable collection of permission names.</returns>
    Task<IEnumerable<string>> GetUserPermissionsAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all roles for a specific user.
    /// </summary>
    /// <param name="user">The user to get roles for.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An enumerable collection of role names.</returns>
    Task<IEnumerable<string>> GetUserRolesAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates multiple permissions for a user in a single call for efficiency.
    /// </summary>
    /// <param name="user">The user to check.</param>
    /// <param name="permissions">The permissions to check for.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A dictionary mapping permission names to their authorization status.</returns>
    Task<IDictionary<string, bool>> EvaluatePermissionsAsync(ClaimsPrincipal user, IEnumerable<string> permissions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has all of the specified permissions.
    /// </summary>
    /// <param name="user">The user to check.</param>
    /// <param name="permissions">The permissions that must all be present.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if the user has all permissions; otherwise, false.</returns>
    Task<bool> HasAllPermissionsAsync(ClaimsPrincipal user, IEnumerable<string> permissions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has any of the specified permissions.
    /// </summary>
    /// <param name="user">The user to check.</param>
    /// <param name="permissions">The permissions, any of which grant access.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if the user has at least one permission; otherwise, false.</returns>
    Task<bool> HasAnyPermissionAsync(ClaimsPrincipal user, IEnumerable<string> permissions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the effective permissions for a user on a specific resource.
    /// </summary>
    /// <param name="user">The user to get permissions for.</param>
    /// <param name="resource">The resource to check permissions on.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An enumerable collection of effective permissions on the resource.</returns>
    Task<IEnumerable<string>> GetEffectivePermissionsAsync(ClaimsPrincipal user, string resource, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of an authorization operation.
/// </summary>
public class AuthorizationResult
{
    /// <summary>
    /// Gets a value indicating whether the authorization was successful.
    /// </summary>
    public bool IsAuthorized { get; init; }

    /// <summary>
    /// Gets the reason for authorization failure, if any.
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// Gets additional context information about the authorization decision.
    /// </summary>
    public IDictionary<string, object> Context { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets the evaluated policies, if any.
    /// </summary>
    public IEnumerable<string> EvaluatedPolicies { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the required permissions that were checked.
    /// </summary>
    public IEnumerable<string> RequiredPermissions { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the user's actual permissions that were considered.
    /// </summary>
    public IEnumerable<string> UserPermissions { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Creates a successful authorization result.
    /// </summary>
    /// <param name="context">Optional context information.</param>
    /// <param name="evaluatedPolicies">Policies that were evaluated.</param>
    /// <param name="requiredPermissions">Permissions that were required.</param>
    /// <param name="userPermissions">User's permissions that were considered.</param>
    /// <returns>A successful authorization result.</returns>
    public static AuthorizationResult Success(
        IDictionary<string, object>? context = null,
        IEnumerable<string>? evaluatedPolicies = null,
        IEnumerable<string>? requiredPermissions = null,
        IEnumerable<string>? userPermissions = null)
    {
        return new AuthorizationResult
        {
            IsAuthorized = true,
            Context = context ?? new Dictionary<string, object>(),
            EvaluatedPolicies = evaluatedPolicies ?? Array.Empty<string>(),
            RequiredPermissions = requiredPermissions ?? Array.Empty<string>(),
            UserPermissions = userPermissions ?? Array.Empty<string>()
        };
    }

    /// <summary>
    /// Creates a failed authorization result.
    /// </summary>
    /// <param name="failureReason">The reason for authorization failure.</param>
    /// <param name="context">Optional context information.</param>
    /// <param name="evaluatedPolicies">Policies that were evaluated.</param>
    /// <param name="requiredPermissions">Permissions that were required.</param>
    /// <param name="userPermissions">User's permissions that were considered.</param>
    /// <returns>A failed authorization result.</returns>
    public static AuthorizationResult Failure(
        string failureReason,
        IDictionary<string, object>? context = null,
        IEnumerable<string>? evaluatedPolicies = null,
        IEnumerable<string>? requiredPermissions = null,
        IEnumerable<string>? userPermissions = null)
    {
        return new AuthorizationResult
        {
            IsAuthorized = false,
            FailureReason = failureReason,
            Context = context ?? new Dictionary<string, object>(),
            EvaluatedPolicies = evaluatedPolicies ?? Array.Empty<string>(),
            RequiredPermissions = requiredPermissions ?? Array.Empty<string>(),
            UserPermissions = userPermissions ?? Array.Empty<string>()
        };
    }
}

/// <summary>
/// Enumeration of authorization policy types.
/// </summary>
public enum AuthorizationPolicyType
{
    /// <summary>
    /// Role-based authorization policy.
    /// </summary>
    Role,

    /// <summary>
    /// Permission-based authorization policy.
    /// </summary>
    Permission,

    /// <summary>
    /// Claim-based authorization policy.
    /// </summary>
    Claim,

    /// <summary>
    /// Resource-based authorization policy.
    /// </summary>
    Resource,

    /// <summary>
    /// Custom authorization policy.
    /// </summary>
    Custom
}

/// <summary>
/// Represents an authorization policy definition.
/// </summary>
public class AuthorizationPolicy
{
    /// <summary>
    /// Gets or sets the name of the policy.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of the policy.
    /// </summary>
    public AuthorizationPolicyType Type { get; set; }

    /// <summary>
    /// Gets or sets the policy requirements.
    /// </summary>
    public IList<string> Requirements { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets additional policy configuration.
    /// </summary>
    public IDictionary<string, object> Configuration { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets a value indicating whether the policy is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the policy description.
    /// </summary>
    public string? Description { get; set; }
}