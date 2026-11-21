using BloodBankLIS.Domain.Entities.Configuration;

namespace BloodBankLIS.Domain.Rules;

public static class SpecimenRejectionReasonValidator
{
    public const string CodeRequired = "REJECTREASON.CODE.REQUIRED";
    public const string CodeLength = "REJECTREASON.CODE.LENGTH";
    public const string TextRequired = "REJECTREASON.TEXT.REQUIRED";
    public const string TextLength = "REJECTREASON.TEXT.LENGTH";
    public const string DuplicateCode = "REJECTREASON.CODE.DUPLICATE";

    public static RuleEvaluation Validate(SpecimenRejectionReason reason, bool duplicateCode)
    {
        var results = new List<RuleResult>();
        if (string.IsNullOrWhiteSpace(reason.Code))
        {
            results.Add(RuleResult.HardStop(CodeRequired, "A rejection reason code is required."));
        }
        else if (reason.Code.Length > 20)
        {
            results.Add(RuleResult.HardStop(CodeLength, "Rejection reason code must be 20 characters or fewer."));
        }

        if (string.IsNullOrWhiteSpace(reason.Text))
        {
            results.Add(RuleResult.HardStop(TextRequired, "Rejection reason text is required."));
        }
        else if (reason.Text.Length > 500)
        {
            results.Add(RuleResult.HardStop(TextLength, "Rejection reason text must be 500 characters or fewer."));
        }

        if (duplicateCode)
        {
            results.Add(RuleResult.HardStop(DuplicateCode, $"Rejection reason code '{reason.Code}' is already in use."));
        }

        return new RuleEvaluation(results);
    }
}
