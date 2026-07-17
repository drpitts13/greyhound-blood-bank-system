using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// A physical or logical inventory location (refrigerator, freezer, issue desk, etc.).
/// <see cref="Code"/> is unique.
/// </summary>
public class InventoryLocation : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public LocationType LocationType { get; set; } = LocationType.Refrigerator;

    public bool IsActive { get; set; } = true;
}
