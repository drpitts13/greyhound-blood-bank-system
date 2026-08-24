using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

public static class ProductBillingValidator
{
    public static RuleEvaluation Validate(ProductBilling row, bool chargeCodeMissing, bool duplicateActive)
    {
        var results = new List<RuleResult>();

        if (row.ChargeCodeId <= 0 || chargeCodeMissing)
        {
            results.Add(RuleResult.HardStop("PRODBILL.CODE.REQUIRED", "A charge code is required."));
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

        if (duplicateActive)
        {
            results.Add(RuleResult.HardStop("PRODBILL.DUPLICATE",
                $"An active product billing row already uses trigger '{row.Trigger}', ISBT code '{row.IsbtProductCode}', and this charge code."));
        }

        return new RuleEvaluation(results);
    }
}
