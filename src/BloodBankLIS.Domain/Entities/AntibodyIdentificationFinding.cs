using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// One specificity classification on a workup. Assist rows are advisory.
/// Only technologist Identified rows may post to antibody history after review.
/// </summary>
public class AntibodyIdentificationFinding : BaseEntity
{
    public long WorkupId { get; set; }

    public long? BloodAttributeDefinitionId { get; set; }

    public string Specificity { get; set; } = string.Empty;

    public AntibodyIdClassification Classification { get; set; }

    public AntibodyIdSource Source { get; set; }

    public string? Rationale { get; set; }

    public bool PostedToHistory { get; set; }
}
