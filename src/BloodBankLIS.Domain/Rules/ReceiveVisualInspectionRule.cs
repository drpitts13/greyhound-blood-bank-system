namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// AABB / SoftBank / SafeTrace intake: do not accept a unit that failed
/// visual inspection at receipt. Return it to the supplier.
/// </summary>
public static class ReceiveVisualInspectionRule
{
    public const string Code = "INV-RCV-VISUAL";

    public static RuleResult Evaluate(bool required, bool acceptable)
    {
        if (!required || acceptable)
        {
            return RuleResult.Pass(Code);
        }

        return RuleResult.HardStop(
            Code,
            "Do not receive a unit that failed visual inspection. Return it to the supplier.");
    }
}
