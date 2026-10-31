using BloodBankLIS.Domain.Entities.Configuration;

namespace BloodBankLIS.Domain.Rules;

public static class UnitDiscardReasonValidator
{
    public const string CodeRequired = "DISCARD.CODE.REQUIRED";
    public const string CodeLength = "DISCARD.CODE.LENGTH";
    public const string TextRequired = "DISCARD.TEXT.REQUIRED";
    public const string TextLength = "DISCARD.TEXT.LENGTH";
    public const string DuplicateCode = "DISCARD.CODE.DUPLICATE";

    public static RuleEvaluation Validate(UnitDiscardReason reason, bool duplicateCode)
    {
        var results = new List<RuleResult>();
        if (string.IsNullOrWhiteSpace(reason.Code))
        {
            results.Add(RuleResult.HardStop(CodeRequired, "A discard reason code is required."));
        }
        else if (reason.Code.Length > 20)
        {
            results.Add(RuleResult.HardStop(CodeLength, "Discard reason code must be 20 characters or fewer."));
        }

        if (string.IsNullOrWhiteSpace(reason.Text))
        {
            results.Add(RuleResult.HardStop(TextRequired, "Discard reason text is required."));
        }
        else if (reason.Text.Length > 500)
        {
            results.Add(RuleResult.HardStop(TextLength, "Discard reason text must be 500 characters or fewer."));
        }

        if (duplicateCode)
        {
            results.Add(RuleResult.HardStop(DuplicateCode, $"Discard reason code '{reason.Code}' is already in use."));
        }

        return new RuleEvaluation(results);
    }
}
