using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>One in-date or historical lot of an antibody-identification panel.</summary>
public class AntibodyPanelLot : BaseEntity
{
    public long ManufacturerId { get; set; }

    public string LotNumber { get; set; } = string.Empty;

    public DateOnly ExpiresOn { get; set; }

    public string PanelName { get; set; } = string.Empty;

    /// <summary>True when this lot is a selected-cell supplement rather than a primary panel.</summary>
    public bool IsSelectedCellLot { get; set; }

    public bool IsActive { get; set; } = true;
}
