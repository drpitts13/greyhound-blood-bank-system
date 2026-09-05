using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>Antigen typing for one catalog attribute on one panel cell.</summary>
public class AntibodyPanelCellAntigen : BaseEntity
{
    public long CellId { get; set; }

    public long BloodAttributeDefinitionId { get; set; }

    public AntigenExpression Expression { get; set; } = AntigenExpression.NotTested;
}
