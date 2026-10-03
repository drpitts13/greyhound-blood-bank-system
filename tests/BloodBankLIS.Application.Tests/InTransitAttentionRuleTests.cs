using BloodBankLIS.Application.Issuing;

namespace BloodBankLIS.Application.Tests;

public class InTransitAttentionRuleTests
{
    [Fact]
    public void CountOverdue_IncludesOnlyFlagsAlreadyPastTheArrivalWindow()
    {
        var count = InTransitAttentionRule.CountOverdue([true, false, true]);

        Assert.Equal(2, count);
        Assert.Equal(0, InTransitAttentionRule.CountOverdue([false, false]));
    }
}
