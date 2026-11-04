using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Staff-facing name for a special requirement in the catalog.
/// The code stays so staff can select the requirement.
/// A missing or blank name stays the requirement id and is not replaced by the code.
/// </summary>
public static class SpecialRequirementLabel
{
    /// <summary>
    /// Name for a special requirement. A blank name stays the requirement id.
    /// The requirement code and a product-attribute code are not substitutes.
    /// </summary>
    public static string Name(long requirementId, string? name) =>
        string.IsNullOrWhiteSpace(name) ? $"Requirement {requirementId}" : name.Trim();
    /// <summary>The admin table name.</summary>
    public static string Category(SpecialRequirementCategory category) => category switch
    {
        SpecialRequirementCategory.UnitTyping => "Unit typing",
        SpecialRequirementCategory.UnitModification => "Unit modification",
        SpecialRequirementCategory.Informational => "Informational",
        _ => "Issue procedure"
    };

    public static string Kind(SpecialRequirementEnforcementKind kind) => kind switch
    {
        SpecialRequirementEnforcementKind.RequireProductAttribute => "Product attribute",
        SpecialRequirementEnforcementKind.RequireAntigenNegative => "Antigen negative",
        SpecialRequirementEnforcementKind.RequireIssueAcknowledgment => "Acknowledge at issue",
        SpecialRequirementEnforcementKind.RequireComplexCrossmatch => "Complex crossmatch",
        SpecialRequirementEnforcementKind.RequireAboSubgroup => "A1/A2 subgroup typed",
        SpecialRequirementEnforcementKind.RequireUnitAbo => "Unit ABO group",
        SpecialRequirementEnforcementKind.RequireRhNegative => "Unit RhD negative",
        SpecialRequirementEnforcementKind.RequireUnitTestNegative => "Unit test negative",
        SpecialRequirementEnforcementKind.Informational => "Banner only",
        _ => kind.ToString()
    };

    /// <summary>Unit category a typing or modification applies to; "All categories" for entries made before categories.</summary>
    public static string UnitCategory(SpecialRequirementCategory category, ComponentClass? componentClass) =>
        category is SpecialRequirementCategory.UnitTyping or SpecialRequirementCategory.UnitModification
            ? componentClass is { } c ? ComponentClassLabels.Label(c) : "All categories"
            : string.Empty;

    /// <summary>"Irradiated – RBC" when the name does not already say the unit category.</summary>
    public static string Choice(string name, SpecialRequirementCategory category, ComponentClass? componentClass)
    {
        var unitCategory = componentClass is { } c ? ComponentClassLabels.Label(c) : null;
        return unitCategory is null
            || category is not (SpecialRequirementCategory.UnitTyping or SpecialRequirementCategory.UnitModification)
            || name.Contains(unitCategory, StringComparison.OrdinalIgnoreCase)
            ? name
            : $"{name} – {unitCategory}";
    }
}
