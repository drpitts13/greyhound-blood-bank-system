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
    DateTime? ExpectedArrivalDueUtc = null,
    UnitQuarantineReason QuarantineReasonCode = UnitQuarantineReason.Unspecified,
    decimal? Volume = null,
    string? DivisionCode = null,
    string? CollectionTypeCode = null,
    string? Supplier = null,
    string? CollectionFacility = null,
    DateTime? CollectionDateTime = null,
    string? ProcessingFacilityCode = null,
    long? DeliveryId = null,
    // Shipped to another facility: where, when, and the outbound shipment reference.
    string? ShippedToFacility = null,
    DateTime? ShippedUtc = null,
    string? OutboundShipmentReference = null,
    // OCD-072: how the shipment was packed and who receives it (null for shipments before Cycle 668).
    string? ShipContainer = null,
    decimal? ShipPackingTemperatureC = null,
    string? ShipPackedBy = null,
    string? ShipReceivingContact = null,
    // OCD-083: when the EHR reported this unit transfused while it was recalled, quarantined, damaged or missing.
    DateTime? TransfusionReportedUtc = null)
{
    public long? BloodSupplierId { get; init; }

    /// <summary>True when this unit was produced by a modification and release still needs a matching label scan.</summary>
    public bool RequiresModificationLabelScan { get; init; }

    /// <summary>
    /// Staff-facing supplier. A recorded name stays that name.
    /// A blank name stays the supplier id when the delivery names that supplier.
    /// The stored supplier and collection facility are not rewritten.
    /// </summary>
    public string? SupplierLabel =>
        string.IsNullOrWhiteSpace(Supplier)
            ? BloodSupplierId is long id ? BloodSupplierLabel.Name(id, Supplier) : null
            : Supplier.Trim();

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
        u.ExpectedArrivalDueUtc, u.QuarantineReasonCode,
        u.Volume, u.DivisionCode, u.CollectionTypeCode,
        u.Supplier, u.CollectionFacility, u.CollectionDateTime, u.ProcessingFacilityCode, u.DeliveryId,
        u.ShippedToFacility, u.ShippedUtc, u.OutboundShipmentReference,
        u.ShipContainer, u.ShipPackingTemperatureC, u.ShipPackedBy, u.ShipReceivingContact, u.TransfusionReportedUtc);
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
    RhType RhD,
    long ProductTypeId,
    bool IsOutdated = false)
{
    public long? BloodSupplierId { get; init; }

    /// <summary>
    /// Staff-facing supplier on the expected-inbound board.
    /// A recorded name stays that name. A blank name stays the supplier id
    /// when the delivery names that supplier. No delivery does not invent one.
    /// </summary>
    public string? SupplierLabel =>
        string.IsNullOrWhiteSpace(Supplier)
            ? BloodSupplierId is long id ? BloodSupplierLabel.Name(id, Supplier) : null
            : Supplier.Trim();

    public static ExpectedInboundWorkItemDto From(BloodUnit u, DateTime now) => new(
        u.Id, u.UnitNumber, u.CreatedUtc, u.ExpectedArrivalDueUtc,
        ExpectedArrivalPendingRule.EvaluateOverdue(u.ExpectedArrivalDueUtc, now).Severity == RuleSeverity.Warning,
        u.ShipmentId, u.Supplier, u.Abo, u.RhD, u.ProductTypeId,
        BloodUnitExpirationRule.IsExpired(u.ExpiresUtc, now));
}

public sealed record NearExpiryWorkItemDto(
    long UnitId,
    string UnitNumber,
    DateTime ExpiresUtc,
    UnitStatus Status,
    AboGroup Abo,
    RhType RhD,
    long ProductTypeId,
    long? CurrentLocationId = null,
    string? CurrentLocationName = null,
    // The patient whose reservation is on this unit, when there is one: issue it first or reserve a replacement.
    long? ReservedPatientId = null,
    string? ReservedPatientMrn = null,
    string? ReservedPatientName = null)
{
    public string LocationLabel => StorageLocationLabel.Format(CurrentLocationId, CurrentLocationName);

    public bool IsReserved => ReservedPatientId is not null;

    /// <summary>"Name (MRN)", or the MRN or patient id alone when that is all that is recorded.</summary>
    public string? ReservedPatientLabel => ReservedPatientId is not long id
        ? null
        : !string.IsNullOrWhiteSpace(ReservedPatientName)
            ? string.IsNullOrWhiteSpace(ReservedPatientMrn) ? ReservedPatientName : $"{ReservedPatientName} ({ReservedPatientMrn})"
            : !string.IsNullOrWhiteSpace(ReservedPatientMrn) ? ReservedPatientMrn : $"patient {id}";

    public const string ReservedNextStep = "Issue it first or reserve a replacement.";

    public static NearExpiryWorkItemDto From(BloodUnit u, string? locationName = null) => new(
        u.Id, u.UnitNumber, u.ExpiresUtc, u.Status, u.Abo, u.RhD, u.ProductTypeId,
        u.CurrentLocationId,
        string.IsNullOrWhiteSpace(locationName) ? null : locationName.Trim());
}

