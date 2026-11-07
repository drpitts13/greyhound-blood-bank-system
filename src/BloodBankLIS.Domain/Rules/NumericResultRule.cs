using System.Globalization;
using BloodBankLIS.Domain.Entities.Configuration;

namespace BloodBankLIS.Domain.Rules;

/// <summary>The numeric limits a test definition sets. Every one is optional and the facility's own.</summary>
public sealed record NumericLimits(
    int? DecimalPlaces = null,
    decimal? ReportableLow = null,
    decimal? ReportableHigh = null,
    decimal? ReferenceLow = null,
    decimal? ReferenceHigh = null,
    decimal? CriticalLow = null,
    decimal? CriticalHigh = null)
{
    public static NumericLimits From(TestDefinition d) => new(
        d.DecimalPlaces, d.ReportableLow, d.ReportableHigh, d.ReferenceLow, d.ReferenceHigh, d.CriticalLow, d.CriticalHigh);

    /// <summary>"reference 0.00–0.30": the reference range as entry shows it, or null when none is set.</summary>
    public string? ReferenceText(string? units)
    {
        if (ReferenceLow is null && ReferenceHigh is null)
        {
            return null;
        }

        var range = (ReferenceLow, ReferenceHigh) switch
        {
            ({ } low, { } high) => $"{Format(low)}–{Format(high)}",
            ({ } low, null) => $"≥ {Format(low)}",
            (null, { } high) => $"≤ {Format(high)}",
            _ => string.Empty
        };
        return string.IsNullOrWhiteSpace(units) ? range : $"{range} {units.Trim()}";
    }

    public string Format(decimal value) =>
        DecimalPlaces is int places ? value.ToString("F" + places, CultureInfo.InvariantCulture) : value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>A checked numeric value: the parsed number, its flag, and the entry outcome.</summary>
public sealed record NumericEvaluation(decimal? Value, string? Flag, RuleResult Result, RuleResult? Critical)
{
    public bool IsAllowed => Result.Severity != RuleSeverity.HardStop;
}

/// <summary>
/// Checks a numeric result against its test's limits: decimal places and the reportable range refuse entry; the
/// reference range flags L or H; critical limits flag LL or HH and warn. Read with the invariant culture.
/// </summary>
public static class NumericResultRule
{
    public const string FormatCode = "RESULT-NUM-FORMAT";
    public const string DecimalsCode = "RESULT-NUM-DECIMALS";
    public const string ReportableCode = "RESULT-NUM-REPORTABLE";
    public const string CriticalCode = "RESULT-NUM-CRITICAL";

    public const string Low = "L";
    public const string High = "H";
    public const string CriticalLowFlag = "LL";
    public const string CriticalHighFlag = "HH";

    public static NumericEvaluation Evaluate(string? text, NumericLimits limits, string? units = null)
    {
        var raw = text?.Trim() ?? string.Empty;
        if (!decimal.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            return new(null, null, RuleResult.HardStop(FormatCode, $"\"{raw}\" is not a number. Enter digits, with a point for decimals."), null);
        }

        if (limits.DecimalPlaces is int places && Places(raw) > places)
        {
            return new(value, null, RuleResult.HardStop(DecimalsCode,
                places == 0 ? "Enter a whole number." : $"Enter at most {places} decimal place{(places == 1 ? "" : "s")}."), null);
        }

        var unitText = string.IsNullOrWhiteSpace(units) ? string.Empty : $" {units.Trim()}";
        if (limits.ReportableLow is decimal reportLow && value < reportLow)
        {
            return new(value, null, RuleResult.HardStop(ReportableCode,
                $"{raw}{unitText} is below the reportable range (from {limits.Format(reportLow)}{unitText}). Repeat the test, or report it as below {limits.Format(reportLow)}{unitText}."), null);
        }

        if (limits.ReportableHigh is decimal reportHigh && value > reportHigh)
        {
            return new(value, null, RuleResult.HardStop(ReportableCode,
                $"{raw}{unitText} is above the reportable range (to {limits.Format(reportHigh)}{unitText}). Dilute and repeat, or report it as above {limits.Format(reportHigh)}{unitText}."), null);
        }

        var flag = FlagFor(value, limits);
        var critical = flag is CriticalLowFlag or CriticalHighFlag
            ? RuleResult.Warning(CriticalCode,
                $"{raw}{unitText} is {(flag == CriticalLowFlag ? "below" : "above")} the critical limit. Confirm the value and notify as your procedure requires.")
            : null;
        return new(value, flag, RuleResult.Pass(FormatCode), critical);
    }

    /// <summary>LL or HH beyond a critical limit, else L or H outside the reference range, else none.</summary>
    public static string? FlagFor(decimal value, NumericLimits limits)
    {
        if (limits.CriticalLow is decimal criticalLow && value < criticalLow)
        {
            return CriticalLowFlag;
        }

        if (limits.CriticalHigh is decimal criticalHigh && value > criticalHigh)
        {
            return CriticalHighFlag;
        }

        if (limits.ReferenceLow is decimal low && value < low)
        {
            return Low;
        }

        return limits.ReferenceHigh is decimal high && value > high ? High : null;
    }

    private static int Places(string raw)
    {
        var point = raw.IndexOf('.');
        return point < 0 ? 0 : raw.Length - point - 1;
    }
}
