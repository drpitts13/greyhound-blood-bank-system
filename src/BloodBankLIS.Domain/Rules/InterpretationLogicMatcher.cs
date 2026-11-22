using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Picks the interpretation whose expected reaction pattern the entered cell reactions
/// satisfy, so result entry can fill the interpretation in from the reactions.
/// The reactions map holds one entry per interpretive slot on the panel: a polarity
/// when graded, or null when the slot is required but not yet entered. Expectation
/// keys missing from the map are not on the panel (or were not tested) and are skipped.
/// A suggestion is only returned when exactly one row matches; incomplete, mixed-field
/// or discrepant reactions return null so the technologist interprets them.
/// </summary>
public static class InterpretationLogicMatcher
{
    public const string CrossmatchCompatibleKey = "Compatible";
    public const string CrossmatchIncompatibleKey = "Incompatible";

    public static InterpretationLogicRow? Match(
        IReadOnlyList<InterpretationLogicRow> rows,
        IReadOnlyDictionary<string, ReactionPolarity?> reactions)
    {
        if (rows.Count == 0 || reactions.Count == 0)
        {
            return null;
        }

        var matches = rows.Where(r => Matches(r, reactions)).Take(2).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    /// <summary>
    /// Crossmatch logic over the panel's interpretive reactions: compatible when every
    /// reaction is negative, incompatible when any reaction is positive.
    /// </summary>
    public static IReadOnlyList<InterpretationLogicRow> CrossmatchLogic(IEnumerable<string> interpretiveKeys)
    {
        var negative = new Dictionary<string, ReactionPolarity>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in interpretiveKeys)
        {
            negative[key] = ReactionPolarity.Negative;
        }

        return
        [
            new InterpretationLogicRow(CrossmatchCompatibleKey, "Compatible", negative, InterpretationMatchMode.AllMatch),
            new InterpretationLogicRow(
                CrossmatchIncompatibleKey,
                "Incompatible",
                new Dictionary<string, ReactionPolarity>(StringComparer.OrdinalIgnoreCase),
                InterpretationMatchMode.AnyPositive)
        ];
    }

    private static bool Matches(InterpretationLogicRow row, IReadOnlyDictionary<string, ReactionPolarity?> reactions)
    {
        var applicable = row.SubtestExpectations
            .Where(kv => reactions.ContainsKey(kv.Key))
            .ToList();

        if (row.MatchMode == InterpretationMatchMode.AnyPositive)
        {
            // A required negative that is not entered yet does not block a positive finding.
            if (applicable.Any(kv => kv.Value == ReactionPolarity.Negative
                    && reactions[kv.Key] is ReactionPolarity entered
                    && entered != ReactionPolarity.Negative))
            {
                return false;
            }

            var candidates = applicable.Where(kv => kv.Value == ReactionPolarity.Positive).Select(kv => kv.Key).ToList();
            if (candidates.Count == 0)
            {
                candidates = reactions.Keys.ToList();
            }

            return candidates.Any(k => reactions.TryGetValue(k, out var p) && p == ReactionPolarity.Positive);
        }

        return applicable.Count > 0
            && applicable.All(kv => reactions[kv.Key] == kv.Value);
    }
}
