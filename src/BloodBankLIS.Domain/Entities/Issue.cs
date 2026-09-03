using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Record of a unit being issued from inventory to a patient. Created only after
/// the full issue gate passes (or its Warnings are overridden). A standard issue
/// requires no override; an emergency release requires an <see cref="Override"/>.
/// </summary>
public class Issue : BaseEntity
{
    public long? AllocationId { get; set; }

    public long BloodProductId { get; set; }

    public BloodUnit? Unit { get; set; }

    public long PatientId { get; set; }

    public long? EncounterId { get; set; }

    public long? OrderId { get; set; }

    public string? IssuedToLocation { get; set; }

    public string? IssuedTo { get; set; }

    public DateTime IssuedUtc { get; set; }

    public string IssuedBy { get; set; } = "system";

    /// <summary>Optional free-text comments captured at issue time.</summary>
    public string? Comment { get; set; }

    public IssueType IssueType { get; set; } = IssueType.Standard;

    public long? OverrideId { get; set; }

    /// <summary>The override that authorized this issue, when one was required.</summary>
    public Override? Override { get; set; }

    public IssueStatus Status { get; set; } = IssueStatus.Issued;

    /// <summary>Fresh scan verification payload captured at issue (normalized identity fields).</summary>
    public string? VerifiedScanJson { get; set; }

    public CrossmatchClinicalStatus CrossmatchStatus { get; set; } = CrossmatchClinicalStatus.NotPerformed;

    public string? EmergencyReleaseDetails { get; set; }

    /// <summary>Person who took the unit at issue (courier / pickup), not ward custody.</summary>
    public string? ReceivedBy { get; set; }

    /// <summary>When the nursing unit acknowledged custody (SoftBank remote-issue receipt).</summary>
    public DateTime? WardReceivedUtc { get; set; }

    public string? WardReceivedBy { get; set; }

    public bool WardVisualAcceptable { get; set; } = true;

    /// <summary>SoftBank cooler / transport container id while the unit is in transit to the ward.</summary>
    public string? CoolerId { get; set; }

    /// <summary>When ward receipt is due (IssuedUtc + Issue.InTransitDueHours).</summary>
    public DateTime? InTransitDueUtc { get; set; }

    public DateTime? UnitExpirationAtIssueUtc { get; set; }

    /// <summary>21 CFR 606.151(b): conspicuous incomplete-testing statement for emergency release.</summary>
    public bool TestsIncompleteAtIssue { get; set; }

    /// <summary>When retrospective compatibility testing is due (emergency / MTP incomplete issue).</summary>
    public DateTime? RetrospectiveCrossmatchDueUtc { get; set; }

    public DateTime? RetrospectiveCrossmatchCompletedUtc { get; set; }

    public long? RetrospectiveCrossmatchId { get; set; }

    public bool VisualInspectionAcceptable { get; set; } = true;

    public string? SecondVerifier { get; set; }

    public string? PatientIdentifier1 { get; set; }

    public string? PatientIdentifier2 { get; set; }
}
