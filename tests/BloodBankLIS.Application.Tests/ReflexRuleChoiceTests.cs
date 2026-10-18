using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Tests;

public class ReflexRuleChoiceTests
{
    [Fact]
    public void CatalogList_KeepsTheCodeAndDoesNotUseItAsTheName()
    {
        var named = Catalog(9, "WEAKD", " Weak D reflex ");
        Assert.Equal("Weak D reflex", named.NameLabel);
        Assert.Equal(" Weak D reflex ", named.Name);
        Assert.Equal("WEAKD", named.Code);
        Assert.Equal("ABSC", named.TriggerTestCode);
        Assert.Equal("ABID", named.ReflexTestCode);

        var blank = Catalog(9, "WEAKD", "   ");
        Assert.Equal("Reflex 9", blank.NameLabel);
        Assert.Equal("   ", blank.Name);
        Assert.Equal("WEAKD", blank.Code);
        Assert.DoesNotContain("WEAKD", blank.NameLabel);
        Assert.DoesNotContain("ABSC", blank.NameLabel);
        Assert.DoesNotContain("ABID", blank.NameLabel);

        Assert.Equal("Reflex 9", ReflexRuleLabel.Name(9, null));
        Assert.Equal("Weak D reflex", ReflexRuleLabel.Name(9, " Weak D reflex "));
    }

    private static ReflexRuleDto Catalog(long id, string code, string name) => new(
        id,
        code,
        name,
        "ABSC",
        "Positive",
        "ABID",
        1,
        true,
        false,
        null,
        null,
        null);
}
