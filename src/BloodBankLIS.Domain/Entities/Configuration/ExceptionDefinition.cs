using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities.Configuration;

/// <summary>
/// Admin catalog of clinical exceptions (rule codes) and the minimum security level
/// required to override them. Seeded for known rule codes; editable via admin UI.
/// </summary>
public class ExceptionDefinition : BaseEntity
{
    /// <summary>Stable rule code (e.g. RES-ABORH-DELTA). Unique.</summary>
    public string RuleCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>User must have max role SecurityLevel &gt;= this value to override.</summary>
    public int MinSecurityLevel { get; set; }

    /// <summary>When false, the exception is never overridable (HardStop-class).</summary>
    public bool IsOverridable { get; set; } = true;

    public bool IsActive { get; set; } = true;
}
