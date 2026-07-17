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

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
