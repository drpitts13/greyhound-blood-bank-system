namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Pure evaluation of blood-unit expiration. A unit is expired at or after its
/// expiration date/time. Reads no clock; the caller supplies the reference time so
/// the rule is deterministic and testable (see docs/safety-rules.md).
/// </summary>
public static class BloodUnitExpirationRule
{
    public const string ExpiredCode = "UNIT-EXPIRED";
    public const string NearExpiryCode = "UNIT-NEAR-EXPIRY";

    public static bool IsExpired(DateTime expiresUtc, DateTime nowUtc) => nowUtc >= expiresUtc;

    /// <summary>
    /// HardStop if the unit is at or past expiration; Warning if it expires within
    /// <paramref name="nearExpiryWindow"/>; otherwise Pass.
    /// </summary>
    public static RuleResult Evaluate(DateTime expiresUtc, DateTime nowUtc, TimeSpan nearExpiryWindow)
    {
        if (IsExpired(expiresUtc, nowUtc))
        {
            return RuleResult.HardStop(ExpiredCode, $"Unit expired at {expiresUtc:u}.");
        }

        if (expiresUtc - nowUtc <= nearExpiryWindow)
        {
            return RuleResult.Warning(NearExpiryCode, $"Unit expires soon, at {expiresUtc:u}.");
        }

        return RuleResult.Pass(ExpiredCode);
    }
}
