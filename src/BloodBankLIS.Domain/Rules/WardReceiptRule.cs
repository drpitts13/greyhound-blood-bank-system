namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// SoftBank/SafeTrace remote-issue custody: transfusion documentation is blocked
/// until the receiving location acknowledges the unit.
/// </summary>
public static class WardReceiptRule
{
    public const string Code = "TX-WARD-RECEIPT";

    public static RuleResult Evaluate(bool required, bool received)
    {
        if (!required || received)
        {
            return RuleResult.Pass(Code);
        }

        return RuleResult.HardStop(
            Code,
            "The receiving location must acknowledge the unit before transfusion can be documented.");
    }
}
