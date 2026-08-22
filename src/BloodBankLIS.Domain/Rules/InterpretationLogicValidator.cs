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
        IReadOnlyDictionary<string, string> enteredSubtests,
        IReadOnlyDictionary<string, PhaseDefinition>? phasesByCode = null)
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

        var interpretive = row.SubtestExpectations
            .Where(kv => IsInterpretiveExpectation(kv.Key, phasesByCode))
            .ToList();

        if (row.MatchMode == InterpretationMatchMode.AnyPositive)
        {
            EvaluateAnyPositive(row, interpretive, catalogByCode, enteredSubtests, phasesByCode, results);
        }
        else
        {
            foreach (var (key, expected) in interpretive)
            {
                EvaluateExact(row, key, expected, catalogByCode, enteredSubtests, results);
            }
        }

        return new RuleEvaluation(results);
    }

    private static bool IsInterpretiveExpectation(
        string key,
        IReadOnlyDictionary<string, PhaseDefinition>? phasesByCode)
    {
        if (phasesByCode is null || !PhaseResultKeys.TrySplit(key, out _, out var phaseCode))
        {
            return true;
        }

        return !phasesByCode.TryGetValue(phaseCode, out var phase)
            || (phase.IncludeInInterpretation && !phase.IsCheckCell);
    }

    private static void EvaluateAnyPositive(
        InterpretationLogicRow row,
        IReadOnlyList<KeyValuePair<string, ReactionPolarity>> interpretive,
        IReadOnlyDictionary<string, SubtestDefinition> catalogByCode,
        IReadOnlyDictionary<string, string> enteredSubtests,
        IReadOnlyDictionary<string, PhaseDefinition>? phasesByCode,
        List<RuleResult> results)
    {
        foreach (var (key, expected) in interpretive.Where(kv => kv.Value == ReactionPolarity.Negative))
        {
            EvaluateExact(row, key, expected, catalogByCode, enteredSubtests, results);
        }

        var candidates = interpretive
            .Where(kv => kv.Value == ReactionPolarity.Positive)
            .Select(kv => kv.Key)
            .ToList();

        if (candidates.Count == 0)
        {
            candidates = enteredSubtests.Keys
                .Where(k => IsInterpretiveExpectation(k, phasesByCode))
                .ToList();
        }

        var anyPositive = false;
        foreach (var key in candidates)
        {
            var polarity = ResolveEnteredPolarity(key, catalogByCode, enteredSubtests, out var warning);
            if (warning is not null)
            {
                results.Add(warning);
                continue;
            }

            if (polarity == ReactionPolarity.Positive)
            {
                anyPositive = true;
            }
        }

        if (!anyPositive)
        {
            results.Add(RuleResult.HardStop(
                "INTERPRETATION.MISMATCH",
                $"Interpretation '{row.Label}' expects at least one positive reaction."));
        }
    }

    private static void EvaluateExact(
        InterpretationLogicRow row,
        string key,
        ReactionPolarity expected,
        IReadOnlyDictionary<string, SubtestDefinition> catalogByCode,
        IReadOnlyDictionary<string, string> enteredSubtests,
        List<RuleResult> results)
    {
        var polarity = ResolveEnteredPolarity(key, catalogByCode, enteredSubtests, out var warning);
        if (warning is not null)
        {
            results.Add(warning);
            return;
        }

        if (polarity is null or ReactionPolarity.Neutral)
        {
            return;
        }

        if (polarity.Value != expected)
        {
            var display = PhaseResultKeys.TrySplit(key, out var subtest, out var phase)
                ? $"{subtest} {phase}"
                : ResolveSubtestName(key, catalogByCode);
            results.Add(RuleResult.HardStop(
                "INTERPRETATION.MISMATCH",
                $"Interpretation '{row.Label}' expects {display} to be {expected}, but entered value is {polarity.Value}."));
        }
    }

    private static ReactionPolarity? ResolveEnteredPolarity(
        string key,
        IReadOnlyDictionary<string, SubtestDefinition> catalogByCode,
        IReadOnlyDictionary<string, string> enteredSubtests,
        out RuleResult? warning)
    {
        warning = null;
        var catalogCode = PhaseResultKeys.TrySplit(key, out var subtestCode, out _)
            ? subtestCode
            : key;

        if (!catalogByCode.TryGetValue(catalogCode, out var def)
            || def.ResultType != SubtestResultType.GradedReaction)
        {
            return null;
        }

        if (!enteredSubtests.TryGetValue(key, out var enteredValue)
            || string.IsNullOrWhiteSpace(enteredValue))
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
        if (choice is null)
        {
            warning = RuleResult.Warning(
                "INTERPRETATION.GRADE.UNKNOWN",
                $"Subtest '{def.Name}' has unrecognized value '{enteredValue}'.");
            return null;
        }

        return choice.Polarity;
    }

    private static string ResolveSubtestName(string code, IReadOnlyDictionary<string, SubtestDefinition> catalogByCode) =>
        catalogByCode.TryGetValue(code, out var def) ? def.Name : code;
}
