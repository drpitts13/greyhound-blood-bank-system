using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// A physical or logical inventory location (refrigerator, freezer, satellite fridge, issue desk).
/// Mirrors SafeTrace storage sites and SoftBank location dictionaries: storage class,
/// which components may reside here, and whether issue / remote electronic issue is allowed
/// (AABB 5.1.8 storage, 5.11 / 5.16 electronic issue, 21 CFR 606.160 location records).
/// <see cref="Code"/> is unique.
/// </summary>
public class InventoryLocation : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public LocationType LocationType { get; set; } = LocationType.Refrigerator;

    public bool IsActive { get; set; } = true;

    public string? Department { get; set; }

    /// <summary>Units may be issued to a recipient from this site (issue window, main fridge).</summary>
    public bool AllowsIssue { get; set; } = true;

    /// <summary>SafeTrace satellite / unattended issue. Requires electronic XM or emergency release.</summary>
    public bool AllowsRemoteIssue { get; set; }

    /// <summary>Computer crossmatch issue is permitted at this site (AABB 5.16).</summary>
    public bool AllowsElectronicIssue { get; set; } = true;

    /// <summary>AABB two-person verification is required at this site (in addition to facility policy).</summary>
    public bool RequiresSecondVerifier { get; set; }

    /// <summary>True for OR / ED / floor refrigerators that are not the transfusion service.</summary>
    public bool IsSatellite { get; set; }

    public bool AllowsRbc { get; set; } = true;

    public bool AllowsPlasma { get; set; } = true;

    public bool AllowsPlatelets { get; set; } = true;

    public bool AllowsCryo { get; set; } = true;

    public bool AllowsWholeBlood { get; set; } = true;

    /// <summary>Configured storage range in Celsius (AABB 5.1.8).</summary>
    public decimal? StorageTempMinC { get; set; }

    public decimal? StorageTempMaxC { get; set; }

    /// <summary>Overrides facility in-transit due hours when this site issues into a cooler.</summary>
    public int? DefaultInTransitHours { get; set; }

    public string? Notes { get; set; }
}
