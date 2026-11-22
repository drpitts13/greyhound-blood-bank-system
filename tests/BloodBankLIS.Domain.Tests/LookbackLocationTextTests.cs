using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-1297 (end-user testing pass 271): a lookback component in storage gives its location; a transfused, issued or
// discarded one says it is not in storage rather than naming its last refrigerator.
public class LookbackLocationTextTests
{
    [Fact]
    public void Location_OnlyForUnitsInStorage()
    {
        Assert.Equal("FRIDGE-1 Main Blood Bank Refrigerator", LookbackRecallRule.LocationText(UnitStatus.Received, "FRIDGE-1 Main Blood Bank Refrigerator"));
        Assert.Equal("—", LookbackRecallRule.LocationText(UnitStatus.Available, null));
        Assert.Equal("Not in storage", LookbackRecallRule.LocationText(UnitStatus.Transfused, "FRIDGE-1 Main Blood Bank Refrigerator"));
        Assert.Equal("Not in storage", LookbackRecallRule.LocationText(UnitStatus.Issued, "FRIDGE-1 Main Blood Bank Refrigerator"));
        Assert.Equal("Not in storage", LookbackRecallRule.LocationText(UnitStatus.Discarded, "FRIDGE-1 Main Blood Bank Refrigerator"));
    }
}
