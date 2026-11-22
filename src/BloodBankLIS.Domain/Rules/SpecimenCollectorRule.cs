namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Who collected a tube, when the facility requires it before the tube is accepted for testing
/// (<c>Specimen.RequireCollector</c>, OCD-064). Collecting and receiving are never blocked by it.
/// </summary>
public static class SpecimenCollectorRule
{
    public const string Code = "SPEC-COLLECTOR";

    public static RuleResult EvaluateAccept(string? collector, bool required) =>
        !required || !string.IsNullOrWhiteSpace(collector)
            ? RuleResult.Pass(Code)
            : RuleResult.HardStop(Code,
                "Record who collected this tube (the collector on the label) before accepting it, or reject the tube if the label does not show one.");
}
