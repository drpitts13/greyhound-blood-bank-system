using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Admin;

public sealed record TestServiceBillingDto(
    long Id,
    string BillingCode,
    string? Description,
    decimal? Price,
    BillingTriggerType Trigger,
    string TestCode,
    bool IsActive)
{
    public static TestServiceBillingDto From(TestServiceBilling e) => new(
        e.Id, e.BillingCode, e.Description, e.Price, e.Trigger, e.TestCode, e.IsActive);
}

public sealed record SaveTestServiceBillingRequest(
    string BillingCode,
    string? Description,
    decimal? Price,
    BillingTriggerType Trigger,
    string TestCode);

public sealed record ProductBillingDto(
    long Id,
    string BillingCode,
    string? Description,
    decimal? Price,
    BillingTriggerType Trigger,
    string IsbtProductCode,
    bool IsActive)
{
    public static ProductBillingDto From(ProductBilling e) => new(
        e.Id, e.BillingCode, e.Description, e.Price, e.Trigger, e.IsbtProductCode, e.IsActive);
}

public sealed record SaveProductBillingRequest(
    string BillingCode,
    string? Description,
    decimal? Price,
    BillingTriggerType Trigger,
    string IsbtProductCode);
