using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A crossmatch recorded as Compatible must not carry a positive reaction: the same logic that fills the result in
/// from the reactions (<see cref="InterpretationLogicMatcher.CrossmatchLogic"/>, incompatible when any counted reaction
/// is positive), applied when the result is saved. Check cells, and phases left out of interpretation, are not counted
/// (check cells react positive on a valid negative AHG). Mixed field and Not tested are neutral and never stop.
/// </summary>
public static class CrossmatchReactionRule
{
    public const string PositiveOnCompatibleCode = "XM-POSITIVE-COMPATIBLE";

    /// <summary>The counted reactions entered as positive, as "{subtest} {grade}" ("AHG 2+", "Cell1 37°C 1+").</summary>
    public static IReadOnlyList<string> PositiveReactions(
        IReadOnlyDictionary<string, string> entered,
        IReadOnlyDictionary<string, SubtestDefinition> catalogByCode,
        IReadOnlyDictionary<string, PhaseDefinition> phasesByCode)
    {
        var positive = new List<string>();
        foreach (var (key, raw) in entered)
        {
            var value = raw?.Trim() ?? string.Empty;
            if (value.Length == 0)
            {
                continue;
            }

            string subtestCode;
            string? phaseName = null;
            if (PhaseResultKeys.TrySplit(key, out var subtest, out var phaseCode))
            {
                subtestCode = subtest;
                if (phasesByCode.TryGetValue(phaseCode, out var phase))
                {
                    if (phase.IsCheckCell || !phase.IncludeInInterpretation)
                    {
                        continue;
                    }

                    phaseName = phase.Name;
                }
                else
                {
                    phaseName = phaseCode;
                }
            }
            else
            {
                subtestCode = key;
                if (!CountsInInterpretation(key, phasesByCode))
                {
                    continue;
                }
            }

            if (!catalogByCode.TryGetValue(subtestCode, out var def) || def.ResultType != SubtestResultType.GradedReaction)
            {
                continue;
            }

            var choices = SubtestChoiceDefinitions.Parse(def.ChoicesJson);
            if (choices.Count == 0)
            {
                choices = SubtestChoiceDefinitions.DefaultGradedReaction();
            }

            var choice = choices.FirstOrDefault(c => string.Equals(c.Code, value, StringComparison.OrdinalIgnoreCase));
            if (choice?.Polarity == ReactionPolarity.Positive)
            {
                positive.Add(phaseName is null ? $"{def.Name} {choice.Label}" : $"{def.Name} {phaseName} {choice.Label}");
            }
        }

        return positive;
    }

    /// <summary>
    /// Whether a reading entered as its own subtest counts toward interpretation: not when its code is a check-cell
    /// phase, or a phase left out of interpretation (the crossmatch's "Check cells").
    /// </summary>
    public static bool CountsInInterpretation(string subtestCode, IReadOnlyDictionary<string, PhaseDefinition> phasesByCode) =>
        !phasesByCode.TryGetValue(subtestCode, out var asPhase) || (!asPhase.IsCheckCell && asPhase.IncludeInInterpretation);

    /// <summary>The hard stop for a Compatible result with positive reactions, or null when there is none to stop.</summary>
    public static RuleResult? Evaluate(CrossmatchResult result, IReadOnlyList<string> positiveReactions) =>
        result == CrossmatchResult.Compatible && positiveReactions.Count > 0
            ? RuleResult.HardStop(
                PositiveOnCompatibleCode,
                $"A crossmatch with a positive reaction ({string.Join(", ", positiveReactions)}) cannot be recorded as Compatible. Record it as Incompatible.")
            : null;
}
