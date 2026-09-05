using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>One cell/phase reaction on an antibody-identification workup.</summary>
public class AntibodyIdentificationReaction : BaseEntity
{
    public long WorkupId { get; set; }

    public long CellId { get; set; }

    public string PhaseCode { get; set; } = string.Empty;

    public ReactionGrade Strength { get; set; } = ReactionGrade.NotTested;
}
