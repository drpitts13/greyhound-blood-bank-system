using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities.Configuration;

/// <summary>
/// Links a <see cref="ProductType"/> to a <see cref="ProductAttribute"/>, recording whether
/// the attribute is allowed or required for that product type.
/// </summary>
public class ProductAttributeAssignment : BaseEntity
{
    public long ProductTypeId { get; set; }

    public ProductType? ProductType { get; set; }

    public long ProductAttributeId { get; set; }

    public ProductAttribute? ProductAttribute { get; set; }

    /// <summary>True when the attribute is mandatory for the product type (not merely allowed).</summary>
    public bool IsRequired { get; set; }

    public bool IsActive { get; set; } = true;
}
