using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Append-only antibody history. Antibodies are never silently removed; deactivation
/// requires a reason and is audited (see docs/erd.md section 3, docs/safety-rules.md).
/// </summary>
public class AntibodyHistory : BaseEntity
{
    public long PatientId { get; set; }

    /// <summary>FK to the blood attribute catalog when the antibody is catalog-defined.</summary>
    public long? BloodAttributeDefinitionId { get; set; }

    /// <summary>Antibody specificity, e.g. "anti-K", "anti-E".</summary>
    public string AntibodySpecificity { get; set; } = string.Empty;

    public AntibodyStatus Status { get; set; } = AntibodyStatus.Identified;

    public long? SourceResultId { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Comment { get; set; }

    /// <summary>Reason captured when the antibody record is deactivated.</summary>
    public string? DeactivationReason { get; set; }
}
