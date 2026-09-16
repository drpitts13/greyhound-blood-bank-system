using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities.Configuration;

/// <summary>
/// Admin-configured allowed modification path: a modification code (reusable across
/// product paths), a source product code, a modification type, the resulting target
/// product code, and an expiration modification code. Identity is <see cref="Id"/>;
/// uniqueness of the catalog key is (ModificationCode, SourceProductTypeId, TargetProductTypeId).
/// The expiration code's offset is applied relative to modification or collection
/// date/time and capped at the original unit's expiration. Drives
/// <c>BloodProductModificationService</c>. See docs/erd.md and docs/workflows.md.
/// </summary>
public class ModificationRule : BaseEntity
{
    /// <summary>
    /// Short key for this mapping. The same code may be reused on different source/target
    /// product pairs; uniqueness is (ModificationCode, SourceProductTypeId, TargetProductTypeId).
    /// </summary>
    public string ModificationCode { get; set; } = string.Empty;

    public long SourceProductTypeId { get; set; }

    public ProductType? SourceProductType { get; set; }

    public ModificationType ModificationType { get; set; }

    public long TargetProductTypeId { get; set; }

    public ProductType? TargetProductType { get; set; }

    public long ExpirationModificationCodeId { get; set; }

    public ExpirationModificationCode? ExpirationModificationCode { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    /// <summary>Monotonic config version; bumped on significant admin edits (snapshot history).</summary>
    public int Version { get; set; } = 1;
}
