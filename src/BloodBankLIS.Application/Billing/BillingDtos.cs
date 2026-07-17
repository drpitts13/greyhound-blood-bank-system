using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Billing;

public sealed record CancelChargeRequest(string Reason);

public sealed record ChargeCodeDto(long Id, string Code, string Description, decimal DefaultAmount, string? CptCode, bool IsActive)
{
    public static ChargeCodeDto From(ChargeCode c) => new(c.Id, c.Code, c.Description, c.DefaultAmount, c.CptCode, c.IsActive);
}

public sealed record BillingEventDto(
    long Id,
    long ChargeCodeId,
    BillingTriggerType TriggerType,
    string TriggerEntityType,
    long TriggerEntityId,
    long? PatientId,
    DateTime ServiceDateUtc,
    decimal Amount,
    string DedupeKey,
    BillingEventStatus Status,
    string? ReviewedBy,
    DateTime? ReviewedUtc,
    DateTime? ExportedUtc,
    string? CancellationReason)
{
    public static BillingEventDto From(BillingEvent e) => new(
        e.Id, e.ChargeCodeId, e.TriggerType, e.TriggerEntityType, e.TriggerEntityId, e.PatientId,
        e.ServiceDateUtc, e.Amount, e.DedupeKey, e.Status, e.ReviewedBy, e.ReviewedUtc, e.ExportedUtc, e.CancellationReason);
}
