using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Tests;

public class BloodAttributeChoiceTests
{
    [Fact]
    public void CatalogList_KeepsTheCodeAndDoesNotUseItAsTheName()
    {
        var named = Catalog(4, "K", " K ", " Anti-K ");
        Assert.Equal("K", named.NameLabel);
        Assert.Equal("Anti-K", named.AntibodyNameLabel);
        Assert.Equal(" K ", named.Name);
        Assert.Equal(" Anti-K ", named.AntibodyName);
        Assert.Equal("K", named.Code);

        var blank = Catalog(4, "K", "   ", "   ");
        Assert.Equal("Attribute 4", blank.NameLabel);
        Assert.Equal("Antibody 4", blank.AntibodyNameLabel);
        Assert.Equal("   ", blank.Name);
        Assert.Equal("   ", blank.AntibodyName);
        Assert.Equal("K", blank.Code);
        Assert.DoesNotContain("K", blank.NameLabel);
        Assert.DoesNotContain("K", blank.AntibodyNameLabel);
        Assert.NotEqual(blank.NameLabel, blank.AntibodyNameLabel);

        var antigenOnly = Catalog(4, "K", "Kell", "   ");
        Assert.Equal("Kell", antigenOnly.NameLabel);
        Assert.Equal("Antibody 4", antigenOnly.AntibodyNameLabel);
        Assert.DoesNotContain("Kell", antigenOnly.AntibodyNameLabel);

        Assert.Equal("Attribute 4", BloodAttributeLabel.Name(4, null));
        Assert.Equal("Antibody 4", BloodAttributeLabel.Antibody(4, null));
        Assert.Equal("Kell", BloodAttributeLabel.Name(4, " Kell "));
        Assert.Equal("Anti-K", BloodAttributeLabel.Antibody(4, " Anti-K "));
    }

    private static BloodAttributeDefinitionDto Catalog(long id, string code, string name, string antibody) => new(
        id,
        code,
        name,
        antibody,
        true,
        10,
        1,
        true,
        false,
        null,
        null,
        null);
}
