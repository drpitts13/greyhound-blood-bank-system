using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Admin;

public sealed record SaveChargeCodeRequest(
    string Code,
    string Description,
    decimal DefaultAmount,
    string? CptCode,
    string? RevenueCode = null,
    string? Modifier = null);

public sealed record ChargeRuleDto(
    long Id,
    BillingTriggerType TriggerType,
    string? TriggerKey,
    long ChargeCodeId,
    string ChargeCode,
    string? ChargeCodeDescription,
    bool IsActive)
{
    public static ChargeRuleDto From(ChargeRule rule, ChargeCode? code) => new(
        rule.Id,
        rule.TriggerType,
        rule.TriggerKey,
        rule.ChargeCodeId,
        code?.Code ?? string.Empty,
        code?.Description,
        rule.IsActive);
}

public sealed record SaveChargeRuleRequest(
    BillingTriggerType TriggerType,
    string? TriggerKey,
    long ChargeCodeId);
