using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ProductRetirementImpactRuleTests
{
    // TEST-BB-1403 (end-user testing loop 2 tick 23): each use of a retired product is named in plain words; nothing in
    // use gives no warning; units still in inventory include quarantined, reserved and crossmatched units.
    [Fact]
    public void Warnings_NameEachUse()
    {
        var w = ProductRetirementImpactRule.Warnings("RBC-LR", ["WASH-RBC-LR", "IRR-RBC-LR"], ["DIV-WB-RBC"], 3, 1);
        Assert.Equal(4, w.Count);
        Assert.Equal("RBC-LR is still the source of modification rules IRR-RBC-LR, WASH-RBC-LR. Update or deactivate them on Modification Rules.", w[0].Message);
        Assert.Equal("Modification rule DIV-WB-RBC still makes RBC-LR. Update or deactivate it on Modification Rules.", w[1].Message);
        Assert.Equal("3 units of RBC-LR are still in inventory; they keep this product.", w[2].Message);
        Assert.Contains("1 minimum stock level;", w[3].Message);
        Assert.Equal("1 unit of FFP is still in inventory; it keeps this product.", ProductRetirementImpactRule.Warnings("FFP", [], [], 1, 0).Single().Message);
        Assert.Empty(ProductRetirementImpactRule.Warnings("FFP", [], [], 0, 0));
        Assert.Contains(UnitStatus.Quarantine, ProductRetirementImpactRule.InInventory);
        Assert.Contains(UnitStatus.Crossmatched, ProductRetirementImpactRule.InInventory);
        Assert.DoesNotContain(UnitStatus.Transfused, ProductRetirementImpactRule.InInventory);
    }
}
