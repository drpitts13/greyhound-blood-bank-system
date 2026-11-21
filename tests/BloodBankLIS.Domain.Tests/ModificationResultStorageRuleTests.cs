using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ModificationResultStorageRuleTests
{
    // TEST-BB-1400 (end-user testing loop 2 tick 19): thawed and deglycerolized results cannot be stored in a freezer; a
    // frozen result must be; other types may go anywhere the location takes the product.
    [Fact]
    public void Problem_FollowsWhetherTheResultIsFrozen()
    {
        Assert.Contains("deglycerolized unit cannot go to FRZ-1, a freezer",
            ModificationResultStorageRule.Problem(ModificationType.Deglycerolize, LocationType.Freezer, "FRZ-1", chosen: true));
        Assert.Contains("thawed unit cannot stay in FRZ-1",
            ModificationResultStorageRule.Problem(ModificationType.Thaw, LocationType.Freezer, "FRZ-1", chosen: false));
        Assert.Null(ModificationResultStorageRule.Problem(ModificationType.Thaw, LocationType.Refrigerator, "FRIDGE-1", chosen: true));
        Assert.Contains("frozen unit must go to a freezer; FRIDGE-1 is not one",
            ModificationResultStorageRule.Problem(ModificationType.Freeze, LocationType.Refrigerator, "FRIDGE-1", chosen: true));
        Assert.Contains("Choose the result location (a freezer)",
            ModificationResultStorageRule.Problem(ModificationType.Freeze, LocationType.Refrigerator, "FRIDGE-1", chosen: false));
        Assert.Null(ModificationResultStorageRule.Problem(ModificationType.Freeze, LocationType.Freezer, "FRZ-1", chosen: true));
        Assert.Null(ModificationResultStorageRule.Problem(ModificationType.Irradiate, LocationType.Freezer, "FRZ-1", chosen: true));
    }
}
