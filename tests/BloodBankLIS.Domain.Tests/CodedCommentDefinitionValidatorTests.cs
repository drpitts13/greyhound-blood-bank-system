using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class CodedCommentDefinitionValidatorTests
{
    [Fact]
    public void Validate_RequiresCodeAndText()
    {
        var evaluation = CodedCommentDefinitionValidator.Validate(
            new CodedCommentDefinition { Page = CommentPage.Specimen },
            duplicatePageCode: false);

        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == "CMT.CODE.REQUIRED");
        Assert.Contains(evaluation.HardStops, r => r.Code == "CMT.TEXT.REQUIRED");
    }

    [Fact]
    public void Validate_RejectsDuplicatePageCode()
    {
        var evaluation = CodedCommentDefinitionValidator.Validate(
            new CodedCommentDefinition
            {
                Code = "HEM",
                CommentText = "Specimen hemolyzed.",
                Page = CommentPage.Specimen
            },
            duplicatePageCode: true);

        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == "CMT.CODE.DUPLICATE");
    }

    [Fact]
    public void Validate_AcceptsValidDefinition()
    {
        var evaluation = CodedCommentDefinitionValidator.Validate(
            new CodedCommentDefinition
            {
                Code = "HEM",
                CommentText = "Specimen hemolyzed.",
                Page = CommentPage.Specimen
            },
            duplicatePageCode: false);

        Assert.False(evaluation.IsHardStopped);
    }
}
