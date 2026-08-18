using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Recipient/consignee notification worklist row for a DIN lookback (21 CFR 610.46–47,
/// 606.165). The LIS does not auto-notify patients; staff record attempts against
/// the physician of record per facility SOP.
/// </summary>
public class LookbackNotification : BaseEntity
{
    public string Din { get; set; } = string.Empty;

    public long BloodProductId { get; set; }

    public long? PatientId { get; set; }

    public long? IssueId { get; set; }

    public long? TransfusionEventId { get; set; }

    public LookbackNotificationStatus Status { get; set; } = LookbackNotificationStatus.Pending;

    public string? PhysicianOfRecord { get; set; }

    public DateTime? AttemptedUtc { get; set; }

    public string? AttemptedBy { get; set; }

    public string? Notes { get; set; }

    public string Reason { get; set; } = string.Empty;
}
