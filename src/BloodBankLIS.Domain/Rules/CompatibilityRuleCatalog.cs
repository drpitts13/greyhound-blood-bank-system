using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Documented compatibility table rows (SafeTrace / SoftBank compatibility dictionaries).
/// Runtime issue still uses domain rules; these codes are the versioned catalog.
/// </summary>
public sealed record CompatibilityRuleDefinition(
    string RuleCode,
    ComponentClass ComponentClass,
    string RuleFamily,
    string Severity,
    string Description,
    string Citation);

public static class CompatibilityRuleCatalog
{
    public static readonly IReadOnlyList<CompatibilityRuleDefinition> Defaults =
    [
        new(AboCompatibilityRule.AboCode, ComponentClass.RedBloodCells, "ABO", "HardStop",
            "Recipient/donor ABO must be compatible for red cells (AABB 5.14).",
            "AABB 5.14 / 21 CFR 606.151"),
        new(AboCompatibilityRule.RhCode, ComponentClass.RedBloodCells, "Rh", "HardStop",
            "Rh(D) is a hard constraint for RBC and whole blood.",
            "AABB 5.14"),
        new(AboCompatibilityRule.UnknownTypeCode, ComponentClass.RedBloodCells, "ABO", "HardStop",
            "Current recipient and unit ABO/Rh must be known except emergency / MTP.",
            "AABB 5.14 / CAP TRM.40650"),
        new($"{AboCompatibilityRule.AboCode}.PLASMA", ComponentClass.Plasma, "ABO", "HardStop",
            "Plasma ABO is reverse-compatible (unit antibodies vs recipient antigens).",
            "AABB 5.14"),
        new($"{AboCompatibilityRule.AboCode}.PLT", ComponentClass.Platelets, "ABO", "Warning",
            "Platelet ABO mismatch is typically a warning unless facility policy elevates it.",
            "AABB 5.14"),
        new($"{AboCompatibilityRule.AboCode}.CRYO", ComponentClass.Cryoprecipitate, "ABO", "Warning",
            "Cryoprecipitate ABO mismatch is typically a warning.",
            "AABB 5.14"),
        new($"{AboCompatibilityRule.AboCode}.WB", ComponentClass.WholeBlood, "ABO", "HardStop",
            "Whole blood requires bidirectional ABO compatibility.",
            "AABB 5.14"),
        new($"{AboCompatibilityRule.RhCode}.WB", ComponentClass.WholeBlood, "Rh", "HardStop",
            "Rh(D) is a hard constraint for whole blood.",
            "AABB 5.14"),
        new(BloodAttributeCompatibilityRule.AntigenNegCode, ComponentClass.RedBloodCells, "Antigen", "HardStop",
            "Antigen-negative units required when the patient has a corresponding antibody.",
            "AABB 5.14 / 21 CFR 606.151"),
        new(ElectronicCrossmatchEligibilityRule.Code, ComponentClass.RedBloodCells, "ElectronicXM", "HardStop",
            "Computer XM requires two concordant ABO/Rh, a negative screen, and no antibody history.",
            "AABB 5.16"),
        new(AntibodyHistoryCrossmatchRule.RuleCode, ComponentClass.RedBloodCells, "SerologicXM", "HardStop",
            "Antibody history requires a serologic (complex) crossmatch unless overridden.",
            "AABB 5.14 / CAP TRM.40670"),
        new(SecondAboDeterminationRule.IssueCode, ComponentClass.RedBloodCells, "SecondABO", "HardStop",
            "RBC issue requires two concordant ABO/Rh determinations except emergency or MTP.",
            "AABB 5.14 / CAP TRM.40650"),
        new($"{SecondAboDeterminationRule.IssueCode}.WB", ComponentClass.WholeBlood, "SecondABO", "HardStop",
            "Whole-blood issue requires two concordant ABO/Rh determinations except emergency or MTP.",
            "AABB 5.14 / CAP TRM.40650"),
        new(EmergencyUncrossmatchedAboRule.AboCode, ComponentClass.RedBloodCells, "EmergencyABO", "Warning",
            "Uncrossmatched red cells should be group O.",
            "AABB 5.27 / CAP TRM.40770"),
        new(EmergencyUncrossmatchedAboRule.RhCode, ComponentClass.RedBloodCells, "EmergencyRh", "Warning",
            "Uncrossmatched red cells for childbearing-potential recipients should be RhD-negative unless the patient is known Rh-positive.",
            "AABB 5.27 / CAP TRM.40770")
    ];

    public static readonly IReadOnlySet<string> Severities =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "HardStop", "Warning", "Pass" };

    public static CompatibilityRuleDefinition? Find(string ruleCode) =>
        Defaults.FirstOrDefault(d => string.Equals(d.RuleCode, ruleCode, StringComparison.OrdinalIgnoreCase));
}
