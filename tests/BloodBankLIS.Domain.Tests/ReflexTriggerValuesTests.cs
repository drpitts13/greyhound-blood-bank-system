using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

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

    // TEST-BB-1401 (end-user testing loop 2 tick 20): ABO/Rh interpretation keys are suggested as reflex matching reads
    // them ("O|Positive" as "O Positive"), so the suggestion fires and the correct value is not warned about.
    [Fact]
    public void AboRhKeys_AreSuggestedAsMatched()
    {
        var known = ReflexTriggerValues.Known(null, ["O|Positive", "A|Negative"]);
        Assert.Equal(["O Positive", "A Negative"], known);
        Assert.Null(ReflexTriggerValues.Warning("a negative", known));
        Assert.Equal("A Negative", ResultInterpretation.Resolve("A|Negative", null));
    }
}
