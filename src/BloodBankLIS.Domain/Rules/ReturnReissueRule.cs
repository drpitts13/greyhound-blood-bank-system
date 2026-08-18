namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Return-to-inventory reissue eligibility. The operator's "reissue eligible" flag is
/// ignored; the computer evaluates temperature, seal, visual inspection, time out of
/// storage, and expiration.
/// </summary>
public static class ReturnReissueRule
{
    public const string Code = "RET-REISSUE";

    public static RuleResult Evaluate(
        bool temperatureAcceptable,
        bool sealIntegrityAcceptable,
        bool visualInspectionAcceptable,
        bool timeOutOfStorageAcceptable,
        bool unitUnexpired)
    {
        if (!visualInspectionAcceptable || !sealIntegrityAcceptable)
        {
            return RuleResult.HardStop(Code, "A unit that fails visual inspection or seal integrity cannot be returned to available inventory.");
        }

        if (temperatureAcceptable && timeOutOfStorageAcceptable && unitUnexpired)
        {
            return RuleResult.Pass(Code);
        }

        return RuleResult.Warning(Code, "Return conditions require quarantine rather than return to available inventory.");
    }
}
