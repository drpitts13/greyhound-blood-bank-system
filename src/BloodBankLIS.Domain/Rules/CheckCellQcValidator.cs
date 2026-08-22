using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Coombs-control QC: when AHG (or the configured validated phase) is Negative
/// and check cells are entered, they must be Positive. Blank check cells are allowed.
/// </summary>
public static class CheckCellQcValidator
{
    public static RuleEvaluation Validate(
        IReadOnlyList<PanelSubtestAssignment> assignments,
        IReadOnlyDictionary<string, PhaseDefinition> phasesByCode,
        IReadOnlyDictionary<string, SubtestDefinition> catalogByCode,
        IReadOnlyDictionary<string, string> enteredSubtests)
    {
        var results = new List<RuleResult>();
        if (assignments.Count == 0 || phasesByCode.Count == 0)
        {
            return new RuleEvaluation(results);
        }

        foreach (var assignment in assignments)
        {
            if (assignment.PhaseCodes is not { Count: > 0 })
            {
                continue;
            }

            foreach (var rawPhase in assignment.PhaseCodes)
            {
                var phaseCode = rawPhase.Trim();
                if (!phasesByCode.TryGetValue(phaseCode, out var phase) || !phase.IsCheckCell)
                {
                    continue;
                }

                var ccKey = PhaseResultKeys.Compose(assignment.SubtestCode, phase.Code);
                if (!enteredSubtests.TryGetValue(ccKey, out var ccValue) || string.IsNullOrWhiteSpace(ccValue))
                {
                    continue;
                }

                var validatedCode = string.IsNullOrWhiteSpace(phase.ValidatesPhaseCode)
                    ? "AHG"
                    : phase.ValidatesPhaseCode.Trim();
                var validatedKey = PhaseResultKeys.Compose(assignment.SubtestCode, validatedCode);
                if (!enteredSubtests.TryGetValue(validatedKey, out var validatedValue)
                    || string.IsNullOrWhiteSpace(validatedValue))
                {
                    continue;
                }

                var validatedPolarity = ResolvePolarity(assignment.SubtestCode, validatedValue, catalogByCode);
                if (validatedPolarity != ReactionPolarity.Negative)
                {
                    continue;
                }

                var ccPolarity = ResolvePolarity(assignment.SubtestCode, ccValue, catalogByCode);
                if (ccPolarity != ReactionPolarity.Positive)
                {
                    results.Add(RuleResult.HardStop(
                        "CHECKCELL.INVALID",
                        $"{assignment.SubtestCode} {validatedCode} is Negative, so check cells must be Positive (entered '{ccValue}')."));
                }
            }
        }

        return new RuleEvaluation(results);
    }

    private static ReactionPolarity? ResolvePolarity(
        string subtestCode,
        string enteredValue,
        IReadOnlyDictionary<string, SubtestDefinition> catalogByCode)
    {
        if (!catalogByCode.TryGetValue(subtestCode, out var def)
            || def.ResultType != SubtestResultType.GradedReaction)
        {
            return null;
        }

        var choices = SubtestChoiceDefinitions.Parse(def.ChoicesJson);
        if (choices.Count == 0)
        {
            choices = SubtestChoiceDefinitions.DefaultGradedReaction();
        }

        var choice = choices.FirstOrDefault(c =>
            string.Equals(c.Code, enteredValue.Trim(), StringComparison.OrdinalIgnoreCase));
        return choice?.Polarity;
    }
}
