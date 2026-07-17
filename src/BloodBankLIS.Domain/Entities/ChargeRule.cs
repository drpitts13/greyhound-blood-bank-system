using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Data-driven mapping from a billing trigger to a charge code (table
/// <c>ChargeRules</c>). <see cref="TriggerKey"/> optionally scopes the rule to a
/// specific test code or product code; null means it applies to every trigger of
/// <see cref="TriggerType"/>. Rules are data, not hard-coded (docs B.1).
/// </summary>
public class ChargeRule : BaseEntity
{
    public BillingTriggerType TriggerType { get; set; }

    /// <summary>Optional selector (e.g. test code "ABORH" or product code "RBC-LR"); null = any.</summary>
    public string? TriggerKey { get; set; }

    public long ChargeCodeId { get; set; }

    public ChargeCode? ChargeCode { get; set; }

    public bool IsActive { get; set; } = true;
}
