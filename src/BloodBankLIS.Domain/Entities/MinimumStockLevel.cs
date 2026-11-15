using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Minimum available count for a product, optionally limited to one ABO group.
/// The board compares on-hand stock with this minimum. It does not order blood.
/// </summary>
public class MinimumStockLevel : BaseEntity
{
    public long ProductTypeId { get; set; }

    public ProductType? ProductType { get; set; }

    public AboGroup? Abo { get; set; }

    /// <summary>
    /// Optionally one Rh(D) type (end-user testing pass 105: minimums were by ABO only, so O negative, the stock kept
    /// for emergencies and Rh-negative women, could not have a minimum of its own).
    /// </summary>
    public RhType? RhD { get; set; }

    public int MinimumAvailable { get; set; }
}
