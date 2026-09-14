using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities.Identity;

/// <summary>
/// Server-issued interactive session. The raw token is shown once at login;
/// only <see cref="TokenHash"/> is stored. Rows are revoked, never hard-deleted.
/// </summary>
public class AuthSession : BaseEntity
{
    public long UserId { get; set; }

    public User? User { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime IssuedUtc { get; set; }

    public DateTime LastActivityUtc { get; set; }

    public DateTime AbsoluteExpiresUtc { get; set; }

    public DateTime IdleExpiresUtc { get; set; }

    public string? Workstation { get; set; }

    public DateTime? RevokedUtc { get; set; }

    public string? RevokedReason { get; set; }
}
