using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Inventory;

public sealed record CreateBloodUnitRequest(
    string UnitNumber,
    long ProductTypeId,
    AboGroup Abo,
    RhType RhD,
    DateTime ExpiresUtc,
    long? CurrentLocationId,
    string? CollectionFacility,
    string? Supplier);

public sealed record BloodUnitDto(
    long Id,
    string UnitNumber,
    string? ComponentIdentity,
    string? Din,
    string? ProductCodeData,
    string? ProductDescriptionCode,
    string? Isbt128ProductCode,
    string? AboRhdCode,
    string? ExpirationEncoded,
    DateTime? ExpirationLocal,
    string? ExpirationTimezone,
    bool ExpirationHasExplicitTime,
    long ProductTypeId,
    AboGroup Abo,
    RhType RhD,
    string BloodType,
    DateTime ExpiresUtc,
    UnitStatus Status,
    long? CurrentLocationId,
    ComponentEntrySource Source,
    DateTime CreatedUtc,
    string CreatedBy,
    string? HoldReason = null,
    string? QuarantineReason = null,
    string? MissingReason = null,
    string? DamagedReason = null,
    bool ReceiveVisualAcceptable = true,
    string? ReceiveVisualNotes = null,
    string? ShipmentId = null,
    UnitAppearance ReceiveAppearance = UnitAppearance.Acceptable,
    decimal? ReceiveTemperatureCelsius = null,
    string? SupplierReturnReason = null,
    DonationRestriction DonationRestriction = DonationRestriction.Allogeneic,
    long? ReservedPatientId = null,
    string? DirectedConversionReason = null,
    DateTime? DirectedConvertedUtc = null,
    string? DirectedConvertedBy = null,
    DateTime? ExpectedArrivalDueUtc = null)
{
    public static BloodUnitDto From(BloodUnit u) => new(
        u.Id, u.UnitNumber, u.ComponentIdentity, u.Din, u.ProductCodeData,
        u.ProductDescriptionCode, u.Isbt128ProductCode, u.AboRhdCode,
        u.ExpirationEncoded, u.ExpirationLocal, u.ExpirationTimezone, u.ExpirationHasExplicitTime,
        u.ProductTypeId, u.Abo, u.RhD, u.BloodType.ToString(),
        u.ExpiresUtc, u.Status, u.CurrentLocationId, u.Source, u.CreatedUtc, u.CreatedBy,
        u.HoldReason, u.QuarantineReason, u.MissingReason, u.DamagedReason, u.ReceiveVisualAcceptable, u.ReceiveVisualNotes,
        u.ShipmentId, u.ReceiveAppearance, u.ReceiveTemperatureCelsius, u.SupplierReturnReason,
        u.DonationRestriction, u.ReservedPatientId,
        u.DirectedConversionReason, u.DirectedConvertedUtc, u.DirectedConvertedBy,
        u.ExpectedArrivalDueUtc);
}

public sealed record ExpectedInboundWorkItemDto(
    long UnitId,
    string UnitNumber,
    DateTime CreatedUtc,
    DateTime? DueUtc,
    bool IsOverdue,
    string? ShipmentId,
    string? Supplier,
    AboGroup Abo,
    RhType RhD)
{
    public static ExpectedInboundWorkItemDto From(BloodUnit u, DateTime now) => new(
        u.Id, u.UnitNumber, u.CreatedUtc, u.ExpectedArrivalDueUtc,
        ExpectedArrivalPendingRule.EvaluateOverdue(u.ExpectedArrivalDueUtc, now).Severity == RuleSeverity.Warning,
        u.ShipmentId, u.Supplier, u.Abo, u.RhD);
}
