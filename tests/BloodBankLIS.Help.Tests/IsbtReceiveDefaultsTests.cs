using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>ISBT receive's manual entry prefills no codes (end-user testing loop 2).</summary>
public class IsbtReceiveDefaultsTests : BunitContext
{
    // TEST-BB-1383: the ABO/RhD and product description codes start empty, with examples only as placeholders, so a unit
    // is never received with a sample code (DEMO read as O positive).
    [Fact]
    public void ManualEntry_PrefillsNoCodes()
    {
        CannedApi.Register(this, new Dictionary<string, object>());
        var page = Render<IsbtReceive>();
        var abo = page.Find("[data-isbt-abo-code]");
        Assert.Equal(string.Empty, abo.GetAttribute("value") ?? string.Empty);
        Assert.Equal("e.g. 5100", abo.GetAttribute("placeholder"));
        Assert.Equal(string.Empty, page.Find("[data-isbt-pdc]").GetAttribute("value") ?? string.Empty);
        Assert.Equal(string.Empty, page.Find("#isbt-din").GetAttribute("value") ?? string.Empty);
    }
}
