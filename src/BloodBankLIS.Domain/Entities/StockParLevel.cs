using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Minimum available count for a product, optionally limited to one ABO group.
/// The board compares on-hand stock with this minimum. It does not order blood.
/// </summary>
public class StockParLevel : BaseEntity
{
    public long ProductTypeId { get; set; }

    public ProductType? ProductType { get; set; }

    public AboGroup? Abo { get; set; }

    public int MinimumAvailable { get; set; }
}
