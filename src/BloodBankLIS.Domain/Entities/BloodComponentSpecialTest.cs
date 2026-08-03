using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

public class BloodComponentSpecialTest : BaseEntity
{
    public long BloodProductId { get; set; }

    public BloodUnit? Unit { get; set; }

    public string TestCode { get; set; } = string.Empty;

    public string? Result { get; set; }

    public string? StandardVersion { get; set; }
}
