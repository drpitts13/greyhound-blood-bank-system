using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// ISBT product description code (for example E0707) whose units may be thawed in place.
/// The code does not change; expiration is rewritten to 24 hours from the thaw time.
/// </summary>
public class AutoThawProduct : BaseEntity
{
    /// <summary>Five-character ISBT 128 product description code.</summary>
    public string ProductCode { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
