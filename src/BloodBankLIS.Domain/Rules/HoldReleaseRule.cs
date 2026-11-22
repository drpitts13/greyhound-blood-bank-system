namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Releasing a unit from operational hold says how the hold was resolved, so the record keeps both why the unit was
/// held and why it was let go. An expired unit is not released to stock; it is discarded.
/// </summary>
public static class HoldReleaseRule
{
    public const string Code = "HOLD-RELEASE";

    public const string ExpiredNextStep = "Expired: discard it.";

    public static RuleResult Evaluate(string? releaseReason, bool expired)
    {
        if (expired)
        {
            return RuleResult.HardStop(Code, "This unit has expired, so it cannot go back to stock. Discard it.");
        }

        return string.IsNullOrWhiteSpace(releaseReason)
            ? RuleResult.HardStop(Code, "Say how the hold was resolved, for example that the paperwork arrived.")
            : RuleResult.Pass(Code);
    }
}
