namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A product that requires ABO/Rh retype cannot be reserved to a patient until a
/// verified retype matches the unit label. Emergency and MTP issue are the only
/// release path for a unit that has not been retyped.
/// </summary>
public static class ProductRetypeAllocationRule
{
    public const string Code = "ALLOC-RETYPE";

    public static RuleResult Evaluate(bool requiresRetype, bool hasVerifiedMatchingRetype)
    {
        if (!requiresRetype || hasVerifiedMatchingRetype)
        {
            return RuleResult.Pass(Code);
        }

        return RuleResult.HardStop(
            Code,
            "Unit has not been retyped and cannot be allocated. Complete the ABO/Rh retype, or release it under the emergency issue protocol.");
    }

    public static RuleResult EvaluateForIssue(bool retypeConfirmed, bool isEmergencyRelease)
    {
        if (retypeConfirmed)
        {
            return RuleResult.Pass(Code);
        }

        return isEmergencyRelease
            ? RuleResult.Warning(
                Code,
                "Unit has not been retyped. Emergency or MTP issue requires an authorized override.")
            : RuleResult.HardStop(
                Code,
                "Unit has not been retyped and cannot be issued except under the emergency protocol.");
    }
}
