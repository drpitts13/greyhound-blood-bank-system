using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Evaluates persisted special transfusion requirements against unit attributes,
/// antigen types, issue acknowledgments, and patient ABO subgroup. Computer-enforced;
/// not an operator checkbox.
/// </summary>
public static class SpecialTransfusionRequirementRule
{
    public const string Code = IssueGate.SpecialReqCode;

    public sealed record RequirementRef(
        string Code,
        SpecialRequirementEnforcementKind EnforcementKind,
        string? ProductAttributeCode,
        string? AntigenCode,
        DateTime EffectiveUtc,
        DateTime? ExpiresUtc,
        bool IsActive,
        // Null applies to every unit category (entries made before categories).
        ComponentClass? ComponentClass = null,
        string? AllowedAboGroups = null,
        string? UnitTestCode = null,
        string? Name = null)
    {
        public string Label => string.IsNullOrWhiteSpace(Name) ? Code : Name!;

        public bool AppliesTo(ComponentClass? unitClass) =>
            ComponentClass is null || unitClass is null || ComponentClass == unitClass;

        public static RequirementRef FromLegacy(
            SpecialTransfusionRequirementType type,
            string? antigenCode,
            DateTime effectiveUtc,
            DateTime? expiresUtc,
            bool isActive) =>
            SpecialRequirementCatalog.ToRef(type, antigenCode, effectiveUtc, expiresUtc, isActive);
    }

    /// <summary>
    /// What the unit is, beyond its product attributes and antigens: its category, ABO/Rh, and the tests recorded
    /// negative on it (upper-case codes).
    /// </summary>
    public sealed record UnitTyping(
        ComponentClass ComponentClass,
        AboGroup Abo,
        RhType RhD,
        IReadOnlySet<string> NegativeTestCodes);

    /// <summary>Requirements judged against the unit itself: filtered at selection and enforced at issue.</summary>
    public static bool IsUnitFact(SpecialRequirementEnforcementKind kind) => kind
        is SpecialRequirementEnforcementKind.RequireProductAttribute
        or SpecialRequirementEnforcementKind.RequireAntigenNegative
        or SpecialRequirementEnforcementKind.RequireUnitAbo
        or SpecialRequirementEnforcementKind.RequireRhNegative
        or SpecialRequirementEnforcementKind.RequireUnitTestNegative;

    /// <summary>A test result recorded on a unit counts as negative when it reads Neg or Negative.</summary>
    public static bool IsNegativeResult(string? result) =>
        result?.Trim().ToUpperInvariant() is "NEG" or "NEGATIVE";

