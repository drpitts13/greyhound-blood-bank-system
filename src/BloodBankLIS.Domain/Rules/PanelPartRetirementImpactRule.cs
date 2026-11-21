using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Which active tests' panels still use a subtest or phase being retired (end-user testing loop 2 tick 24). Retiring is
/// not refused; the tests are named so their panels can be updated.
/// </summary>
public static class PanelPartRetirementImpactRule
{
    public const string Code = "PANEL-PART-RETIRE-IN-USE";

    public sealed record PanelTest(string Code, IReadOnlyList<PanelSubtestAssignment> Panel);

    public static IReadOnlyList<RuleResult> SubtestWarnings(string subtestCode, IEnumerable<PanelTest> activeTests) =>
        Warn("subtest", subtestCode, activeTests.Where(t => t.Panel.Any(a => Same(a.SubtestCode, subtestCode))));

    public static IReadOnlyList<RuleResult> PhaseWarnings(string phaseCode, IEnumerable<PanelTest> activeTests) =>
        Warn("phase", phaseCode, activeTests.Where(t => t.Panel.Any(a => (a.PhaseCodes ?? []).Any(p => Same(p, phaseCode)))));

    private static IReadOnlyList<RuleResult> Warn(string kind, string code, IEnumerable<PanelTest> using_)
    {
        var tests = using_.Select(t => t.Code).OrderBy(c => c).ToList();
        if (tests.Count == 0)
        {
            return [];
        }

        return
        [
            RuleResult.Warning(Code,
                $"The {kind} {code.Trim()} is still in the panel of {(tests.Count == 1 ? "1 test" : $"{tests.Count} tests")} ({string.Join(", ", tests)}). Update {(tests.Count == 1 ? "its panel" : "their panels")} on Tests.")
        ];
    }

    private static bool Same(string? a, string b) => string.Equals(a?.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
