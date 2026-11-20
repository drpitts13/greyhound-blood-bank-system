using BloodBankLIS.Application.Reference;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class UnitProductTextTests
{
    // TEST-BB-1334 (end-user testing pass 307): the Modify page names the selected unit's product from its product type
    // ("FFP — Fresh Frozen Plasma"); an unknown product type gives nothing, shown as "—".
    [Fact]
    public void For_NamesTheProduct()
    {
        var types = new[] { new ProductTypeDto(3, "FFP", "Fresh Frozen Plasma", ComponentClass.Plasma, null, false, true) };
        Assert.Equal("FFP — Fresh Frozen Plasma", UnitProductText.For(types, 3));
        Assert.Null(UnitProductText.For(types, 99));
    }
}
