using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Written investigation of a reported transfusion reaction (21 CFR 606.170).
/// Opened when a transfusion event flags <c>ReactionSuspected</c>. Fatality fields
/// record CBER notification timing; the LIS does not file with FDA.
/// </summary>
public class ReactionInvestigation : BaseEntity
{
    public long TransfusionEventId { get; set; }

    public TransfusionEvent? TransfusionEvent { get; set; }

    public long PatientId { get; set; }

    public long BloodProductId { get; set; }

    public DateTime ReportedUtc { get; set; }

    public string ReportedBy { get; set; } = "system";

    public string? ReactionType { get; set; }

    public ReactionSeverity Severity { get; set; } = ReactionSeverity.Unknown;

    public string? Findings { get; set; }

    public string? Conclusions { get; set; }

    public string? FollowUp { get; set; }

    public ReactionInvestigationStatus Status { get; set; } = ReactionInvestigationStatus.Open;

    public string? Disposition { get; set; }

    /// <summary>True when the investigation concluded the product was at fault.</summary>
    public bool ProductAtFault { get; set; }

    public bool IsFatality { get; set; }

    public FatalityNotificationStatus FatalityNotificationStatus { get; set; } = FatalityNotificationStatus.NotApplicable;

    /// <summary>Written report due 7 days after fatality confirmation (21 CFR 606.170(b)).</summary>
    public DateTime? WrittenReportDueUtc { get; set; }

    public DateTime? CberNotifiedUtc { get; set; }

    public DateTime? WrittenReportSubmittedUtc { get; set; }

    public long? ClosedSignatureId { get; set; }

    public string? ClosedBy { get; set; }

    public DateTime? ClosedUtc { get; set; }
}
