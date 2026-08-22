using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Ensures required interpretive phases are present at result entry.
/// Check-cell phases remain optional.
/// </summary>
public static class PanelPhaseEntryValidator
{
    public static RuleEvaluation ValidateRequired(
        IReadOnlyList<PanelSubtestAssignment> assignments,
        IReadOnlyDictionary<string, PhaseDefinition> phasesByCode,
        IReadOnlyDictionary<string, string> enteredSubtests)
    {
        var results = new List<RuleResult>();
        var missing = new List<string>();

        foreach (var assignment in assignments.Where(a => a.Required))
        {
            if (assignment.PhaseCodes is not { Count: > 0 })
            {
                if (!HasValue(enteredSubtests, assignment.SubtestCode))
                {
                    missing.Add(assignment.SubtestCode);
                }

                continue;
            }

            foreach (var raw in assignment.PhaseCodes)
            {
                var phaseCode = raw.Trim();
                if (phasesByCode.TryGetValue(phaseCode, out var phase)
                    && (phase.IsCheckCell || !phase.IncludeInInterpretation))
                {
                    continue;
                }

                var key = PhaseResultKeys.Compose(assignment.SubtestCode, phaseCode);
                if (!HasValue(enteredSubtests, key))
                {
                    missing.Add($"{assignment.SubtestCode} {phaseCode}");
                }
            }
        }

        if (missing.Count > 0)
        {
            results.Add(RuleResult.HardStop(
                "PANEL.PHASE.REQUIRED",
                $"Required reactions missing: {string.Join(", ", missing)}."));
        }

        return new RuleEvaluation(results);
    }

    private static bool HasValue(IReadOnlyDictionary<string, string> entered, string key) =>
        entered.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value);
}
