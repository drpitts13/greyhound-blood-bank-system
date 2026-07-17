using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities.Configuration;

/// <summary>
/// Catalog of configurable product attributes (e.g. Irradiated, Leukoreduced, CMV-negative,
/// Washed, Volume-reduced). Assigned to product types via <see cref="ProductAttributeAssignment"/>.
/// </summary>
public class ProductAttribute : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<ProductAttributeAssignment> Assignments { get; set; } = new List<ProductAttributeAssignment>();
}