    /// <summary>Allowed ABO groups parsed from the definition's comma-separated list.</summary>
    public static IReadOnlySet<AboGroup> ParseAboGroups(string? groups) =>
        (groups ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(g => Enum.TryParse<AboGroup>(g, ignoreCase: true, out var abo) ? abo : AboGroup.Unknown)
            .Where(g => g != AboGroup.Unknown)
            .ToHashSet();

    public static IReadOnlyList<RuleResult> Evaluate(
        IReadOnlyList<RequirementRef> requirements,
        IReadOnlySet<string> unitProductAttributeCodes,
        IReadOnlyList<BloodAttributeCompatibilityRule.AntigenRef> unitAntigens,
        DateTime nowUtc,
        IReadOnlySet<string>? acknowledgedCodes = null,
        AboGroup patientAbo = AboGroup.Unknown,
        AboSubgroup patientSubgroup = AboSubgroup.Unknown,
        UnitTyping? unit = null)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(unitProductAttributeCodes);
        ArgumentNullException.ThrowIfNull(unitAntigens);

        var acks = acknowledgedCodes ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var active = requirements
            .Where(r => SpecialRequirementCatalog.IsClinicallyActive(r.IsActive, r.EffectiveUtc, r.ExpiresUtc, nowUtc))
            .Where(r => r.EnforcementKind != SpecialRequirementEnforcementKind.Informational)
            // A typing or modification for one unit category does not apply to units of another.
            .Where(r => !IsUnitFact(r.EnforcementKind) || r.AppliesTo(unit?.ComponentClass))
            .ToList();

        if (active.Count == 0)
        {
            return [RuleResult.Pass(Code)];
        }

        var results = new List<RuleResult>();
        var unitByAntigen = unitAntigens.ToDictionary(a => a.Code, StringComparer.Ordinal);

        foreach (var requirement in active)
        {
            switch (requirement.EnforcementKind)
            {
                case SpecialRequirementEnforcementKind.RequireProductAttribute:
                    var attr = requirement.ProductAttributeCode?.Trim();
                    if (string.IsNullOrEmpty(attr))
                    {
                        results.Add(RuleResult.HardStop(Code, $"Special requirement {requirement.Label} ({requirement.Code}) is missing a product attribute code."));
                        break;
                    }

                    AddIfMissing(results, unitProductAttributeCodes, attr, requirement.Label);
                    break;

                case SpecialRequirementEnforcementKind.RequireAntigenNegative:
                    var antigen = requirement.AntigenCode?.Trim();
                    if (string.IsNullOrEmpty(antigen))
                    {
                        results.Add(RuleResult.HardStop(Code, "Antigen-negative special requirement is missing an antigen code."));
                        break;
                    }

                    if (!unitByAntigen.TryGetValue(antigen, out var typed)
                        || typed.Result != AntigenResult.Negative)
                    {
                        results.Add(RuleResult.HardStop(Code, $"Patient requires {requirement.Label}: the unit is not typed {antigen} negative."));
                    }

                    break;

                case SpecialRequirementEnforcementKind.RequireUnitAbo:
                    var allowed = ParseAboGroups(requirement.AllowedAboGroups);
                    if (allowed.Count == 0)
                    {
                        results.Add(RuleResult.HardStop(Code, $"Special requirement {requirement.Label} has no allowed ABO group."));
                        break;
                    }

                    if (unit is null || !allowed.Contains(unit.Abo))
                    {
                        var groups = string.Join(" or ", allowed.OrderBy(g => g));
                        results.Add(RuleResult.HardStop(
                            Code,
                            $"Patient requires {requirement.Label}: the unit must be group {groups}, and it is {(unit is null ? "not known" : unit.Abo.ToString())}."));
                    }

                    break;

                case SpecialRequirementEnforcementKind.RequireRhNegative:
                    if (unit is null || unit.RhD != RhType.Negative)
                    {
                        results.Add(RuleResult.HardStop(Code, $"Patient requires {requirement.Label}: the unit is not RhD negative."));
                    }

                    break;

                case SpecialRequirementEnforcementKind.RequireUnitTestNegative:
                    var byProduct = requirement.ProductAttributeCode?.Trim() is { Length: > 0 } productCode
                        && unitProductAttributeCodes.Contains(productCode);
                    var byTest = requirement.UnitTestCode?.Trim().ToUpperInvariant() is { Length: > 0 } testCode
                        && unit is not null
                        && unit.NegativeTestCodes.Contains(testCode);
                    if (!byProduct && !byTest)
                    {
                        results.Add(RuleResult.HardStop(
                            Code,
                            $"Patient requires {requirement.Label}: neither the product code nor a test recorded on the unit shows it negative."));
                    }

                    break;

                case SpecialRequirementEnforcementKind.RequireIssueAcknowledgment:
                    if (!acks.Contains(requirement.Code))
                    {
                        results.Add(RuleResult.HardStop(
                            Code,
                            $"Active special requirement {requirement.Label} must be acknowledged at issue."));
                    }

                    break;

                case SpecialRequirementEnforcementKind.RequireAboSubgroup:
                    if (!SpecialRequirementCatalog.AboSubgroupSatisfied(patientAbo, patientSubgroup))
                    {
                        results.Add(RuleResult.HardStop(
                            Code,
                            "Patient requires A1/A2 subgroup typing before issue."));
                    }

                    break;

                case SpecialRequirementEnforcementKind.RequireComplexCrossmatch:
                    // Enforced at allocate / electronic XM, not as an issue-gate unit check.
                    break;
            }
        }

        return results.Count == 0 ? [RuleResult.Pass(Code)] : results;
    }

