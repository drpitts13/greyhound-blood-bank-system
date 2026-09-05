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
    /// <summary>Set when the charge came from a <see cref="ChargeRule"/>; null for catalog rows.</summary>
    public long? ChargeCodeId { get; set; }

    public ChargeCode? ChargeCode { get; set; }

    /// <summary>Snapshotted billing code from the charge master or catalog row.</summary>
    public string BillingCode { get; set; } = string.Empty;

    public BillingTriggerType TriggerType { get; set; }

    public string TriggerEntityType { get; set; } = string.Empty;

    public long TriggerEntityId { get; set; }

    public long? PatientId { get; set; }

    public DateTime ServiceDateUtc { get; set; }

    /// <summary>Amount snapshotted at capture; null when the catalog price is omitted.</summary>
    public decimal? Amount { get; set; }

    public BillingChargeSourceKind SourceKind { get; set; }

    public long SourceId { get; set; }

    public long? Hl7MessageId { get; set; }

    public string DedupeKey { get; set; } = string.Empty;

    public BillingEventStatus Status { get; set; } = BillingEventStatus.Pending;

    public string? ReviewedBy { get; set; }

    public DateTime? ReviewedUtc { get; set; }

    public DateTime? ExportedUtc { get; set; }

    public string? CancellationReason { get; set; }

    /// <summary>Snapshotted CPT/HCPCS from the charge master at capture (DFT FT1-25).</summary>
    public string? ProcedureCode { get; set; }

    /// <summary>Snapshotted UB-04 revenue code (DFT FT1-13).</summary>
    public string? RevenueCode { get; set; }

    /// <summary>Snapshotted procedure modifier (DFT FT1-26).</summary>
    public string? Modifier { get; set; }

    /// <summary>Snapshotted charge description (DFT FT1-8).</summary>
    public string? Description { get; set; }

    /// <summary>Issuing or performing location code (DFT FT1-16).</summary>
    public string? PerformingLocationCode { get; set; }
}
