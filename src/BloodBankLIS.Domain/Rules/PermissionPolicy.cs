namespace BloodBankLIS.Domain.Rules;

/// <summary>The outcome of an authorization check.</summary>
public enum AccessDecision
{
    /// <summary>No authenticated actor — the request is unauthenticated (HTTP 401).</summary>
    Unauthenticated = 0,

    /// <summary>Authenticated but lacking the required permission (HTTP 403).</summary>
    Forbidden = 1,

    /// <summary>Authorized to proceed.</summary>
    Allowed = 2
}

/// <summary>
/// Pure authorization decision: default-deny. An unauthenticated actor is rejected
/// before permission evaluation; an authenticated actor must hold the required
/// permission code. No role-string shortcuts (see docs/architecture.md 4.2).
/// </summary>
public static class PermissionPolicy
{
    public static AccessDecision Evaluate(bool isAuthenticated, IReadOnlySet<string>? grantedPermissions, string requiredPermission)
    {
        if (!isAuthenticated)
        {
            return AccessDecision.Unauthenticated;
        }

        if (string.IsNullOrEmpty(requiredPermission))
        {
            return AccessDecision.Allowed;
        }

        return grantedPermissions is not null && grantedPermissions.Contains(requiredPermission)
            ? AccessDecision.Allowed
            : AccessDecision.Forbidden;
    }
}
