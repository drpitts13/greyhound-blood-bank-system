using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ReflexTriggerValuesTests
{
    // TEST-BB-1393 (end-user testing loop 2 tick 11): the trigger test's allowed values and interpretation keys are
    // suggested; a value it never produces is warned about (any case matches); a test with no listed values warns nothing.
    [Fact]
    public void Known_AndWarning()
    {
        var known = ReflexTriggerValues.Known("Negative\nPositive", ["Positive", "Indeterminate"]);
        Assert.Equal(["Negative", "Positive", "Indeterminate"], known);
        Assert.Null(ReflexTriggerValues.Warning("positive", known));
        Assert.Null(ReflexTriggerValues.Warning("", known));
        Assert.Contains("never fire", ReflexTriggerValues.Warning("Postive", known));
        Assert.Contains("Negative, Positive, Indeterminate", ReflexTriggerValues.Warning("Postive", known));
        Assert.Null(ReflexTriggerValues.Warning("anything", ReflexTriggerValues.Known(null, null)));
    }
}
