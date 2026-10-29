using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// One cell of a product's Rh allow/deny grid for a sex group.
/// </summary>
public class ProductRhCompatibility : BaseEntity
{
    public long ProductTypeId { get; set; }

    public ProductType? ProductType { get; set; }

    public RhCompatibilitySex SexGroup { get; set; }

    public RhType PatientRh { get; set; }

    public RhType UnitRh { get; set; }

    public bool IsAllowed { get; set; }
}
