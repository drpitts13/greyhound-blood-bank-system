using System.Text.RegularExpressions;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A test result recorded on a unit after it arrived (for example CMV or HbS). Results are Negative or Positive.
/// Each recording is kept; the latest one for a test code is the unit's result, so a correction never overwrites
/// the earlier row and needs a reason. Unit typings in the special requirements read the latest result.
/// </summary>
public static partial class UnitSpecialTestRule
{
    public const string Code = "UNIT-TEST";
    public const int ReasonMinimumLength = 8;
    public const string Negative = "Negative";
    public const string Positive = "Positive";

    [GeneratedRegex("^[A-Z0-9-]{1,20}$")]
    private static partial Regex CodePattern();

    public static string NormalizeCode(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>Negative or Positive, whatever the case or abbreviation; null when it is neither.</summary>
    public static string? NormalizeResult(string? result) => result?.Trim().ToUpperInvariant() switch
    {
        "NEG" or "NEGATIVE" => Negative,
        "POS" or "POSITIVE" => Positive,
        _ => null
    };

    /// <param name="previousResult">The unit's current result for this test, if one is recorded.</param>
    public static RuleResult Evaluate(string? testCode, string? result, string? previousResult, string? reason)
    {
        var code = NormalizeCode(testCode);
        if (!CodePattern().IsMatch(code))
        {
            return RuleResult.HardStop(Code, "Give the test code: letters, digits, or dashes, up to 20 (for example CMV or HBS).");
        }

        var normalized = NormalizeResult(result);
        if (normalized is null)
        {
            return RuleResult.HardStop(Code, "The result must be Negative or Positive.");
        }

        if (previousResult is null)
        {
            return RuleResult.Pass(Code);
        }

        if (string.Equals(NormalizeResult(previousResult), normalized, StringComparison.Ordinal))
        {
            return RuleResult.HardStop(Code, $"{code} is already recorded {normalized} on this unit.");
        }

        return string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < ReasonMinimumLength
            ? RuleResult.HardStop(Code,
                $"{code} is recorded {NormalizeResult(previousResult) ?? previousResult} on this unit. Give a reason of at least {ReasonMinimumLength} characters to correct it.")
            : RuleResult.Pass(Code);
    }
}
