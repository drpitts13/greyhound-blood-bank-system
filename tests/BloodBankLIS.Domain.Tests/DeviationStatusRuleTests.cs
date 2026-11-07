using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-489: a deviation closes only with a corrective action, and a recorded corrective action is never blanked.
public class DeviationStatusRuleTests
{
    [Theory]
    [InlineData(DeviationStatus.Closed, null, null, true)]
    [InlineData(DeviationStatus.Closed, null, "  ", true)]
    [InlineData(DeviationStatus.Closed, "Retrained staff", null, false)]
    [InlineData(DeviationStatus.Closed, null, "No action needed: one-off label printer jam", false)]
    [InlineData(DeviationStatus.UnderReview, null, null, false)]
    [InlineData(DeviationStatus.UnderReview, "Retrained staff", "", true)]
    [InlineData(DeviationStatus.CorrectiveAction, "Retrained staff", "Retrained staff; SOP revised", false)]
    public void ClosingNeedsAnAction_AndARecordedActionIsNotCleared(DeviationStatus to, string? recorded, string? entered, bool stopped)
    {
        var result = DeviationStatusRule.Evaluate(to, recorded, entered);

        Assert.Equal(stopped ? RuleSeverity.HardStop : RuleSeverity.Pass, result.Severity);
        Assert.Equal(DeviationStatusRule.Code, result.Code);
    }
}
