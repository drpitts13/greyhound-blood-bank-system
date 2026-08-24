using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Billing catalog row for a blood product. When <see cref="Trigger"/> is met
/// for <see cref="IsbtProductCode"/>, a <see cref="BillingEvent"/> is captured and
/// a DFT is queued. Amount comes from the referenced <see cref="ChargeCode"/>.
/// </summary>
public class ProductBilling : BaseEntity
{
    public long ChargeCodeId { get; set; }

    public ChargeCode? ChargeCode { get; set; }

    public string? Description { get; set; }

    public BillingTriggerType Trigger { get; set; } = BillingTriggerType.UnitIssued;

    /// <summary>ISBT 128 product description code (e.g. E0336).</summary>
    public string IsbtProductCode { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
