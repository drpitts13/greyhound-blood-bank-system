using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>Panel or selected-cell lot attached to a workup.</summary>
public class AntibodyIdentificationWorkupLot : BaseEntity
{
    public long WorkupId { get; set; }

    public long LotId { get; set; }

    public bool IsPrimary { get; set; }
}
