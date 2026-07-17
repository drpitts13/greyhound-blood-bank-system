using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

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
    long ProductTypeId,
    AboGroup Abo,
    RhType RhD,
    string BloodType,
    DateTime ExpiresUtc,
    UnitStatus Status,
    long? CurrentLocationId,
    DateTime CreatedUtc,
    string CreatedBy)
{
    public static BloodUnitDto From(BloodUnit u) => new(
        u.Id, u.UnitNumber, u.ProductTypeId, u.Abo, u.RhD, u.BloodType.ToString(),
        u.ExpiresUtc, u.Status, u.CurrentLocationId, u.CreatedUtc, u.CreatedBy);
}
