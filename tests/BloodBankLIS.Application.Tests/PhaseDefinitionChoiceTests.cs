using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Tests;

public class PhaseDefinitionChoiceTests
{
    [Fact]
    public void CatalogList_KeepsTheCodeAndDoesNotUseItAsTheName()
    {
        var named = Catalog(5, "AHG", " AHG ");
        Assert.Equal("AHG", named.NameLabel);
        Assert.Equal(" AHG ", named.Name);
        Assert.Equal("AHG", named.Code);

        var blank = Catalog(5, "AHG", "   ");
        Assert.Equal("Phase 5", blank.NameLabel);
        Assert.Equal("   ", blank.Name);
        Assert.Equal("AHG", blank.Code);
        Assert.NotEqual(blank.Code, blank.NameLabel);
        Assert.DoesNotContain("AHG", blank.NameLabel);

        Assert.Equal("Phase 5", PhaseDefinitionLabel.Name(5, null));
        Assert.Equal("AHG", PhaseDefinitionLabel.Name(5, " AHG "));
    }

    private static PhaseDefinitionDto Catalog(long id, string code, string name) => new(
        id,
        code,
        name,
        1,
        true,
        false,
        null,
        1,
        true,
        false,
        null,
        null,
        null);
}
