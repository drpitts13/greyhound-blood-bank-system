namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Reagent antibody-identification panels must be in-date and active before a
/// new workup is opened. Expired lots are a HardStop — they are not used.
/// </summary>
public static class AntibodyPanelLotValidityRule
{
    public const string ExpiredCode = "ABID-LOT-EXPIRED";
    public const string InactiveCode = "ABID-LOT-INACTIVE";

    public static RuleResult Evaluate(bool isActive, DateOnly expiresOn, DateOnly today)
    {
        if (!isActive)
        {
            return RuleResult.HardStop(InactiveCode, "The antibody panel lot is inactive and cannot be used.");
        }

        // Lots remain usable through the expiration date (end of that calendar day).
        if (expiresOn < today)
        {
            return RuleResult.HardStop(
                ExpiredCode,
                $"Antibody panel lot expired on {expiresOn:yyyy-MM-dd} and cannot be used for a new workup.");
        }

        return RuleResult.Pass(ExpiredCode);
    }
}
