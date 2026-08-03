using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities.Configuration;

/// <summary>
/// Admin-configured allowed modification path: a source product code, a modification
/// type, the resulting target product code, and an expiration offset code (e.g. "24H",
/// "5D") applied relative to the modification date/time and capped at the original
/// unit's expiration date/time. Drives <c>BloodProductModificationService</c>.
/// See docs/erd.md and docs/workflows.md.
/// </summary>
public class ModificationRule : BaseEntity
{
    public long SourceProductTypeId { get; set; }

    public ProductType? SourceProductType { get; set; }

    public ModificationType ModificationType { get; set; }

    public long TargetProductTypeId { get; set; }

    public ProductType? TargetProductType { get; set; }

    /// <summary>
    /// Expiration offset, e.g. "24H" (24 hours) or "5D" (5 days). Parsed by
    /// <see cref="ValueObjects.ExpirationOffsetCode"/>.
    /// </summary>
    public string ExpirationOffsetCode { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    /// <summary>Monotonic config version; bumped on significant admin edits (snapshot history).</summary>
    public int Version { get; set; } = 1;
}
