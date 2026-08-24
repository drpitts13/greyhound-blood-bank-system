using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Billing catalog row for a test or service. When <see cref="Trigger"/> is met
/// for <see cref="TestCode"/>, a <see cref="BillingEvent"/> is captured and a DFT
/// is queued. Amount comes from the referenced <see cref="ChargeCode"/>.
/// </summary>
public class TestServiceBilling : BaseEntity
{
    public long ChargeCodeId { get; set; }

    public ChargeCode? ChargeCode { get; set; }

    public string? Description { get; set; }

    public BillingTriggerType Trigger { get; set; } = BillingTriggerType.TestVerified;

    public string TestCode { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
