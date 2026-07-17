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

    public IssueType IssueType { get; set; } = IssueType.Standard;

    public long? OverrideId { get; set; }

    /// <summary>The override that authorized this issue, when one was required.</summary>
    public Override? Override { get; set; }

    public IssueStatus Status { get; set; } = IssueStatus.Issued;
}
