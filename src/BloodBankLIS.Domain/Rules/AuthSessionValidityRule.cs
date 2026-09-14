namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Pure session-acceptability checks. Application services supply clock and user
/// state; this rule never reads the database.
/// </summary>
public static class AuthSessionValidityRule
{
    public const string RevokedCode = "AUTH-SESSION-REVOKED";
    public const string AbsoluteExpiredCode = "AUTH-SESSION-ABS-EXPIRED";
    public const string IdleExpiredCode = "AUTH-SESSION-IDLE";
    public const string InactiveCode = "AUTH-SESSION-INACTIVE";
    public const string LockedCode = "AUTH-SESSION-LOCKED";

    public static RuleResult Evaluate(
        DateTime nowUtc,
        DateTime? revokedUtc,
        DateTime absoluteExpiresUtc,
        DateTime idleExpiresUtc,
        bool userIsActive,
        bool userIsLocked)
    {
        if (revokedUtc is not null)
        {
            return RuleResult.HardStop(RevokedCode, "The session was revoked.");
        }

        if (nowUtc >= absoluteExpiresUtc)
        {
            return RuleResult.HardStop(AbsoluteExpiredCode, "The session has reached its absolute lifetime.");
        }

        if (nowUtc >= idleExpiresUtc)
        {
            return RuleResult.HardStop(IdleExpiredCode, "The session expired after idle timeout.");
        }

        if (!userIsActive)
        {
            return RuleResult.HardStop(InactiveCode, "The account is inactive.");
        }

        if (userIsLocked)
        {
            return RuleResult.HardStop(LockedCode, "The account is locked.");
        }

        return RuleResult.Pass("AUTH-SESSION-OK", "The session is valid.");
    }
}
