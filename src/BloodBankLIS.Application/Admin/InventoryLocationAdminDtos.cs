using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Admin;

public sealed record InventoryLocationAdminDto(
    long Id,
    string Code,
    string Name,
    LocationType LocationType,
    bool IsActive,
    string? Department,
    bool AllowsIssue,
    bool AllowsRemoteIssue,
    bool AllowsElectronicIssue,
    bool RequiresSecondVerifier,
    bool IsSatellite,
    bool AllowsRbc,
    bool AllowsPlasma,
    bool AllowsPlatelets,
    bool AllowsCryo,
    bool AllowsWholeBlood,
    decimal? StorageTempMinC,
    decimal? StorageTempMaxC,
    int? DefaultInTransitHours,
    string? Notes)
{
    public static InventoryLocationAdminDto From(InventoryLocation l) => new(
        l.Id, l.Code, l.Name, l.LocationType, l.IsActive, l.Department,
        l.AllowsIssue, l.AllowsRemoteIssue, l.AllowsElectronicIssue, l.RequiresSecondVerifier, l.IsSatellite,
        l.AllowsRbc, l.AllowsPlasma, l.AllowsPlatelets, l.AllowsCryo, l.AllowsWholeBlood,
        l.StorageTempMinC, l.StorageTempMaxC, l.DefaultInTransitHours, l.Notes);
}

public sealed record SaveInventoryLocationRequest(
    string Code,
    string Name,
    LocationType LocationType,
    string? Department = null,
    bool? AllowsIssue = null,
    bool? AllowsRemoteIssue = null,
    bool? AllowsElectronicIssue = null,
    bool RequiresSecondVerifier = false,
    bool? IsSatellite = null,
    bool? AllowsRbc = null,
    bool? AllowsPlasma = null,
    bool? AllowsPlatelets = null,
    bool? AllowsCryo = null,
    bool? AllowsWholeBlood = null,
    decimal? StorageTempMinC = null,
    decimal? StorageTempMaxC = null,
    int? DefaultInTransitHours = null,
    string? Notes = null,
    bool ApplyTypeDefaults = false);
