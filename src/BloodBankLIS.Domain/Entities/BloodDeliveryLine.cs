using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>Expected quantity of one blood product class on a delivery, such as RBC or FFP.</summary>
public class BloodDeliveryLine : BaseEntity
{
    public long BloodDeliveryId { get; set; }

    public BloodDelivery? BloodDelivery { get; set; }

    public ComponentClass ComponentClass { get; set; }

    public int ExpectedQuantity { get; set; }
}
