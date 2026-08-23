using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities.Configuration;

/// <summary>
/// Admin-configured allowed modification path: a unique modification code, a source
/// product code, a modification type, the resulting target product code, and an
/// expiration modification code.
/// The expiration code's offset is applied relative to modification or collection
/// date/time and capped at the original unit's expiration. Drives
/// <c>BloodProductModificationService</c>. See docs/erd.md and docs/workflows.md.
/// </summary>
public class ModificationRule : BaseEntity
{
    /// <summary>Unique short key that identifies this specific source→type→target mapping.</summary>
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
