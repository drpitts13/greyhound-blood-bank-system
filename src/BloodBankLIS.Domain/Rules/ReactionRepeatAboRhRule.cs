using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Compares a recorded reaction-workup repeat ABO/Rh to the current patient type
/// or the labeled unit type. Disagreement is a Warning so the board can cue
/// clerical / WBIT review. Close remains gated only by
/// <see cref="ReactionWorkupCompletenessRule"/>.
/// </summary>
public static class ReactionRepeatAboRhRule
{
    public const string Code = "RXN-REPEAT-ABORH";

    public static RuleResult Evaluate(AboRh? expected, string? repeatText, string subject)
    {
        if (string.IsNullOrWhiteSpace(repeatText))
        {
            return RuleResult.Pass(Code, "Repeat ABO/Rh not yet recorded.");
        }

        if (expected is null || !expected.Value.IsKnown)
        {
            return RuleResult.Pass(Code, "No labeled or current ABO/Rh to compare.");
        }

        if (!TryParseDisplay(repeatText, out var repeat) || !repeat.IsKnown)
        {
            return RuleResult.Warning(
                Code,
                $"{subject} repeat ABO/Rh '{repeatText.Trim()}' could not be compared to {expected.Value}.");
        }

        return expected.Value == repeat
            ? RuleResult.Pass(Code)
            : RuleResult.Warning(
                Code,
                $"{subject} repeat ABO/Rh {repeat} disagrees with {expected.Value}.");
    }

    public static bool TryParseDisplay(string? value, out AboRh result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (AboRhResultValue.TryParse(value, out result) && result.IsKnown)
        {
            return true;
        }

        var compact = ReplaceInsensitive(value.Trim().Replace('|', ' '), "positive", "+");
        compact = ReplaceInsensitive(compact, "pos", "+");
        compact = ReplaceInsensitive(compact, "negative", "-");
        compact = ReplaceInsensitive(compact, "neg", "-");
        compact = compact
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("RHD", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("RH", string.Empty, StringComparison.OrdinalIgnoreCase);

        RhType rh;
        if (compact.EndsWith('+'))
        {
            rh = RhType.Positive;
        }
        else if (compact.EndsWith('-'))
        {
            rh = RhType.Negative;
        }
        else
        {
            return false;
        }

        var aboPart = compact[..^1];
        AboGroup abo;
        if (aboPart.Equals("AB", StringComparison.OrdinalIgnoreCase))
        {
            abo = AboGroup.AB;
        }
        else if (aboPart.Equals("A", StringComparison.OrdinalIgnoreCase))
        {
            abo = AboGroup.A;
        }
        else if (aboPart.Equals("B", StringComparison.OrdinalIgnoreCase))
        {
            abo = AboGroup.B;
        }
        else if (aboPart.Equals("O", StringComparison.OrdinalIgnoreCase))
        {
            abo = AboGroup.O;
        }
        else
        {
            return false;
        }

        result = new AboRh(abo, rh);
        return true;
    }

    private static string ReplaceInsensitive(string input, string oldValue, string newValue)
    {
        var remaining = input;
        while (true)
        {
            var idx = remaining.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return remaining;
            }

            remaining = string.Concat(
                remaining.AsSpan(0, idx),
                newValue,
                remaining.AsSpan(idx + oldValue.Length));
        }
    }
}
