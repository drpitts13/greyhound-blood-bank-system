using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>One reagent red cell (or autocontrol placeholder) on a panel lot.</summary>
public class AntibodyPanelCell : BaseEntity
{
    public long LotId { get; set; }

    public string CellNumber { get; set; } = string.Empty;

    public PanelCellRole Role { get; set; } = PanelCellRole.Panel;

    public int SortOrder { get; set; }
}
