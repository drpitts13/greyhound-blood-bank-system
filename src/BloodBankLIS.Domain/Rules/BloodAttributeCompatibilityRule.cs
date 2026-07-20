using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Clinically significant antibody/antigen compatibility between patient and unit.
/// RBC/WB: patient antibody (current or historical) requires unit antigen-negative
/// (Warning <c>ISS-ANTIGEN-NEG</c>, overridable by supervisor+ via ExceptionDefinitions).
/// Plasma/platelets: unit antibody requires patient antigen-negative (Warning).
/// Granulocytes: patient antibody requires unit antigen-negative (Warning).
/// </summary>
public static class BloodAttributeCompatibilityRule
{
    public const string AntigenNegCode = "ISS-ANTIGEN-NEG";
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
        if (UsesRbcWbAntigenNeg(componentClass))
        {
            return EvaluateCellularAntigenNeg(patientAntibodies, unitAntigens);
        }

        if (UsesCellularWarning(componentClass))
        {
            return EvaluateCellularWarning(patientAntibodies, unitAntigens);
        }

        if (UsesPlasmaDirection(componentClass))
        {
            return EvaluatePlasma(unitAntibodies, patientAntigens);
        }

        return Array.Empty<RuleResult>();
    }

    private static bool UsesRbcWbAntigenNeg(ComponentClass componentClass) =>
        componentClass is ComponentClass.RedBloodCells or ComponentClass.WholeBlood;

    private static bool UsesCellularWarning(ComponentClass componentClass) =>
        componentClass is ComponentClass.Granulocytes;

    private static bool UsesPlasmaDirection(ComponentClass componentClass) =>
        componentClass is ComponentClass.Plasma or ComponentClass.Platelets;

    private static IReadOnlyList<RuleResult> EvaluateCellularAntigenNeg(
        IReadOnlyList<AntibodyRef> patientAntibodies,
        IReadOnlyList<AntigenRef> unitAntigens)
    {
        if (patientAntibodies.Count == 0)
        {
            return Array.Empty<RuleResult>();
        }

        // Case-sensitive: Rh C and c (and E/e) are distinct antigens.
        var unitByCode = unitAntigens.ToDictionary(a => a.Code, StringComparer.Ordinal);
        var results = new List<RuleResult>();

        foreach (var antibody in patientAntibodies)
        {
            if (!unitByCode.TryGetValue(antibody.Code, out var unitAntigen)
                || unitAntigen.Result != AntigenResult.Negative)
            {
                results.Add(RuleResult.Warning(
                    AntigenNegCode,
                    $"Patient has {antibody.AntibodyName}; unit must be antigen-negative for {antibody.Code}."));
            }
        }

        return results;
    }

    private static IReadOnlyList<RuleResult> EvaluateCellularWarning(
        IReadOnlyList<AntibodyRef> patientAntibodies,
        IReadOnlyList<AntigenRef> unitAntigens)
    {
        if (patientAntibodies.Count == 0)
        {
            return Array.Empty<RuleResult>();
        }

        var unitByCode = unitAntigens.ToDictionary(a => a.Code, StringComparer.Ordinal);
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

        var patientByCode = patientAntigens.ToDictionary(a => a.Code, StringComparer.Ordinal);
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
