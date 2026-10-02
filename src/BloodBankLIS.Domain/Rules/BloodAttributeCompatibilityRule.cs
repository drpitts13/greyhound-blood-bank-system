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
    public const string UntestedFlag = "Untested";
    public const string NotNegativeFlag = "NotNegative";
    public const string MixedFlag = "Mixed";

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
                results.Add(DescribeCellularGap(antibody, unitAntigen));
            }
        }

        return results;
    }

    /// <summary>
    /// Untested, not-antigen-negative, or both, from <see cref="AntigenNegCode"/> warnings.
    /// </summary>
    public static string? SummarizeAntigenNegFlag(IEnumerable<RuleResult> results)
    {
        var untested = false;
        var notNegative = false;
        foreach (var result in results)
        {
            if (result.Code != AntigenNegCode || result.Severity != RuleSeverity.Warning)
            {
                continue;
            }

            if (result.Message.Contains("not tested", StringComparison.Ordinal))
            {
                untested = true;
            }
            else
            {
                notNegative = true;
            }
        }

        if (untested && notNegative)
        {
            return MixedFlag;
        }

        if (untested)
        {
            return UntestedFlag;
        }

        return notNegative ? NotNegativeFlag : null;
    }

    private static RuleResult DescribeCellularGap(AntibodyRef antibody, AntigenRef? unitAntigen)
    {
        if (unitAntigen is null || unitAntigen.Result == AntigenResult.NotTested)
        {
            return RuleResult.Warning(
                AntigenNegCode,
                $"Patient has {antibody.AntibodyName}; unit is not tested for {antibody.Code} and must be antigen-negative.");
        }

        return RuleResult.Warning(
            AntigenNegCode,
            $"Patient has {antibody.AntibodyName}; unit is {unitAntigen.Result} for {antibody.Code} and must be antigen-negative.");
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

    /// <summary>
    /// Ready-to-issue notice for an antigen warning. Uses the same evaluation as
    /// issue. A unit that is already antigen-negative has no notice. The warning
    /// stays overridable.
    /// </summary>
    public static string? ReadyToIssueNotice(
        ComponentClass componentClass,
        IReadOnlyList<AntibodyRef> patientAntibodies,
        IReadOnlyList<AntigenRef> patientAntigens,
        IReadOnlyList<AntibodyRef> unitAntibodies,
        IReadOnlyList<AntigenRef> unitAntigens)
    {
        var messages = Evaluate(componentClass, patientAntibodies, patientAntigens, unitAntibodies, unitAntigens)
            .Where(r => r.Severity == RuleSeverity.Warning)
            .Select(r => r.Message)
            .ToList();
        return messages.Count == 0 ? null : string.Join(" ", messages);
    }
}
