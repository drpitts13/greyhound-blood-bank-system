using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Inventory;

/// <summary>Intake request for a new blood unit. Received units land in Quarantine.</summary>
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
    decimal? Volume = null);
