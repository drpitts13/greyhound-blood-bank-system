namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Pure evaluation of specimen expiration. Reads no clock or database; the
/// caller passes the reference time so the rule is fully deterministic and
/// exhaustively testable (see docs/safety-rules.md section 2).
/// </summary>
public static class SpecimenExpirationRule
{
    public const string ExpiredCode = "SPEC-EXPIRED";
    public const string NearExpiryCode = "SPEC-NEAR-EXPIRY";

    /// <summary>
    /// HardStop if the specimen is at or past its expiration; Warning if it
    /// expires within <paramref name="nearExpiryWindow"/>; otherwise Pass.
    /// A specimen with no expiration set cannot be validated, which is a HardStop.
    /// </summary>
    public static RuleResult Evaluate(DateTime? expiresUtc, DateTime nowUtc, TimeSpan nearExpiryWindow)
    {
        if (expiresUtc is null)
        {
            return RuleResult.HardStop(ExpiredCode, "Specimen has no expiration date and cannot be validated.");
        }

        if (nowUtc >= expiresUtc.Value)
        {
            return RuleResult.HardStop(ExpiredCode, $"Specimen expired at {expiresUtc.Value:u}.");
        }

        if (expiresUtc.Value - nowUtc <= nearExpiryWindow)
        {
            return RuleResult.Warning(NearExpiryCode, $"Specimen expires soon, at {expiresUtc.Value:u}.");
        }

        return RuleResult.Pass(ExpiredCode);
    }
}
