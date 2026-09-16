using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities.Configuration;

/// <summary>
/// Versioned catalog of special transfusion requirement types (unit, issuing, or patient).
/// Patient assignments reference this row; enforcement is computer-evaluated from
/// <see cref="EnforcementKind"/>.
/// </summary>
public class SpecialRequirementDefinition : VersionedConfigEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public SpecialRequirementLevel Level { get; set; }

    public SpecialRequirementEnforcementKind EnforcementKind { get; set; }

    /// <summary>Required when <see cref="EnforcementKind"/> is RequireProductAttribute.</summary>
    public string? ProductAttributeCode { get; set; }

    public string? Instruction { get; set; }

    public int SortOrder { get; set; }
}
