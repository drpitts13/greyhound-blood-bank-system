using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// One cell of a product's ABO allow/deny grid. The same grid applies to every sex.
/// </summary>
public class ProductAboCompatibility : BaseEntity
{
    public long ProductTypeId { get; set; }

    public ProductType? ProductType { get; set; }

    public AboGroup PatientAbo { get; set; }

    public AboGroup UnitAbo { get; set; }

    public bool IsAllowed { get; set; }
}
