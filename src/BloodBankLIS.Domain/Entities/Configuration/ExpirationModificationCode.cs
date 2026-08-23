using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Entities.Configuration;

/// <summary>
/// Admin-configured expiration offset used by <see cref="ModificationRule"/>.
/// The numeric amount is applied from either the modification date/time or the
/// collection date/time, then capped at the source unit's original expiration.
/// </summary>
public class ExpirationModificationCode : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public int OffsetAmount { get; set; }

    public ExpirationOffsetUnit OffsetUnit { get; set; }

    public ExpirationRelativeTo RelativeTo { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    /// <summary>Monotonic config version; bumped on significant admin edits.</summary>
    public int Version { get; set; } = 1;

    public ICollection<ModificationRule> ModificationRules { get; set; } = new List<ModificationRule>();

    public ExpirationOffsetCode ToOffset() => new(OffsetAmount, OffsetUnit);
}
