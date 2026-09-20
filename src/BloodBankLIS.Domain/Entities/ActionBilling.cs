using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Billing catalog row for a clinical action. When <see cref="Trigger"/> is met
/// for <see cref="ActionDefinitionId"/>, a <see cref="BillingEvent"/> is captured
/// and a DFT is queued. Amount comes from the referenced <see cref="ChargeCode"/>.
/// </summary>
public class ActionBilling : BaseEntity
{
    public long ActionDefinitionId { get; set; }

    public ActionDefinition? ActionDefinition { get; set; }

    public long ChargeCodeId { get; set; }

    public ChargeCode? ChargeCode { get; set; }

    public string? Description { get; set; }

    public BillingTriggerType Trigger { get; set; } = BillingTriggerType.ActionCompleted;

    public bool IsActive { get; set; } = true;
}
