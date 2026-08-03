using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Canonical blood-component record (persisted to <c>BloodProducts</c>).
/// Identity is DIN13 + full 8-character product data (+ extended division when required),
/// never DIN alone. Downstream workflows must use these normalized fields and must not
/// independently re-parse raw barcode strings.
/// ICCBBA_VALIDATION_REQUIRED / INSTITUTIONAL_POLICY_REVIEW: field semantics and
/// institutional expiration policy require local validation before clinical use.
/// </summary>
public class BloodUnit : BaseEntity
{
    /// <summary>
    /// Legacy/search key. For ISBT-normalized units this equals <see cref="ComponentIdentity"/>.
    /// </summary>
    public string UnitNumber { get; set; } = string.Empty;

    /// <summary>Canonical identity: DIN|ProductCodeData[|ExtendedDivision].</summary>
    public string? ComponentIdentity { get; set; }

    /// <summary>Persisted uniqueness key (extended division coalesced).</summary>
    public string? ComponentIdentityKey { get; set; }

    public long ProductTypeId { get; set; }

    public ProductType? ProductType { get; set; }

    public AboGroup Abo { get; set; } = AboGroup.Unknown;

    public RhType RhD { get; set; } = RhType.Unknown;

    // --- DIN quadrant ---
    public string? Din { get; set; }
    public string? Fin { get; set; }
    public string? NominalYear { get; set; }
    public string? DonationSequence { get; set; }
    public string? DinFlags { get; set; }
    public string? DinKeyboardCheck { get; set; }

    // --- ABO/RhD quadrant ---
    public string? AboRhdCode { get; set; }
    public string? DonationCollectionCategory { get; set; }
    public string? EncodedPhenotype { get; set; }
    public string? AboSpecialMessage { get; set; }

    // --- Product quadrant ---
    public string? ProductCodeData { get; set; }
    public string? ProductDescriptionCode { get; set; }
    public string? CollectionTypeCode { get; set; }
    public string? DivisionCode { get; set; }
    public string? ExtendedDivisionCode { get; set; }

    // --- Expiration quadrant ---
    public string? ExpirationEncoded { get; set; }
    public DateTime? ExpirationLocal { get; set; }
    public string? ExpirationTimezone { get; set; }
    public bool ExpirationHasExplicitTime { get; set; }

    public DateTime? CollectionDateTime { get; set; }
    public string? ProcessingFacilityCode { get; set; }
    public string StandardVersion { get; set; } = "PLACEHOLDER-REQUIRES-ICCBBA";

    public ComponentEntrySource Source { get; set; } = ComponentEntrySource.Manual;

    /// <summary>Legacy free-text ISBT fields retained for back-compat / migration.</summary>
    public string? Isbt128ProductCode { get; set; }

    public string? Isbt128DonationId { get; set; }

    public string? CollectionFacility { get; set; }

    public string? Supplier { get; set; }

    public string? ShipmentId { get; set; }

    public DateTime? CollectedUtc { get; set; }

    public DateTime ExpiresUtc { get; set; }

    public decimal? Volume { get; set; }

    public long? CurrentLocationId { get; set; }

    public InventoryLocation? CurrentLocation { get; set; }

    public UnitStatus Status { get; set; } = UnitStatus.Quarantine;

    public string? QuarantineReason { get; set; }

    public string? DiscardReason { get; set; }

    public string? RecallReason { get; set; }

    /// <summary>
    /// Set on a result unit produced by a product modification (divide/pool/irradiate/
    /// thaw/volume-reduce/leukoreduce); null for units received directly into inventory.
    /// </summary>
    public long? DerivedFromModificationId { get; set; }

    public UnitModification? DerivedFromModification { get; set; }

    /// <summary>Convenience projection of the unit's ABO/Rh as a value object.</summary>
    public AboRh BloodType => new(Abo, RhD);

    public ICollection<BloodComponentRawScan> RawScans { get; set; } = new List<BloodComponentRawScan>();

    public ICollection<BloodComponentSpecialTest> SpecialTests { get; set; } = new List<BloodComponentSpecialTest>();
}
