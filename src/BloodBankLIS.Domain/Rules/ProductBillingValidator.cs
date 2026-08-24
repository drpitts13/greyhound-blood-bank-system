using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

public static class ProductBillingValidator
{
    public static RuleEvaluation Validate(ProductBilling row, bool duplicateActive)
    {
        var results = new List<RuleResult>();

        if (string.IsNullOrWhiteSpace(row.BillingCode))
        {
            results.Add(RuleResult.HardStop("PRODBILL.CODE.REQUIRED", "Billing code is required."));
        }

        if (string.IsNullOrWhiteSpace(row.IsbtProductCode))
        {
            results.Add(RuleResult.HardStop("PRODBILL.ISBT.REQUIRED", "ISBT product description code is required."));
        }
        else if (row.IsbtProductCode.Length > 5)
        {
            results.Add(RuleResult.HardStop("PRODBILL.ISBT.LENGTH",
                "ISBT product description code must be 5 characters or fewer."));
        }

        if (row.Trigger != BillingTriggerType.UnitIssued)
        {
            results.Add(RuleResult.HardStop("PRODBILL.TRIGGER.INVALID",
                "Product billing rows must use the UnitIssued trigger."));
        }

        if (row.Price is < 0)
        {
            results.Add(RuleResult.HardStop("PRODBILL.PRICE.NEGATIVE", "Price cannot be negative."));
        }

        if (duplicateActive)
        {
            results.Add(RuleResult.HardStop("PRODBILL.DUPLICATE",
                $"An active product billing row already uses trigger '{row.Trigger}', ISBT code '{row.IsbtProductCode}', and billing code '{row.BillingCode}'."));
        }

        return new RuleEvaluation(results);
    }
}
