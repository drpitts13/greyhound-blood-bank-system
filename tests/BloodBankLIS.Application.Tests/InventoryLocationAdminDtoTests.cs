using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Tests;

public class InventoryLocationAdminDtoTests
{
    [Fact]
    public void NameLabel_KeepsTheLocationIdWhenTheNameIsBlank()
    {
        var named = Location(4, "FR1", " Issue fridge ");
        Assert.Equal("Issue fridge", named.NameLabel);
        Assert.Equal("FR1", named.Code);

        var blank = named with { Name = "   " };
        Assert.Equal("Location 4", blank.NameLabel);
        Assert.Equal("   ", blank.Name);
        Assert.DoesNotContain("FR1", blank.NameLabel);
    }

    private static InventoryLocationAdminDto Location(long id, string code, string name) => new(
        id, code, name, LocationType.Refrigerator, true, null,
        true, false, true, false, false,
        true, true, true, true, true,
        null, null, null, null, true);
}
