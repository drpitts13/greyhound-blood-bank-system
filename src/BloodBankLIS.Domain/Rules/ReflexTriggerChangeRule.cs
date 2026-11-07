using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A reflex test was added because a verified result matched a rule. When that result is later invalidated, or
/// corrected to a value that no longer matches, the reflex test may no longer be needed. The test is never cancelled
/// automatically (it may already be under way); the bench is told to check.
/// </summary>
public static class ReflexTriggerChangeRule
{
    public readonly record struct ResultVersion(long Id, long? SupersededByResultId, ResultStatus Status, string? Value, string? Interpretation);

    /// <summary>
    /// The notice for a reflex line, or null when its trigger still stands. Follows the trigger result's supersede chain
    /// to the current version.
    /// </summary>
    public static string? Notice(
        long triggerResultId,
        string? triggerTestCode,
        string? triggerValue,
        string reflexTestCode,
        IReadOnlyDictionary<long, ResultVersion> results)
    {
        if (!results.TryGetValue(triggerResultId, out var current))
        {
            return null;
        }

        var seen = new HashSet<long> { current.Id };
        while (current.SupersededByResultId is long next && results.TryGetValue(next, out var newer) && seen.Add(newer.Id))
        {
            current = newer;
        }

        if (current.Id == triggerResultId)
        {
            return null;
        }

        var trigger = string.IsNullOrWhiteSpace(triggerTestCode) ? "The result" : triggerTestCode.Trim();
        var reflex = reflexTestCode.Trim();
        if (current.Status == ResultStatus.Invalidated)
        {
            return $"The {trigger} result that added this test was invalidated. Check whether {reflex} is still needed.";
        }

        var expected = triggerValue?.Trim() ?? string.Empty;
        var value = current.Value?.Trim() ?? string.Empty;
        var interpretation = ResultInterpretation.Resolve(current.Interpretation, current.Value)?.Trim() ?? string.Empty;
        if (string.Equals(expected, value, StringComparison.OrdinalIgnoreCase)
            || string.Equals(expected, interpretation, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var shown = interpretation.Length > 0 ? interpretation : value;
        var verb = current.Status == ResultStatus.Verified ? "was corrected" : "is being corrected";
        return $"{trigger} {verb} to {shown} after this test was added. Check whether {reflex} is still needed.";
    }
}
