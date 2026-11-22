using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Completing a supplier delivery (end-user testing pass 83: a delivery short of its packing list was completed with no
/// record of the difference). When what was entered differs from what was expected, completing needs a note saying why
/// (supplier short, credit requested, unit refused, ...); a delivery that matches needs none.
/// </summary>
public static class DeliveryCompletionRule
{
    public const string Code = "DELIVERY-VARIANCE";
    public const int MaxNoteLength = 500;

    public sealed record Line(string Product, int Expected, int Received);

    /// <summary>The products that differ, for example "RBC 2 short; Plasma 1 over"; null when every line matches.</summary>
    public static string? Differences(IEnumerable<Line> lines)
    {
        var parts = lines
            .Where(l => l.Received != l.Expected)
            .Select(l => l.Received < l.Expected ? $"{l.Product} {l.Expected - l.Received} short" : $"{l.Product} {l.Received - l.Expected} over")
            .ToList();
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    public static RuleResult Evaluate(IEnumerable<Line> lines, string? note)
    {
        var differences = Differences(lines);
        if (differences is null)
        {
            return RuleResult.Pass(Code);
        }

        if (string.IsNullOrWhiteSpace(note))
        {
            return RuleResult.HardStop(Code,
                $"The delivery does not match its packing list ({differences}). Say why before completing it, for example supplier short and credit requested.");
        }

        return note.Trim().Length > MaxNoteLength
            ? RuleResult.HardStop(Code, $"The note can be at most {MaxNoteLength} characters.")
            : RuleResult.Pass(Code);
    }
}
