using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Clinically significant antibody/antigen compatibility between patient and unit.
/// Cellular products: patient antibody requires unit antigen-negative.
/// Plasma/platelets: unit antibody requires patient antigen-negative.
/// </summary>
public static class BloodAttributeCompatibilityRule
{
    public const string CodePrefix = "COMPAT-ATTR-";

    public sealed record AntibodyRef(string Code, string AntibodyName);

    public sealed record AntigenRef(string Code, AntigenResult Result);

    public static IReadOnlyList<RuleResult> Evaluate(
        ComponentClass componentClass,
        IReadOnlyList<AntibodyRef> patientAntibodies,
        IReadOnlyList<AntigenRef> patientAntigens,
        IReadOnlyList<AntibodyRef> unitAntibodies,
        IReadOnlyList<AntigenRef> unitAntigens)
    {
        if (UsesCellularDirection(componentClass))
        {
            return EvaluateCellular(patientAntibodies, unitAntigens);
        }

        if (UsesPlasmaDirection(componentClass))
        {
            return EvaluatePlasma(unitAntibodies, patientAntigens);
        }

        return Array.Empty<RuleResult>();
    }

    private static bool UsesCellularDirection(ComponentClass componentClass) =>
        componentClass is ComponentClass.RedBloodCells
            or ComponentClass.WholeBlood
            or ComponentClass.Granulocytes;

    private static bool UsesPlasmaDirection(ComponentClass componentClass) =>
        componentClass is ComponentClass.Plasma or ComponentClass.Platelets;

    private static IReadOnlyList<RuleResult> EvaluateCellular(
        IReadOnlyList<AntibodyRef> patientAntibodies,
        IReadOnlyList<AntigenRef> unitAntigens)
    {
        if (patientAntibodies.Count == 0)
        {
            return Array.Empty<RuleResult>();
        }

        var unitByCode = unitAntigens.ToDictionary(a => a.Code, StringComparer.OrdinalIgnoreCase);
        var results = new List<RuleResult>();

        foreach (var antibody in patientAntibodies)
        {
            if (!unitByCode.TryGetValue(antibody.Code, out var unitAntigen)
                || unitAntigen.Result != AntigenResult.Negative)
            {
                results.Add(RuleResult.Warning(
                    CodePrefix + antibody.Code.ToUpperInvariant(),
                    $"Patient has {antibody.AntibodyName}; unit must be antigen-negative for {antibody.Code}."));
            }
        }

        return results;
    }

    private static IReadOnlyList<RuleResult> EvaluatePlasma(
        IReadOnlyList<AntibodyRef> unitAntibodies,
        IReadOnlyList<AntigenRef> patientAntigens)
    {
        if (unitAntibodies.Count == 0)
        {
            return Array.Empty<RuleResult>();
        }

        var patientByCode = patientAntigens.ToDictionary(a => a.Code, StringComparer.OrdinalIgnoreCase);
        var results = new List<RuleResult>();

        foreach (var antibody in unitAntibodies)
        {
            if (!patientByCode.TryGetValue(antibody.Code, out var patientAntigen)
                || patientAntigen.Result != AntigenResult.Negative)
            {
                results.Add(RuleResult.Warning(
                    CodePrefix + antibody.Code.ToUpperInvariant(),
                    $"Unit has {antibody.AntibodyName}; patient must be antigen-negative for {antibody.Code}."));
            }
        }

        return results;
    }
}
