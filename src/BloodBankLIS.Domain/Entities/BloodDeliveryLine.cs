using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>Expected quantity of one product type on a delivery.</summary>
public class BloodDeliveryLine : BaseEntity
{
    public long BloodDeliveryId { get; set; }

    public BloodDelivery? BloodDelivery { get; set; }

    public long ProductTypeId { get; set; }

    public ProductType? ProductType { get; set; }

    public int ExpectedQuantity { get; set; }
}
