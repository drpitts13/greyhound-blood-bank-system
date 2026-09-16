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
        bool IsActive)
    {
        public static RequirementRef FromLegacy(
            SpecialTransfusionRequirementType type,
            string? antigenCode,
            DateTime effectiveUtc,
            DateTime? expiresUtc,
            bool isActive) =>
            SpecialRequirementCatalog.ToRef(type, antigenCode, effectiveUtc, expiresUtc, isActive);
    }

    public static IReadOnlyList<RuleResult> Evaluate(
        IReadOnlyList<RequirementRef> requirements,
        IReadOnlySet<string> unitProductAttributeCodes,
        IReadOnlyList<BloodAttributeCompatibilityRule.AntigenRef> unitAntigens,
        DateTime nowUtc,
        IReadOnlySet<string>? acknowledgedCodes = null,
        AboGroup patientAbo = AboGroup.Unknown,
        AboSubgroup patientSubgroup = AboSubgroup.Unknown)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(unitProductAttributeCodes);
        ArgumentNullException.ThrowIfNull(unitAntigens);

        var acks = acknowledgedCodes ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var active = requirements
            .Where(r => SpecialRequirementCatalog.IsClinicallyActive(r.IsActive, r.EffectiveUtc, r.ExpiresUtc, nowUtc))
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
                        results.Add(RuleResult.HardStop(Code, $"Special requirement {requirement.Code} is missing a product attribute code."));
                        break;
                    }

                    AddIfMissing(results, unitProductAttributeCodes, attr, requirement.Code);
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
                        results.Add(RuleResult.HardStop(Code, $"Unit is not antigen-negative for required antigen {antigen}."));
                    }

                    break;

                case SpecialRequirementEnforcementKind.RequireIssueAcknowledgment:
                    if (!acks.Contains(requirement.Code))
                    {
                        results.Add(RuleResult.HardStop(
                            Code,
                            $"Active special requirement {requirement.Code} must be acknowledged at issue."));
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
            results.Add(RuleResult.HardStop(Code, $"Unit product attributes do not include required {label} ({expectedCode})."));
        }
    }

    public static bool AllMet(IReadOnlyList<RuleResult> results) =>
        results.All(r => r.Severity == RuleSeverity.Pass);
}
