using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// One massive transfusion protocol activation for a patient: who asked for it, why, who activated it in the blood
/// bank, and when and why it ended. Every massive-transfusion issue links to the patient's active activation. Pack
/// composition and ratios are facility policy and are not stored here. Rows are closed, never deleted.
/// </summary>
public class MtpActivation : BaseEntity
{
    public long PatientId { get; set; }

    public MtpActivationStatus Status { get; set; } = MtpActivationStatus.Active;

    /// <summary>The clinician who called the protocol (not necessarily a system user).</summary>
    public string RequestedBy { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string? Location { get; set; }

    public DateTime ActivatedUtc { get; set; }

    /// <summary>The signed-in blood bank user who recorded the activation.</summary>
    public string ActivatedBy { get; set; } = string.Empty;

    public DateTime? ClosedUtc { get; set; }

    public string? ClosedBy { get; set; }

    public string? CloseReason { get; set; }
}
