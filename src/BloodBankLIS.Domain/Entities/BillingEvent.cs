using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// A captured charge (table <c>BillingEvents</c>). Created by the billing service when
/// a clinical trigger fires after its action commits. <see cref="DedupeKey"/> carries a
/// unique constraint so a repeated trigger cannot create a second charge (docs B.3).
/// Status flows Pending -> Reviewed -> Exported, or Cancelled (docs B.4).
/// </summary>
public class BillingEvent : BaseEntity
{
    public long ChargeCodeId { get; set; }

    public ChargeCode? ChargeCode { get; set; }

    public BillingTriggerType TriggerType { get; set; }

    public string TriggerEntityType { get; set; } = string.Empty;

    public long TriggerEntityId { get; set; }

    public long? PatientId { get; set; }

    public DateTime ServiceDateUtc { get; set; }

    /// <summary>Amount snapshotted from the charge code at capture time.</summary>
    public decimal Amount { get; set; }

    public string DedupeKey { get; set; } = string.Empty;

    public BillingEventStatus Status { get; set; } = BillingEventStatus.Pending;

    public string? ReviewedBy { get; set; }

    public DateTime? ReviewedUtc { get; set; }

    public DateTime? ExportedUtc { get; set; }

    public string? CancellationReason { get; set; }
}
