namespace BloodBankLIS.Domain.Rules;

public enum MfaRequirement
{
    /// <summary>Password alone signs in.</summary>
    None = 0,

    /// <summary>A one-time code is required with the password.</summary>
    Challenge = 1,

    /// <summary>Policy requires MFA but the user has not enrolled; sign-in proceeds and enrollment is requested.</summary>
    EnrollmentRequired = 2
}

/// <summary>
/// Who must use a second factor. An enrolled user always gets the challenge. With the
/// facility policy on, a privileged user who has not enrolled is asked to enroll at
/// sign-in. Privileged means holding a permission that administers users, roles, or
/// configuration, or approves overrides.
/// </summary>
public static class MfaRule
{
    public static readonly IReadOnlySet<string> PrivilegedPermissions = new HashSet<string>(StringComparer.Ordinal)
    {
        PermissionCodes.AdminUsersManage,
        PermissionCodes.AdminRolesManage,
        PermissionCodes.AdminConfigEdit,
        PermissionCodes.AdminConfigActivate,
        PermissionCodes.OverrideApprove
    };

    public static bool IsPrivileged(IEnumerable<string> permissions) =>
        permissions.Any(PrivilegedPermissions.Contains);

    public static MfaRequirement Evaluate(bool policyRequiresPrivilegedMfa, bool privileged, bool enrolled)
    {
        if (enrolled)
        {
            return MfaRequirement.Challenge;
        }

        return policyRequiresPrivilegedMfa && privileged
            ? MfaRequirement.EnrollmentRequired
            : MfaRequirement.None;
    }
}
