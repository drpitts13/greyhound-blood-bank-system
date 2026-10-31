using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities.Configuration;

/// <summary>
/// Admin catalog of why a unit may be discarded. Discard stores the code on the unit
/// so wastage can be reported by reason. <see cref="RequiresComment"/> makes the
/// free-text comment mandatory, for a catch-all code such as Other.
/// </summary>
public class UnitDiscardReason : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public bool RequiresComment { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
