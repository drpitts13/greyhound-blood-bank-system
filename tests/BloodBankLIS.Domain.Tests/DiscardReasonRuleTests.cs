using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-297: a discard needs an active catalog code, and a comment when the code says so.
public class DiscardReasonRuleTests
{
    private static readonly UnitDiscardReason Bag = new() { Code = "BAG", Text = "Bag damaged", IsActive = true };
    private static readonly UnitDiscardReason Other = new() { Code = "OTHER", Text = "Other", RequiresComment = true, IsActive = true };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankCode_IsRequired(string? code)
    {
        var result = DiscardReasonRule.Evaluate(code, null, "a comment");
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(DiscardReasonRule.RequiredCode, result.Code);
    }

    [Fact]
    public void UnknownOrInactiveCode_IsRejected()
    {
        Assert.Equal(DiscardReasonRule.UnknownCode, DiscardReasonRule.Evaluate("NOPE", null, null).Code);
        var retired = new UnitDiscardReason { Code = "OLD", Text = "Old", IsActive = false };
        Assert.Equal(DiscardReasonRule.UnknownCode, DiscardReasonRule.Evaluate("OLD", retired, null).Code);
    }

    [Fact]
    public void CommentRequiredCode_NeedsAComment()
    {
        Assert.Equal(DiscardReasonRule.CommentRequiredCode, DiscardReasonRule.Evaluate("OTHER", Other, "  ").Code);
        Assert.Equal(RuleSeverity.Pass, DiscardReasonRule.Evaluate("OTHER", Other, "Dropped on floor").Severity);
    }

    [Fact]
    public void ActiveCode_PassesWithOrWithoutComment()
    {
        Assert.Equal(RuleSeverity.Pass, DiscardReasonRule.Evaluate("BAG", Bag, null).Severity);
        Assert.Equal(RuleSeverity.Pass, DiscardReasonRule.Evaluate("BAG", Bag, "Leak at port").Severity);
    }

    [Fact]
    public void StoredText_AppendsTheComment()
    {
        Assert.Equal("Bag damaged", DiscardReasonRule.StoredText(Bag, null));
        Assert.Equal("Bag damaged", DiscardReasonRule.StoredText(Bag, "   "));
        Assert.Equal("Bag damaged: Leak at port", DiscardReasonRule.StoredText(Bag, " Leak at port "));
    }
}
