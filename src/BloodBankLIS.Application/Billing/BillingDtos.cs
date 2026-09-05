using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Billing;

public sealed record CancelChargeRequest(string Reason);

public sealed record ChargeCodeDto(
    long Id,
    string Code,
    string Description,
    decimal DefaultAmount,
    string? CptCode,
    bool IsActive,
    string? RevenueCode = null,
    string? Modifier = null)
{
    public static ChargeCodeDto From(ChargeCode c) =>
        new(c.Id, c.Code, c.Description, c.DefaultAmount, c.CptCode, c.IsActive, c.RevenueCode, c.Modifier);
}

public sealed record BillingEventDto(
    long Id,
    long? ChargeCodeId,
    string BillingCode,
    BillingTriggerType TriggerType,
    string TriggerEntityType,
    long TriggerEntityId,
    long? PatientId,
    DateTime ServiceDateUtc,
    decimal? Amount,
    BillingChargeSourceKind SourceKind,
    long SourceId,
    long? Hl7MessageId,
    string DedupeKey,
    BillingEventStatus Status,
    string? ReviewedBy,
    DateTime? ReviewedUtc,
    DateTime? ExportedUtc,
    string? CancellationReason,
    string? ProcedureCode = null,
    string? RevenueCode = null,
    string? Modifier = null,
    string? Description = null,
    string? PerformingLocationCode = null)
{
    public static BillingEventDto From(BillingEvent e) => new(
        e.Id, e.ChargeCodeId, e.BillingCode, e.TriggerType, e.TriggerEntityType, e.TriggerEntityId, e.PatientId,
        e.ServiceDateUtc, e.Amount, e.SourceKind, e.SourceId, e.Hl7MessageId, e.DedupeKey, e.Status,
        e.ReviewedBy, e.ReviewedUtc, e.ExportedUtc, e.CancellationReason,
        e.ProcedureCode, e.RevenueCode, e.Modifier, e.Description, e.PerformingLocationCode);
}
