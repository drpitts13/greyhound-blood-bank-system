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

public sealed record ActionDefinitionDto(
    long Id,
    string Code,
    string Name,
    ActionCategory Category,
    ModificationType? ModificationType,
    string? Description,
    bool IsActive,
    int Version)
{
    public static ActionDefinitionDto From(ActionDefinition e) => new(
        e.Id, e.Code, e.Name, e.Category, e.ModificationType, e.Description, e.IsActive, e.Version);
}

public sealed record SaveActionDefinitionRequest(
    string Code,
    string Name,
    ActionCategory Category,
    ModificationType? ModificationType,
    string? Description);

public sealed record ActionBillingDto(
    long Id,
    long ActionDefinitionId,
    string ActionCode,
    string ActionName,
    long ChargeCodeId,
    string ChargeCode,
    string? ChargeCodeDescription,
    decimal? ChargeCodeAmount,
    string? Description,
    BillingTriggerType Trigger,
    bool IsActive)
{
    public static ActionBillingDto From(ActionBilling e, ActionDefinition? action, ChargeCode? code) => new(
        e.Id,
        e.ActionDefinitionId,
        action?.Code ?? string.Empty,
        action?.Name ?? string.Empty,
        e.ChargeCodeId,
        code?.Code ?? string.Empty,
        code?.Description,
        code?.DefaultAmount,
        e.Description,
        e.Trigger,
        e.IsActive);
}

public sealed record SaveActionBillingRequest(
    long ActionDefinitionId,
    long ChargeCodeId,
    string? Description,
    BillingTriggerType Trigger);
