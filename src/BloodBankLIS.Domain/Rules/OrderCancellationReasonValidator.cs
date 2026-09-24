using BloodBankLIS.Domain.Entities.Configuration;

namespace BloodBankLIS.Domain.Rules;

public static class OrderCancellationReasonValidator
{
    public const string CodeRequired = "OCANCEL.CODE.REQUIRED";
    public const string CodeLength = "OCANCEL.CODE.LENGTH";
    public const string TextRequired = "OCANCEL.TEXT.REQUIRED";
    public const string TextLength = "OCANCEL.TEXT.LENGTH";
    public const string DuplicateCode = "OCANCEL.CODE.DUPLICATE";

    public static RuleEvaluation Validate(OrderCancellationReason reason, bool duplicateCode)
    {
        var results = new List<RuleResult>();
        if (string.IsNullOrWhiteSpace(reason.Code))
        {
            results.Add(RuleResult.HardStop(CodeRequired, "A cancellation reason code is required."));
        }
        else if (reason.Code.Length > 20)
        {
            results.Add(RuleResult.HardStop(CodeLength, "Cancellation reason code must be 20 characters or fewer."));
        }

        if (string.IsNullOrWhiteSpace(reason.Text))
        {
            results.Add(RuleResult.HardStop(TextRequired, "Cancellation reason text is required."));
        }
        else if (reason.Text.Length > 500)
        {
            results.Add(RuleResult.HardStop(TextLength, "Cancellation reason text must be 500 characters or fewer."));
        }

        if (duplicateCode)
        {
            results.Add(RuleResult.HardStop(DuplicateCode, $"Cancellation reason code '{reason.Code}' is already in use."));
        }

        return new RuleEvaluation(results);
    }
}
