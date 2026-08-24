using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Billing catalog row for a blood product. When <see cref="Trigger"/> is met
/// for <see cref="IsbtProductCode"/>, a <see cref="BillingEvent"/> is captured and
/// a DFT is queued. <see cref="Price"/> is optional internal tracking only.
/// </summary>
public class ProductBilling : BaseEntity
{
    public string BillingCode { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Optional facility price for reporting; never sent on the DFT.</summary>
    public decimal? Price { get; set; }

    public BillingTriggerType Trigger { get; set; } = BillingTriggerType.UnitIssued;

    /// <summary>ISBT 128 product description code (e.g. E0336).</summary>
    public string IsbtProductCode { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
