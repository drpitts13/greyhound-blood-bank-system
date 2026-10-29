namespace BloodBankLIS.Domain.Rules;

public static class BatchModificationRule
{
    public const string Code = "BATCH-MOD";

    public static RuleResult Evaluate(int distinctUnitCount)
    {
        if (distinctUnitCount >= 2)
        {
            return RuleResult.Pass(Code);
        }

        return RuleResult.HardStop(Code, "Apply one modification rule to at least two units.");
    }
}
