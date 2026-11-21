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
    // TEST-BB-1238 (end-user testing pass 216): a component still on a shelf and not already held is named on the DIN
    // lookback; a quarantined, recalled, damaged, transfused, issued or discarded one is not.
    [Theory]
    [InlineData(UnitStatus.Available, true)]
    [InlineData(UnitStatus.Received, true)]
    [InlineData(UnitStatus.Crossmatched, true)]
    [InlineData(UnitStatus.Quarantine, false)]
    [InlineData(UnitStatus.Recalled, false)]
    [InlineData(UnitStatus.Damaged, false)]
    [InlineData(UnitStatus.Issued, false)]
    [InlineData(UnitStatus.Transfused, false)]
    [InlineData(UnitStatus.Discarded, false)]
    public void StillInStock_NamesUnheldComponentsOnAShelf(UnitStatus status, bool expected) =>
        Assert.Equal(expected, LookbackRecallRule.StillInStock(status));
}
