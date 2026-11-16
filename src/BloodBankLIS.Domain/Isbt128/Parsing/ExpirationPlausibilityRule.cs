using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Isbt128.Parsing;

/// <summary>
/// A scanned ISBT 128 expiration that resolves to an implausible year (end-user testing pass 161: the label code
/// <c>&amp;&gt;0263312359</c> resolved to 1926 under the century mapping then awaiting ICCBBA confirmation). OCD-092 was
/// decided 2026-11-16: century digit 0 is the 2000s and 9 the 1900s, and the parser now reads it so. This check stays as
/// a safety net: a scan whose year is more than two years before or more than eleven years after today is refused with
/// what to check (frozen red cells can carry a ten-year expiry).
/// </summary>
public static class ExpirationPlausibilityRule
{
    public const string Code = "ISBT_IMPLAUSIBLE_EXPIRATION";

    public static RuleResult Evaluate(int year, DateTime nowUtc) =>
        year < nowUtc.Year - 2 || year > nowUtc.Year + 11
            ? RuleResult.HardStop(Code,
                $"The scanned expiration reads as year {year}, which cannot be right for a unit in stock. Check the eye-readable "
                + "expiration and the label's century digit (0 is the 2000s, 9 the 1900s; OCD-092) and enter it by hand if they disagree.")
            : RuleResult.Pass(Code);
}
