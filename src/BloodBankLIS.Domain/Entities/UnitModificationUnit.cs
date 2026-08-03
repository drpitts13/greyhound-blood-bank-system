using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Links a <see cref="BloodUnit"/> to a <see cref="UnitModification"/> as either a
/// source (consumed) or a result (produced). A single header row can have multiple
/// Source rows (Pool) or multiple Result rows (Divide).
/// </summary>
public class UnitModificationUnit : BaseEntity
{
    public long UnitModificationId { get; set; }

    public UnitModification? UnitModification { get; set; }

    public long BloodProductId { get; set; }

    public BloodUnit? Unit { get; set; }

    public ModificationUnitRole Role { get; set; }

    public int SortOrder { get; set; }
}
