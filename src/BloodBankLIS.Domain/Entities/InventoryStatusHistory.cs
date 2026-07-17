using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Append-only record of a blood unit's status and/or location change. Provides the
/// inventory state trail (see docs/erd.md section 6). Not a BaseEntity: it has no
/// modifiable metadata and is never updated or deleted.
/// </summary>
public class InventoryStatusHistory
{
    public long Id { get; set; }

    public long BloodProductId { get; set; }

    public BloodUnit? Unit { get; set; }

    public UnitStatus? FromStatus { get; set; }

    public UnitStatus ToStatus { get; set; }

    public long? FromLocationId { get; set; }

    public long? ToLocationId { get; set; }

    public string? Reason { get; set; }

    public string ChangedBy { get; set; } = "system";

    public DateTime ChangedUtc { get; set; }

    /// <summary>Optional link to the action that caused the change (e.g. an issue or return).</summary>
    public string? RelatedEntityType { get; set; }

    public long? RelatedEntityId { get; set; }
}
