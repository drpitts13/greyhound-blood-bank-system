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

    /// <summary>Another facility reported this antibody (OutsideAntibodyReportRule); null when found here.</summary>
    public string? ReportingFacility { get; set; }

    /// <summary>When the reporting facility identified it, when known.</summary>
    public DateOnly? IdentifiedOn { get; set; }

    /// <summary>OCD-073: how the outside report is documented (a copy kept, or a phone report).</summary>
    public OutsideReportKind? ReportKind { get; set; }

    /// <summary>OCD-073: for a copy, what it is and where it is kept; for a phone report, who called.</summary>
    public string? ReportDocumentation { get; set; }

    /// <summary>OCD-073: when this laboratory confirmed an outside report by its own testing; null until then.</summary>
    public DateTime? ConfirmedUtc { get; set; }

    public string? ConfirmedBy { get; set; }

    /// <summary>The testing here that confirmed it (e.g. the panel or workup).</summary>
    public string? ConfirmationNote { get; set; }
}
