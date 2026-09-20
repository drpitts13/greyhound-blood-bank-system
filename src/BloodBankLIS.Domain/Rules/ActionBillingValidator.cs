using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

public static class ActionBillingValidator
{
    public const string CodeRequired = "ACTBILL.CODE.REQUIRED";
    public const string ActionRequired = "ACTBILL.ACTION.REQUIRED";
    public const string TriggerInvalid = "ACTBILL.TRIGGER.INVALID";
    public const string Duplicate = "ACTBILL.DUPLICATE";

    public static RuleEvaluation Validate(ActionBilling row, bool chargeCodeMissing, bool actionMissing, bool duplicateActive)
    {
        var results = new List<RuleResult>();

        if (row.ChargeCodeId <= 0 || chargeCodeMissing)
        {
            results.Add(RuleResult.HardStop(CodeRequired, "A charge code is required."));
        }

        if (row.ActionDefinitionId <= 0 || actionMissing)
        {
            results.Add(RuleResult.HardStop(ActionRequired, "An action is required."));
        }

        if (row.Trigger != BillingTriggerType.ActionCompleted)
        {
            results.Add(RuleResult.HardStop(TriggerInvalid,
                "Action billing rows must use the ActionCompleted trigger."));
        }

        if (duplicateActive)
        {
            results.Add(RuleResult.HardStop(Duplicate,
                "An active action billing row already uses this action and charge code."));
        }

        return new RuleEvaluation(results);
    }
}
