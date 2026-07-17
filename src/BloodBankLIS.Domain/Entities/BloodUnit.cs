using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// A blood product unit (persisted to the <c>BloodProducts</c> table). The unit
/// number (donation identification number) is a preserved source identifier and is
/// unique. Status changes are guarded by <see cref="Rules.InventoryStatusTransition"/>.
/// </summary>
public class BloodUnit : BaseEntity
{
    public string UnitNumber { get; set; } = string.Empty;

    public long ProductTypeId { get; set; }

    public ProductType? ProductType { get; set; }

    public AboGroup Abo { get; set; } = AboGroup.Unknown;

    public RhType RhD { get; set; } = RhType.Unknown;

    public string? Isbt128ProductCode { get; set; }

    public string? Isbt128DonationId { get; set; }

    public string? CollectionFacility { get; set; }

    public string? Supplier { get; set; }

    public DateTime? CollectedUtc { get; set; }

    public DateTime ExpiresUtc { get; set; }

    public decimal? Volume { get; set; }

    public long? CurrentLocationId { get; set; }

    public InventoryLocation? CurrentLocation { get; set; }

    public UnitStatus Status { get; set; } = UnitStatus.Quarantine;

    public string? QuarantineReason { get; set; }

    public string? DiscardReason { get; set; }

    /// <summary>Convenience projection of the unit's ABO/Rh as a value object.</summary>
    public AboRh BloodType => new(Abo, RhD);
}
