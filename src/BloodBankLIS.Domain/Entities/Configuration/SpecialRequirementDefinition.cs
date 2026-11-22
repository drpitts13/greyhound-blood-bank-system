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

    /// <summary>For informational requirements, the banner text.</summary>
    public string? Instruction { get; set; }

    /// <summary>The admin table: unit typing, unit modification, informational, or issue procedure.</summary>
    public SpecialRequirementCategory Category { get; set; } = SpecialRequirementCategory.IssueProcedure;

    /// <summary>
    /// The unit category a typing or modification applies to (RBC, platelets, plasma...). Null means all categories,
    /// kept for entries that existed before categories.
    /// </summary>
    public ComponentClass? ComponentClass { get; set; }

    /// <summary>For RequireUnitAbo: the allowed unit ABO groups, comma separated (e.g. "O" or "O,A").</summary>
    public string? AllowedAboGroups { get; set; }

    /// <summary>For RequireAntigenNegative: a fixed antigen. Blank means the antigen is entered on the chart.</summary>
    public string? AntigenCode { get; set; }

    /// <summary>For RequireUnitTestNegative: the unit test code whose negative result satisfies it (e.g. CMV, HBS).</summary>
    public string? UnitTestCode { get; set; }

    public int SortOrder { get; set; }
}
