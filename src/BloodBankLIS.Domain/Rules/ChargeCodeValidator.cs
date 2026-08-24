using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Domain.Rules;

public static class ChargeCodeValidator
{
    public static RuleEvaluation Validate(ChargeCode code, bool duplicateCode)
    {
        var results = new List<RuleResult>();

        if (string.IsNullOrWhiteSpace(code.Code))
        {
            results.Add(RuleResult.HardStop("CHARGE.CODE.REQUIRED", "Charge code is required."));
        }

        if (string.IsNullOrWhiteSpace(code.Description))
        {
            results.Add(RuleResult.HardStop("CHARGE.DESCRIPTION.REQUIRED", "Description is required."));
        }

        if (code.DefaultAmount < 0)
        {
            results.Add(RuleResult.HardStop("CHARGE.AMOUNT.NEGATIVE", "Default amount cannot be negative."));
        }

        if (duplicateCode)
        {
            results.Add(RuleResult.HardStop("CHARGE.CODE.DUPLICATE", $"Another charge code already uses '{code.Code}'."));
        }

        return new RuleEvaluation(results);
    }
}
