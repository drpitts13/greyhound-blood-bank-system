using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Blood product supplier used when units are received on a delivery.
/// </summary>
public class BloodSupplier : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string Registration { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
