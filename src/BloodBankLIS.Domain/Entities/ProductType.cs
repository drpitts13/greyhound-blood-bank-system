using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Catalog of blood product types. <see cref="ProductCode"/> is unique (ISBT 128
/// product code where applicable).
/// </summary>
public class ProductType : BaseEntity
{
    public string ProductCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public ComponentClass ComponentClass { get; set; } = ComponentClass.RedBloodCells;

    public int? DefaultShelfLifeHours { get; set; }

    public bool RequiresCrossmatch { get; set; }

    public bool IsActive { get; set; } = true;

    // --- Product definition (admin configuration) ---

    /// <summary>Free-form category/grouping label for the catalog UI.</summary>
    public string? Category { get; set; }

    /// <summary>Whether ABO/Rh compatibility applies when issuing this product.</summary>
    public bool RequiresAboMatch { get; set; } = true;

    public bool RequiresRhMatch { get; set; } = true;

    /// <summary>ISBT 128 product description code placeholder (facility-configurable).</summary>
    public string? Isbt128ProductCode { get; set; }

    /// <summary>Default charge-code mapping placeholder (links to the charge master later).</summary>
    public string? DefaultChargeCode { get; set; }

    /// <summary>Storage requirements note (e.g. "1-6C", "-18C or colder").</summary>
    public string? StorageRequirements { get; set; }

    /// <summary>Issue/return/modification rule notes (configurable text for the foundation phase).</summary>
    public string? IssueRules { get; set; }

    public string? ReturnRules { get; set; }

    public string? ModificationRules { get; set; }

    /// <summary>Monotonic config version; bumped on significant admin edits (snapshot history).</summary>
    public int Version { get; set; } = 1;

    public ICollection<BloodUnit> Units { get; set; } = new List<BloodUnit>();

    public ICollection<ProductAttributeAssignment> AttributeAssignments { get; set; } = new List<ProductAttributeAssignment>();
}
