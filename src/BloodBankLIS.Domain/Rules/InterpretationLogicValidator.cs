using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Validates that entered subtest reactions match the interpretation logic table
/// for FDA/AABB-style discrepancy detection.
/// </summary>
public static class InterpretationLogicValidator
{
    public static RuleEvaluation Validate(
        IReadOnlyList<InterpretationLogicRow> logicRows,
        IReadOnlyDictionary<string, SubtestDefinition> catalogByCode,
        string interpretationKey,
        IReadOnlyDictionary<string, string> enteredSubtests)
    {
        var results = new List<RuleResult>();
        if (logicRows.Count == 0 || string.IsNullOrWhiteSpace(interpretationKey))
        {
            return new RuleEvaluation(results);
        }

        var row = logicRows.FirstOrDefault(r =>
            string.Equals(r.InterpretationKey, interpretationKey, StringComparison.OrdinalIgnoreCase));
        if (row is null)
        {
            return new RuleEvaluation(results);
        }

        foreach (var (subtestCode, expected) in row.SubtestExpectations)
        {
            if (!catalogByCode.TryGetValue(subtestCode, out var def)
                || def.ResultType != SubtestResultType.GradedReaction)
            {
                continue;
            }

            if (!enteredSubtests.TryGetValue(subtestCode, out var enteredValue)
                || string.IsNullOrWhiteSpace(enteredValue))
            {
                continue;
            }

            var choices = SubtestChoiceDefinitions.Parse(def.ChoicesJson);
            if (choices.Count == 0)
            {
                choices = SubtestChoiceDefinitions.DefaultGradedReaction();
            }

            var choice = choices.FirstOrDefault(c =>
                string.Equals(c.Code, enteredValue.Trim(), StringComparison.OrdinalIgnoreCase));
            if (choice is null)
            {
                results.Add(RuleResult.Warning(
                    "INTERPRETATION.GRADE.UNKNOWN",
                    $"Subtest '{def.Name}' has unrecognized value '{enteredValue}'."));
                continue;
            }

            if (choice.Polarity is null or ReactionPolarity.Neutral)
            {
                continue;
            }

            if (choice.Polarity.Value != expected)
            {
                results.Add(RuleResult.HardStop(
                    "INTERPRETATION.MISMATCH",
                    $"Interpretation '{row.Label}' expects {def.Name} to be {expected}, but entered '{enteredValue}' is {choice.Polarity.Value}."));
            }
        }

        return new RuleEvaluation(results);
    }
}
