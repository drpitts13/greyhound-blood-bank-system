using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Entities.Configuration;

/// <summary>
/// One facility-approved indication for ordering a component class (patient blood
/// management). A laboratory criterion names a test, a comparison, a threshold, and how
/// recent the result must be; it is met automatically. An attestation criterion has no
/// test (for example, active bleeding) and is met when the orderer selects it.
/// No criteria are shipped: every threshold is entered by the facility after SME
/// approval (OCD-038).
/// </summary>
public class TransfusionIndicationCriterion : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public ComponentClass ComponentClass { get; set; }

    /// <summary>Test code to compare; null for an attestation criterion.</summary>
    public string? LabTestCode { get; set; }

    public IndicationComparison? Comparison { get; set; }

    public decimal? Threshold { get; set; }

    public string? Unit { get; set; }

    /// <summary>How old the qualifying result may be at order time.</summary>
    public int LookbackHours { get; set; } = 24;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