    private static void AddIfMissing(
        List<RuleResult> results,
        IReadOnlySet<string> codes,
        string expectedCode,
        string label)
    {
        if (!codes.Contains(expectedCode))
        {
            results.Add(RuleResult.HardStop(Code, $"Patient requires {label}: the unit's product does not carry {expectedCode}."));
        }
    }

    /// <summary>
    /// What each unmet unit typing or modification requires, for the issue gate. Empty when all are met.
    /// These may be overridden on emergency release; issue procedures may not.
    /// </summary>
    public static IReadOnlyList<string> UnmetUnitRequirements(
        IReadOnlyList<RequirementRef> requirements,
        IReadOnlySet<string> unitProductAttributeCodes,
        IReadOnlyList<BloodAttributeCompatibilityRule.AntigenRef> unitAntigens,
        DateTime nowUtc,
        UnitTyping? unit) =>
        Evaluate(
                requirements.Where(r => IsUnitFact(r.EnforcementKind)).ToList(),
                unitProductAttributeCodes,
                unitAntigens,
                nowUtc,
                unit: unit)
            .Where(r => r.Severity == RuleSeverity.HardStop)
            .Select(r => r.Message)
            .ToList();

    /// <summary>Issue procedures (acknowledgment, A1/A2 subgroup) are met. These are never overridden.</summary>
    public static bool ProceduresMet(
        IReadOnlyList<RequirementRef> requirements,
        DateTime nowUtc,
        IReadOnlySet<string>? acknowledgedCodes,
        AboGroup patientAbo,
        AboSubgroup patientSubgroup) =>
        AllMet(Evaluate(
            requirements.Where(r => !IsUnitFact(r.EnforcementKind)).ToList(),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            [],
            nowUtc,
            acknowledgedCodes,
            patientAbo,
            patientSubgroup));

    public static bool AllMet(IReadOnlyList<RuleResult> results) =>
        results.All(r => r.Severity == RuleSeverity.Pass);

    /// <summary>
    /// True when a unit fails an active unit typing or modification for its category.
    /// Acknowledgment, ABO subgroup, and complex crossmatch are not unit-selection filters.
    /// </summary>
    public static bool FailsUnitSelection(
        IReadOnlyList<RequirementRef> requirements,
        IReadOnlySet<string> unitProductAttributeCodes,
        IReadOnlyList<BloodAttributeCompatibilityRule.AntigenRef> unitAntigens,
        DateTime nowUtc,
        UnitTyping? unit = null)
    {
        var unitFacts = requirements.Where(r => IsUnitFact(r.EnforcementKind)).ToList();
        if (unitFacts.Count == 0)
        {
            return false;
        }

        return Evaluate(unitFacts, unitProductAttributeCodes, unitAntigens, nowUtc, unit: unit)
            .Any(r => r.Severity == RuleSeverity.HardStop);
    }

    /// <summary>
    /// Ready-to-issue notice for a unit-fact special requirement. Acknowledgment,
    /// ABO subgroup, and complex crossmatch are not unit facts and stay on the
    /// issue form. A met requirement has no notice.
    /// </summary>
    public static string? ReadyToIssueNotice(
        IReadOnlyList<RequirementRef> requirements,
        IReadOnlySet<string> unitProductAttributeCodes,
        IReadOnlyList<BloodAttributeCompatibilityRule.AntigenRef> unitAntigens,
        DateTime nowUtc,
        UnitTyping? unit = null)
    {
        var unitFacts = requirements.Where(r => IsUnitFact(r.EnforcementKind)).ToList();
        if (unitFacts.Count == 0)
        {
            return null;
        }

        return Evaluate(unitFacts, unitProductAttributeCodes, unitAntigens, nowUtc, unit: unit)
            .FirstOrDefault(r => r.Severity == RuleSeverity.HardStop)
            ?.Message;
    }
}
