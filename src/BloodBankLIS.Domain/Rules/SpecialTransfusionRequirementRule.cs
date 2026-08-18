using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Evaluates persisted special transfusion requirements against the unit's product
/// attributes and antigen types. Computer-enforced; not an operator checkbox.
/// </summary>
public static class SpecialTransfusionRequirementRule
{
    public const string Code = IssueGate.SpecialReqCode;

    public sealed record RequirementRef(
        SpecialTransfusionRequirementType Type,
        string? AntigenCode,
        DateTime EffectiveUtc,
        DateTime? ExpiresUtc,
        bool IsActive);

    public static IReadOnlyList<RuleResult> Evaluate(
        IReadOnlyList<RequirementRef> requirements,
        IReadOnlySet<string> unitProductAttributeCodes,
        IReadOnlyList<BloodAttributeCompatibilityRule.AntigenRef> unitAntigens,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(unitProductAttributeCodes);
        ArgumentNullException.ThrowIfNull(unitAntigens);

        var active = requirements
            .Where(r => r.IsActive && r.EffectiveUtc <= nowUtc && (r.ExpiresUtc is null || r.ExpiresUtc > nowUtc))
            .ToList();

        if (active.Count == 0)
        {
            return [RuleResult.Pass(Code)];
        }

        var results = new List<RuleResult>();
        var unitByAntigen = unitAntigens.ToDictionary(a => a.Code, StringComparer.Ordinal);

        foreach (var requirement in active)
        {
            switch (requirement.Type)
            {
                case SpecialTransfusionRequirementType.Irradiated:
                    AddIfMissing(results, unitProductAttributeCodes, "IRRAD", "irradiated");
                    break;
                case SpecialTransfusionRequirementType.CmvNegative:
                    AddIfMissing(results, unitProductAttributeCodes, "CMVNEG", "CMV-negative");
                    break;
                case SpecialTransfusionRequirementType.Leukoreduced:
                    AddIfMissing(results, unitProductAttributeCodes, "LR", "leukoreduced");
                    break;
                case SpecialTransfusionRequirementType.Washed:
                    AddIfMissing(results, unitProductAttributeCodes, "WASHED", "washed");
                    break;
                case SpecialTransfusionRequirementType.AntigenNegative:
                    var antigen = requirement.AntigenCode?.Trim();
                    if (string.IsNullOrEmpty(antigen))
                    {
                        results.Add(RuleResult.HardStop(Code, "Antigen-negative special requirement is missing an antigen code."));
                        break;
                    }

                    if (!unitByAntigen.TryGetValue(antigen, out var typed)
                        || typed.Result != AntigenResult.Negative)
                    {
                        results.Add(RuleResult.HardStop(Code, $"Unit is not antigen-negative for required antigen {antigen}."));
                    }

                    break;
                case SpecialTransfusionRequirementType.Other:
                    results.Add(RuleResult.Warning(
                        Code,
                        "An 'Other' special requirement is active and must be confirmed against the unit."));
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
            results.Add(RuleResult.HardStop(Code, $"Unit product attributes do not include required {label} ({expectedCode})."));
        }
    }

    public static bool AllMet(IReadOnlyList<RuleResult> results) =>
        results.All(r => r.Severity == RuleSeverity.Pass);
}
