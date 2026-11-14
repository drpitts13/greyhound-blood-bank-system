using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-334
public class SpecimenRejectionReasonRuleTests
{
    private static SpecimenRejectionReason Reason(string code, bool comment = false, bool active = true) =>
        new() { Code = code, Text = code + " text", RequiresComment = comment, IsActive = active };

    [Fact]
    public void BlankCode_IsRefused() =>
        Assert.Equal(SpecimenRejectionReasonRule.RequiredCode, SpecimenRejectionReasonRule.Evaluate(" ", null, null).Code);

    [Fact]
    public void UnknownCode_IsRefused() =>
        Assert.Equal(SpecimenRejectionReasonRule.UnknownCode, SpecimenRejectionReasonRule.Evaluate("NOPE", null, null).Code);

    [Fact]
    public void InactiveCode_IsRefused() =>
        Assert.Equal(SpecimenRejectionReasonRule.UnknownCode, SpecimenRejectionReasonRule.Evaluate("OLD", Reason("OLD", active: false), null).Code);

    [Fact]
    public void CommentRequired_WithoutComment_IsRefused() =>
        Assert.Equal(SpecimenRejectionReasonRule.CommentRequiredCode, SpecimenRejectionReasonRule.Evaluate("OTHER", Reason("OTHER", comment: true), "  ").Code);

    [Fact]
    public void ActiveCode_Passes() =>
        Assert.Equal(RuleSeverity.Pass, SpecimenRejectionReasonRule.Evaluate("CLOTTED", Reason("CLOTTED"), null).Severity);

    [Fact]
    public void StoredText_AppendsCommentWhenPresent()
    {
        Assert.Equal("CLOTTED text", SpecimenRejectionReasonRule.StoredText(Reason("CLOTTED"), null));
        Assert.Equal("OTHER text: tube cracked", SpecimenRejectionReasonRule.StoredText(Reason("OTHER"), " tube cracked "));
    }
}
