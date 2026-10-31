namespace BloodBankLIS.Domain.Rules;

public enum MfaRequirement
{
    /// <summary>Password alone signs in.</summary>
    None = 0,

    /// <summary>A one-time code is required with the password.</summary>
    Challenge = 1,

    /// <summary>
    /// Policy requires an authenticator and the user has none: no session is issued.
    /// The user sets one up on the sign-in screen and signs in with its first code.
    /// </summary>
    EnrollmentRequired = 2
}

/// <summary>
/// Who must use a second factor (OCD-037, decided 2026-10-31). Authenticator sign-in is a
/// facility option: with it off, nobody is asked for a code. With it on, an enrolled user
/// always gets the challenge, and a user the facility requires to use MFA (everyone, or
/// privileged users) cannot sign in until they enroll. Privileged means holding a
/// permission that administers users, roles, or configuration, or approves overrides.
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

    /// <summary>Whether facility settings require this user to have an authenticator.</summary>
    public static bool IsRequired(bool mfaEnabled, bool requireAllUsers, bool requirePrivileged, bool privileged) =>
        mfaEnabled && (requireAllUsers || (requirePrivileged && privileged));

    public static MfaRequirement Evaluate(bool mfaEnabled, bool requiredForUser, bool enrolled)
    {
        if (!mfaEnabled)
        {
            return MfaRequirement.None;
        }

        if (enrolled)
        {
            return MfaRequirement.Challenge;
        }

        return requiredForUser ? MfaRequirement.EnrollmentRequired : MfaRequirement.None;
    }
}
