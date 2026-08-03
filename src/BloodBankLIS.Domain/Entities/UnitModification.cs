using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Append-only header record of a single product-modification transaction (divide,
/// pool, irradiate, thaw, volume-reduce, leukoreduce), executed under a specific
/// <see cref="ModificationRule"/>. Source and result units are linked via
/// <see cref="UnitModificationUnit"/> (1 source/N results for Divide, N sources/1
/// result for Pool, 1/1 otherwise). A dangerous action: requires a reason and is
/// recorded via a named <c>Modify</c> audit event (see docs/safety-rules.md).
/// </summary>
public class UnitModification : BaseEntity
{
    public long ModificationRuleId { get; set; }

    public ModificationRule? ModificationRule { get; set; }

    /// <summary>Denormalized copy of the rule's type at execution time.</summary>
    public ModificationType ModificationType { get; set; }

    /// <summary>Denormalized copy of the rule's expiration offset code at execution time.</summary>
    public string ExpirationOffsetCodeApplied { get; set; } = string.Empty;

    /// <summary>Expiration date/time computed for the result unit(s), already capped at the source's original expiration.</summary>
    public DateTime ResultExpiresUtc { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string PerformedBy { get; set; } = "system";

    public DateTime PerformedUtc { get; set; }

    public ICollection<UnitModificationUnit> Units { get; set; } = new List<UnitModificationUnit>();
}
