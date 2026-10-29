namespace BloodBankLIS.Domain.Rules;

public static class ModificationSupplyLotRule
{
    public const string Code = "MOD-SUPPLY-LOT";

    public static RuleResult Evaluate(bool required, string? supplyLot)
    {
        if (!required || !string.IsNullOrWhiteSpace(supplyLot))
        {
            return RuleResult.Pass(Code);
        }

        return RuleResult.HardStop(Code, "A supply lot is required for this modification.");
    }
}
