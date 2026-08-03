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

    public string? ReceivedBy { get; set; }

    public DateTime? UnitExpirationAtIssueUtc { get; set; }
}
