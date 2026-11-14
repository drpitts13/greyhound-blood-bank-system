using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using V = BloodBankLIS.Domain.Rules.ReflexTriggerChangeRule.ResultVersion;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-567: a reflex test whose trigger result was later invalidated, or corrected so it no longer matches, tells
// the bench to check whether it is still needed; a trigger that still stands (or a correction to the same value) does not.
public class ReflexTriggerChangeRuleTests
{
    private static Dictionary<long, V> Chain(params V[] versions) => versions.ToDictionary(v => v.Id);

    [Fact]
    public void StandingTrigger_HasNoNotice()
    {
        Assert.Null(ReflexTriggerChangeRule.Notice(1, "ABSC", "Positive", "ABID", Chain(new V(1, null, ResultStatus.Verified, "Positive", null))));
        Assert.Null(ReflexTriggerChangeRule.Notice(9, "ABSC", "Positive", "ABID", Chain()));
        Assert.Null(ReflexTriggerChangeRule.Notice(1, "ABSC", "Positive", "ABID", Chain(
            new V(1, 2, ResultStatus.Verified, "Positive", null),
            new V(2, null, ResultStatus.Verified, "positive", null))));
    }

    [Fact]
    public void CorrectedTrigger_NamesTheNewValue()
    {
        Assert.Equal("ABSC is being corrected to Negative after this test was added. Check whether ABID is still needed.",
            ReflexTriggerChangeRule.Notice(1, "ABSC", "Positive", "ABID", Chain(
                new V(1, 2, ResultStatus.Verified, "Positive", null),
                new V(2, null, ResultStatus.Corrected, "Negative", null))));
        Assert.Equal("ABSC was corrected to Negative after this test was added. Check whether ABID is still needed.",
            ReflexTriggerChangeRule.Notice(1, "ABSC", "Positive", "ABID", Chain(
                new V(1, 2, ResultStatus.Verified, "Positive", null),
                new V(2, 3, ResultStatus.Corrected, "Negative", null),
                new V(3, null, ResultStatus.Verified, "Negative", null))));
    }

    [Fact]
    public void InvalidatedTrigger_SaysSo()
    {
        Assert.Equal("The ABSC result that added this test was invalidated. Check whether ABID is still needed.",
            ReflexTriggerChangeRule.Notice(1, "ABSC", "Positive", "ABID", Chain(
                new V(1, 2, ResultStatus.Verified, "Positive", null),
                new V(2, null, ResultStatus.Invalidated, "Positive", null))));
    }
}
