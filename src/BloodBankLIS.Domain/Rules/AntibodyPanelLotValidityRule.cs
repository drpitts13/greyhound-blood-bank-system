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

    /// <summary>
    /// Lots already attached to an open workup. A deactivated lot HardStops
    /// complete and review. Clock expiry warns and does not identify antibodies.
    /// </summary>
    public static RuleResult EvaluateOpenWorkup(
        bool isActive,
        DateOnly expiresOn,
        DateOnly today,
        string lotNumber)
    {
        if (!isActive)
        {
            return RuleResult.HardStop(
                InactiveCode,
                $"Antibody panel lot {lotNumber} is inactive and cannot remain the reagent of record. Void this workup or attach an in-date active lot.");
        }

        if (expiresOn < today)
        {
            return RuleResult.Warning(
                ExpiredCode,
                $"Antibody panel lot {lotNumber} expired on {expiresOn:yyyy-MM-dd}. Completing still posts only technologist-Identified antibodies. Confirm the reactions were recorded while the lot was in date.");
        }

        return RuleResult.Pass(ExpiredCode);
    }
}
