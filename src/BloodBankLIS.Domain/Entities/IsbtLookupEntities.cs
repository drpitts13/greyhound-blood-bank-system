using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Versioned ABO/RhD code lookup. PLACEHOLDER rows only — do not fabricate clinical mappings.
/// ICCBBA_VALIDATION_REQUIRED.
/// </summary>
public class IsbtAboRhdCode : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public AboGroup Abo { get; set; }
    public RhType RhD { get; set; }
    public string? CollectionType { get; set; }
    public string? SpecialMessage { get; set; }
    public string? AdditionalPhenotype { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? RetiredDate { get; set; }
    public string StandardVersion { get; set; } = "PLACEHOLDER-REQUIRES-ICCBBA";
    public bool IsPlaceholder { get; set; } = true;
}

/// <summary>Versioned product description code lookup. ICCBBA_VALIDATION_REQUIRED.</summary>
public class IsbtProductCode : BaseEntity
{
    public string ProductDescriptionCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ComponentClass { get; set; } = "Other";
    public string? Modifier { get; set; }
    public string AttributesJson { get; set; } = "[]";
    public string? StorageRequirements { get; set; }
    public bool RequiresExtendedDivision { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? RetiredDate { get; set; }
    public string StandardVersion { get; set; } = "PLACEHOLDER-REQUIRES-ICCBBA";
    public bool IsPlaceholder { get; set; } = true;
}

public class IsbtCollectionType : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? RetiredDate { get; set; }
    public string StandardVersion { get; set; } = "PLACEHOLDER-REQUIRES-ICCBBA";
    public bool IsPlaceholder { get; set; } = true;
}

public class IsbtDataStructure : BaseEntity
{
    public string DataIdentifier { get; set; } = string.Empty;
    public IsbtDataStructureKind Kind { get; set; }
    public string Description { get; set; } = string.Empty;
    public string StandardVersion { get; set; } = "PLACEHOLDER-REQUIRES-ICCBBA";
    public bool IsActive { get; set; } = true;
}
