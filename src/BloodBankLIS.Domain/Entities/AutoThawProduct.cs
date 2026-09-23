using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Facility product type whose units may be thawed in place at allocation.
/// The product code does not change; expiration is rewritten to 24 hours from the thaw time.
/// </summary>
public class AutoThawProduct : BaseEntity
{
    public long ProductTypeId { get; set; }

    public ProductType? ProductType { get; set; }

    public bool IsActive { get; set; } = true;
}