public sealed record QuarantineWorkItemDto(
    long UnitId,
    string UnitNumber,
    UnitQuarantineReason ReasonCode,
    string? Notes,
    AboGroup Abo,
    RhType RhD,
    DateTime ExpiresUtc,
    long ProductTypeId = 0,
    string? ProductCode = null,
    AboGroup? RetypeInterpretedAbo = null,
    RhType? RetypeInterpretedRh = null,
    bool RetypeMismatch = false,
    long? CurrentLocationId = null,
    string? CurrentLocationName = null,
    decimal? Volume = null,
    bool RequiresLabelScan = false,
    // From QuarantineReleaseRule: what to do with the unit, and whether Release is allowed.
    string? NextStep = null,
    bool CanRelease = true)
{
    public string LocationLabel => StorageLocationLabel.Format(CurrentLocationId, CurrentLocationName);

    public string VolumeLabel =>
        Volume is decimal volume
            ? $"{volume.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} mL"
            : "Volume not recorded";

    public string ReleasePlaceSentence =>
        LocationLabel == StorageLocationLabel.None
            ? "No storage location is recorded."
            : $"It is at {LocationLabel}.";

    public static QuarantineWorkItemDto From(
        BloodUnit u,
        string? productCode = null,
        ProductRetypeResult? latestRetype = null,
        string? locationName = null,
        bool requiresLabelScan = false) => new(
        u.Id, u.UnitNumber, u.QuarantineReasonCode, u.QuarantineReason, u.Abo, u.RhD, u.ExpiresUtc,
        u.ProductTypeId,
        productCode ?? u.ProductType?.ProductCode,
        latestRetype?.InterpretedAbo,
        latestRetype?.InterpretedRh,
        latestRetype is { MatchesLabel: false },
        u.CurrentLocationId,
        string.IsNullOrWhiteSpace(locationName) ? null : locationName.Trim(),
        u.Volume,
        requiresLabelScan);
}

public sealed record DiscrepancyWorkItemDto(
    long UnitId,
    string UnitNumber,
    UnitStatus Status,
    string? Reason,
    AboGroup Abo,
    RhType RhD,
    long? CurrentLocationId,
    long ProductTypeId = 0,
    string? ProductCode = null,
    string? CurrentLocationName = null)
{
    public string LocationLabel => StorageLocationLabel.Format(CurrentLocationId, CurrentLocationName);

    public static DiscrepancyWorkItemDto From(BloodUnit u, string? productCode = null, string? locationName = null) => new(
        u.Id, u.UnitNumber, u.Status,
        u.Status == UnitStatus.Missing ? u.MissingReason : u.DamagedReason,
        u.Abo, u.RhD, u.CurrentLocationId,
        u.ProductTypeId,
        productCode ?? u.ProductType?.ProductCode,
        string.IsNullOrWhiteSpace(locationName) ? null : locationName.Trim());
}

public sealed record OnHoldWorkItemDto(
    long UnitId,
    string UnitNumber,
    string? HoldReason,
    AboGroup Abo,
    RhType RhD,
    DateTime ExpiresUtc,
    long ProductTypeId = 0,
    string? ProductCode = null,
    long? CurrentLocationId = null,
    string? CurrentLocationName = null)
{
    public string LocationLabel => StorageLocationLabel.Format(CurrentLocationId, CurrentLocationName);

    public static OnHoldWorkItemDto From(BloodUnit u, string? productCode = null, string? locationName = null) => new(
        u.Id, u.UnitNumber, u.HoldReason, u.Abo, u.RhD, u.ExpiresUtc,
        u.ProductTypeId,
        productCode ?? u.ProductType?.ProductCode,
        u.CurrentLocationId,
        string.IsNullOrWhiteSpace(locationName) ? null : locationName.Trim());
}

/// <summary>
/// Cycle 688: a unit recalled and not yet pulled: still in Recalled status, waiting to be quarantined or discarded (a
/// return to the supplier goes through quarantine). <see cref="WasStatus"/> is what it was when recalled (Issued means it is on the ward, not on a shelf).
/// </summary>
public sealed record RecalledWorkItemDto(
    long UnitId,
    string UnitNumber,
    AboGroup Abo,
    RhType RhD,
    DateTime ExpiresUtc,
    string? ProductCode,
    long? CurrentLocationId,
    string? CurrentLocationName,
    string? RecallReason,
    DateTime? RecalledUtc,
    string? RecalledBy,
    UnitStatus? WasStatus)
{
    public string LocationLabel => StorageLocationLabel.Format(CurrentLocationId, CurrentLocationName);
}
