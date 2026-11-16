using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Isbt128.Parsing;

/// <summary>
/// A scanned ISBT 128 expiration that resolves to an implausible year (end-user testing pass 161: the label code
/// <c>&amp;&gt;0263312359</c>, which ICCBBA labels in use read as 2026 day 331, resolved to 1926-11-27 under the century
/// mapping still awaiting ICCBBA confirmation, OCD-092). The century rule is not changed here; a scan whose year is more
/// than two years before or more than eleven years after today is refused with what to check (frozen red cells can
/// carry a ten-year expiry), instead of reading as a unit that expired a century ago.
/// </summary>
public static class ExpirationPlausibilityRule
{
    public const string Code = "ISBT_IMPLAUSIBLE_EXPIRATION";

    public static RuleResult Evaluate(int year, DateTime nowUtc) =>
        year < nowUtc.Year - 2 || year > nowUtc.Year + 11
            ? RuleResult.HardStop(Code,
                $"The scanned expiration reads as year {year}, which cannot be right for a unit in stock. Check the eye-readable "
                + "expiration on the label and enter it by hand; the label's century digit may be read differently (OCD-092).")
            : RuleResult.Pass(Code);
}
