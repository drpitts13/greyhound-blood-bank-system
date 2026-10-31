using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities.Identity;

/// <summary>
/// An application user (table <c>Users</c>). <see cref="UserName"/> is unique. The
/// password hash is optional here because production deployments may federate to an
/// external identity provider; the field exists for local accounts (see docs/erd.md 1).
/// </summary>
public class User : BaseEntity
{
    public string UserName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? PasswordHash { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsLocked { get; set; }

    /// <summary>Non-interactive system/service account (e.g. interface engine).</summary>
    public bool IsServiceAccount { get; set; }

    public DateTime? LastLoginUtc { get; set; }

    public int FailedSignInCount { get; set; }

    /// <summary>Optional PIN hash for electronic-signature re-authentication.</summary>
    public string? PinHash { get; set; }

    /// <summary>Authenticator key, protected with Data Protection; set once enrollment is confirmed.</summary>
    public string? TotpSecretProtected { get; set; }

    /// <summary>Key issued at enrollment start, kept until the user confirms a code.</summary>
    public string? TotpPendingSecretProtected { get; set; }

    public DateTime? TotpEnrolledUtc { get; set; }

    /// <summary>Last accepted time step, so a code cannot be used twice.</summary>
    public long? TotpLastStep { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
