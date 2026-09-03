using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Inventory;

/// <summary>Intake request for a new blood unit. Retype products land in Received; others in Quarantine.</summary>
public sealed record ReceiveUnitRequest(
    string UnitNumber,
    long ProductTypeId,
    AboGroup Abo,
    RhType RhD,
    DateTime ExpiresUtc,
    long? LocationId = null,
    string? CollectionFacility = null,
    string? Supplier = null,
    string? Isbt128ProductCode = null,
    string? Isbt128DonationId = null,
    decimal? Volume = null,
    bool VisualInspectionAcceptable = true,
    string? VisualInspectionNotes = null,
    string? ShipmentId = null,
    string? SecondVerifier = null,
    UnitAppearance Appearance = UnitAppearance.Acceptable,
    decimal? ReceiveTemperatureCelsius = null,
    DonationRestriction DonationRestriction = DonationRestriction.Allogeneic,
    long? ReservedPatientId = null);

public sealed record ReceiveExpectedUnitRequest(
    bool VisualInspectionAcceptable = true,
    string? VisualInspectionNotes = null,
    long? LocationId = null,
    string? SecondVerifier = null,
    UnitAppearance Appearance = UnitAppearance.Acceptable,
    decimal? ReceiveTemperatureCelsius = null);

public sealed record CancelExpectedUnitRequest(string Reason);

public sealed record ReleaseFromQuarantineRequest(string? SecondVerifier = null);

public sealed record QuarantineUnitRequest(UnitQuarantineReason ReasonCode, string? Notes = null);

public sealed record ConvertDirectedToAllogeneicRequest(string Reason, string? SecondVerifier = null);
