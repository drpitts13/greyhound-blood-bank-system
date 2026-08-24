using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Domain.Rules;

public static class ChargeRuleValidator
{
    public static RuleEvaluation Validate(ChargeRule rule, bool chargeCodeMissing, bool duplicateActive)
    {
        var results = new List<RuleResult>();

        if (rule.ChargeCodeId <= 0 || chargeCodeMissing)
        {
            results.Add(RuleResult.HardStop("CHARGE.RULE.CODE.REQUIRED", "A charge code is required."));
        }

        if (duplicateActive)
        {
            var key = string.IsNullOrWhiteSpace(rule.TriggerKey) ? "(any)" : rule.TriggerKey;
            results.Add(RuleResult.HardStop(
                "CHARGE.RULE.DUPLICATE",
                $"An active charge rule already maps {rule.TriggerType} / {key} to this charge code."));
        }

        return new RuleEvaluation(results);
    }
}
