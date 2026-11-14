using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class LookbackRecallRuleTests
{
    // TEST-BB-912: a component that cannot become Recalled is named with why; one that can, or that the recall handles
    // otherwise (transfused, shipped, already recalled, discarded), is not.
    [Theory]
    [InlineData(UnitStatus.ReturnedToSupplier, "already returned to the supplier")]
    [InlineData(UnitStatus.Modified, "modified into other units")]
    [InlineData(UnitStatus.Expired, "expired")]
    [InlineData(UnitStatus.Available, null)]
    [InlineData(UnitStatus.Issued, null)]
    [InlineData(UnitStatus.Quarantine, null)]
    [InlineData(UnitStatus.Transfused, null)]
    [InlineData(UnitStatus.ShippedOut, null)]
    [InlineData(UnitStatus.Recalled, null)]
    [InlineData(UnitStatus.Discarded, null)]
    public void WhyNotRecalled_NamesWhatCannotBeRecalled(UnitStatus status, string? expected)
    {
        var why = LookbackRecallRule.WhyNotRecalled(status);
        if (expected is null)
        {
            Assert.Null(why);
        }
        else
        {
            Assert.Contains(expected, why);
        }
    }
}
