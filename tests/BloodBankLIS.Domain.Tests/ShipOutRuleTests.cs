using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-596: only an unreserved, unexpired unit in stock ships, and only to a named facility; a shipped unit is final.
public class ShipOutRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Evaluate_NeedsAFacility_AndAnUnreservedUnexpiredUnitInStock()
    {
        Assert.Equal(RuleSeverity.Pass, ShipOutRule.Evaluate(UnitStatus.Available, Now.AddDays(1), Now, "St. Mary's", null).Severity);
        Assert.Equal(RuleSeverity.Pass, ShipOutRule.Evaluate(UnitStatus.Received, Now.AddDays(1), Now, "St. Mary's", "PL-1").Severity);
        Assert.Contains("Name the facility", ShipOutRule.Evaluate(UnitStatus.Available, Now.AddDays(1), Now, " ", null).Message);
        Assert.Contains("at most 200", ShipOutRule.Evaluate(UnitStatus.Available, Now.AddDays(1), Now, new string('x', 201), null).Message);
        Assert.Contains("at most 100", ShipOutRule.Evaluate(UnitStatus.Available, Now.AddDays(1), Now, "St. Mary's", new string('x', 101)).Message);
        Assert.Contains("already shipped", ShipOutRule.Evaluate(UnitStatus.ShippedOut, Now.AddDays(1), Now, "St. Mary's", null).Message);
        Assert.Contains("is Crossmatched", ShipOutRule.Evaluate(UnitStatus.Crossmatched, Now.AddDays(1), Now, "St. Mary's", null).Message);
        Assert.Contains("is Quarantine", ShipOutRule.Evaluate(UnitStatus.Quarantine, Now.AddDays(1), Now, "St. Mary's", null).Message);
        Assert.Contains("expired", ShipOutRule.Evaluate(UnitStatus.Available, Now, Now, "St. Mary's", null).Message);

        Assert.True(InventoryStatusTransition.IsAllowed(UnitStatus.Available, UnitStatus.ShippedOut));
        Assert.False(InventoryStatusTransition.IsAllowed(UnitStatus.ShippedOut, UnitStatus.Available));
    }
}
