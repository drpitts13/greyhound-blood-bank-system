using BloodBankLIS.Application.Reference;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Results;

/// <summary>
/// Derives the interpretation of an ABO/Rh, crossmatch or subtest panel from the cell
/// reactions entered on it. Each method returns null when the reactions are incomplete
/// or match no single interpretation, leaving the technologist's choice in place.
/// </summary>
public static class ReactionInterpretationSuggestion
{
    private const string NotTested = "NT";

    public const string FilledNote = "Interpretation filled in from the reactions. Check it before saving.";
    public const string NoMatchNote = "The reactions do not match a single interpretation yet. Finish the reactions or choose the interpretation yourself.";

    /// <summary>The interpretation key (as on <see cref="TestDefinitionForEntryDto.InterpretationOptions"/>) the reactions imply.</summary>
    public static string? SuggestKey(
        IReadOnlyList<InterpretationOptionDto> options,
        IEnumerable<ResolvedPanelSubtestDto> subtests,
        IReadOnlyDictionary<string, string> values,
        IReadOnlySet<string>? notPerformed = null)
    {
        var rows = options
            .Where(o => o.SubtestExpectations is { Count: > 0 })
            .Select(o => new InterpretationLogicRow(o.Key, o.Label, o.SubtestExpectations!, o.MatchMode))
            .ToList();
        return InterpretationLogicMatcher.Match(rows, InterpretiveReactions(subtests, values, notPerformed))?.InterpretationKey;
    }

    public static AboRh? SuggestAboRh(
        IReadOnlyList<InterpretationOptionDto> options,
        IEnumerable<ResolvedPanelSubtestDto> subtests,
        IReadOnlyDictionary<string, string> values,
        IReadOnlySet<string>? notPerformed = null)
    {
        var key = SuggestKey(options, subtests, values, notPerformed);
        return AboRhResultValue.TryParse(key, out var type) && type.IsKnown ? type : null;
    }

    public static CrossmatchResult? SuggestCrossmatch(
        IEnumerable<ResolvedPanelSubtestDto> subtests,
        IReadOnlyDictionary<string, string> values,
        IReadOnlySet<string>? notPerformed = null)
    {
        var reactions = InterpretiveReactions(subtests, values, notPerformed);
        var row = InterpretationLogicMatcher.Match(InterpretationLogicMatcher.CrossmatchLogic(reactions.Keys), reactions);
        return row?.InterpretationKey switch
        {
            InterpretationLogicMatcher.CrossmatchCompatibleKey => CrossmatchResult.Compatible,
            InterpretationLogicMatcher.CrossmatchIncompatibleKey => CrossmatchResult.Incompatible,
            _ => null
        };
    }

    /// <summary>
    /// The required readings not yet entered, by label ("Anti-A", "Screen cell I IAT"): a required reading starts blank,
    /// so a reaction nobody read is never saved as negative.
    /// </summary>
    public static IReadOnlyList<string> MissingReadings(
        IEnumerable<ResolvedPanelSubtestDto> subtests,
        IReadOnlyDictionary<string, string> values) =>
        CrossmatchReactionValidation.MissingRequired(subtests, values);

    /// <summary>The save refusal naming the readings still to enter, or null when every required reading is in.</summary>
    public static string? MissingReadingsMessage(IReadOnlyList<string> missing) =>
        missing.Count == 0 ? null : $"Enter a reading for {string.Join(", ", missing)} before saving.";

    /// <summary>
    /// Polarity of every graded reaction slot that counts toward interpretation. Check-cell
    /// phases, phases left out of interpretation and optional slots not tested are omitted;
    /// a required slot not yet entered maps to null.
    /// </summary>
    public static IReadOnlyDictionary<string, ReactionPolarity?> InterpretiveReactions(
        IEnumerable<ResolvedPanelSubtestDto> subtests,
        IReadOnlyDictionary<string, string> values,
        IReadOnlySet<string>? notPerformed = null)
    {
        var reactions = new Dictionary<string, ReactionPolarity?>(StringComparer.OrdinalIgnoreCase);
        foreach (var subtest in subtests)
        {
            if (subtest.ResultType != SubtestResultType.GradedReaction || !subtest.CountsInInterpretation)
            {
                continue;
            }

            if (subtest.Phases is { Count: > 0 })
            {
                foreach (var phase in subtest.Phases.Where(p => p.IncludeInInterpretation && !p.IsCheckCell))
                {
                    AddReaction(reactions, subtest, PhaseResultKeys.Compose(subtest.SubtestCode, phase.PhaseCode), phase.Required, values);
                }

                continue;
            }

            if (notPerformed?.Contains(subtest.SubtestCode) == true)
            {
                continue;
            }

            AddReaction(reactions, subtest, subtest.SubtestCode, subtest.Required, values);
        }

        return reactions;
    }

    private static void AddReaction(
        Dictionary<string, ReactionPolarity?> reactions,
        ResolvedPanelSubtestDto subtest,
        string key,
        bool required,
        IReadOnlyDictionary<string, string> values)
    {
        var entered = values.TryGetValue(key, out var v) ? v.Trim() : string.Empty;
        if (entered.Length == 0)
        {
            if (required)
            {
                reactions[key] = null;
            }

            return;
        }

        if (!required && string.Equals(entered, NotTested, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var choice = subtest.Choices.FirstOrDefault(c => string.Equals(c.Code, entered, StringComparison.OrdinalIgnoreCase));
        reactions[key] = choice?.Polarity ?? ReactionPolarity.Neutral;
    }
}
