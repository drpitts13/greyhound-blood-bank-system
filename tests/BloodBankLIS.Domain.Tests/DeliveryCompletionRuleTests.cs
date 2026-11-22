using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

/// <summary>End-user testing pass 83: completing a supplier delivery that does not match its packing list.</summary>
public class DeliveryCompletionRuleTests
{
    // TEST-BB-1096: a matching delivery completes without a note; one short or over names each product that differs and
    // needs a note; a note over the limit is refused.
    [Fact]
    public void Evaluate_NeedsANoteOnlyWhenTheDeliveryDiffers()
    {
        DeliveryCompletionRule.Line[] even = [new("RBC", 4, 4), new("Plasma", 2, 2)];
        DeliveryCompletionRule.Line[] uneven = [new("RBC", 4, 2), new("Plasma", 2, 3), new("Platelets", 1, 1)];

        Assert.Null(DeliveryCompletionRule.Differences(even));
        Assert.Equal(RuleSeverity.Pass, DeliveryCompletionRule.Evaluate(even, null).Severity);
        Assert.Equal("RBC 2 short; Plasma 1 over", DeliveryCompletionRule.Differences(uneven));

        var blocked = DeliveryCompletionRule.Evaluate(uneven, "  ");
        Assert.Equal((RuleSeverity.HardStop, DeliveryCompletionRule.Code), (blocked.Severity, blocked.Code));
        Assert.StartsWith("The delivery does not match its packing list (RBC 2 short; Plasma 1 over).", blocked.Message);
        Assert.Equal(RuleSeverity.Pass, DeliveryCompletionRule.Evaluate(uneven, "Supplier short 2 RBC, credit requested").Severity);
        Assert.Equal(RuleSeverity.HardStop, DeliveryCompletionRule.Evaluate(uneven, new string('x', 501)).Severity);
    }
}
