using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

public static class TestServiceBillingValidator
{
    public static RuleEvaluation Validate(TestServiceBilling row, bool duplicateActive)
    {
        var results = new List<RuleResult>();

        if (string.IsNullOrWhiteSpace(row.BillingCode))
        {
            results.Add(RuleResult.HardStop("TESTBILL.CODE.REQUIRED", "Billing code is required."));
        }

        if (string.IsNullOrWhiteSpace(row.TestCode))
        {
            results.Add(RuleResult.HardStop("TESTBILL.TEST.REQUIRED", "Test code is required."));
        }

        if (row.Trigger != BillingTriggerType.TestVerified)
        {
            results.Add(RuleResult.HardStop("TESTBILL.TRIGGER.INVALID",
                "Test/service billing rows must use the TestVerified trigger."));
        }

        if (row.Price is < 0)
        {
            results.Add(RuleResult.HardStop("TESTBILL.PRICE.NEGATIVE", "Price cannot be negative."));
        }

        if (duplicateActive)
        {
            results.Add(RuleResult.HardStop("TESTBILL.DUPLICATE",
                $"An active test/service billing row already uses trigger '{row.Trigger}', test '{row.TestCode}', and billing code '{row.BillingCode}'."));
        }

        return new RuleEvaluation(results);
    }
}
