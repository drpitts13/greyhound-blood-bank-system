using BloodBankLIS.Domain.Entities.Configuration;

namespace BloodBankLIS.Domain.Rules;

public static class CodedCommentDefinitionValidator
{
    public const int MaxCodeLength = 20;
    public const int MaxCommentTextLength = 500;

    public static RuleEvaluation Validate(CodedCommentDefinition definition, bool duplicatePageCode)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var results = new List<RuleResult>();

        if (string.IsNullOrWhiteSpace(definition.Code))
        {
            results.Add(RuleResult.HardStop("CMT.CODE.REQUIRED", "Code is required."));
        }
        else if (definition.Code.Length > MaxCodeLength)
        {
            results.Add(RuleResult.HardStop("CMT.CODE.LENGTH", $"Code cannot exceed {MaxCodeLength} characters."));
        }

        if (string.IsNullOrWhiteSpace(definition.CommentText))
        {
            results.Add(RuleResult.HardStop("CMT.TEXT.REQUIRED", "Comment text is required."));
        }
        else if (definition.CommentText.Length > MaxCommentTextLength)
        {
            results.Add(RuleResult.HardStop("CMT.TEXT.LENGTH", $"Comment text cannot exceed {MaxCommentTextLength} characters."));
        }

        if (!Enum.IsDefined(definition.Page))
        {
            results.Add(RuleResult.HardStop("CMT.PAGE.INVALID", "Page must be Patient, Order, Specimen, or Test."));
        }

        if (duplicatePageCode)
        {
            results.Add(RuleResult.HardStop(
                "CMT.CODE.DUPLICATE",
                $"Code '{definition.Code}' already exists for the {definition.Page} page."));
        }

        return new RuleEvaluation(results);
    }
}
