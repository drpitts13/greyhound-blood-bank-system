using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Admin;

public sealed record TestServiceBillingDto(
    long Id,
    long ChargeCodeId,
    string ChargeCode,
    string? ChargeCodeDescription,
    decimal? ChargeCodeAmount,
    string? Description,
    BillingTriggerType Trigger,
    string TestCode,
    bool IsActive)
{
    public static TestServiceBillingDto From(TestServiceBilling e, ChargeCode? code) => new(
        e.Id,
        e.ChargeCodeId,
        code?.Code ?? string.Empty,
        code?.Description,
        code?.DefaultAmount,
        e.Description,
        e.Trigger,
        e.TestCode,
        e.IsActive);
}

public sealed record SaveTestServiceBillingRequest(
    long ChargeCodeId,
    string? Description,
    BillingTriggerType Trigger,
    string TestCode);

public sealed record ProductBillingDto(
    long Id,
    long ChargeCodeId,
    string ChargeCode,
    string? ChargeCodeDescription,
    decimal? ChargeCodeAmount,
    string? Description,
    BillingTriggerType Trigger,
    string IsbtProductCode,
    bool IsActive)
{
    public static ProductBillingDto From(ProductBilling e, ChargeCode? code) => new(
        e.Id,
        e.ChargeCodeId,
        code?.Code ?? string.Empty,
        code?.Description,
        code?.DefaultAmount,
        e.Description,
        e.Trigger,
        e.IsbtProductCode,
        e.IsActive);
}

public sealed record SaveProductBillingRequest(
    long ChargeCodeId,
    string? Description,
    BillingTriggerType Trigger,
    string IsbtProductCode);
