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

        if (!string.IsNullOrWhiteSpace(code.RevenueCode)
            && (code.RevenueCode.Length is < 3 or > 4 || !code.RevenueCode.All(char.IsDigit)))
        {
            results.Add(RuleResult.HardStop("CHARGE.REVENUE.FORMAT", "Revenue code must be 3 or 4 digits (UB-04)."));
        }

        if (!string.IsNullOrWhiteSpace(code.Modifier) && code.Modifier.Length > 2)
        {
            results.Add(RuleResult.HardStop("CHARGE.MODIFIER.LENGTH", "Procedure modifier cannot exceed 2 characters."));
        }

        return new RuleEvaluation(results);
    }
}
