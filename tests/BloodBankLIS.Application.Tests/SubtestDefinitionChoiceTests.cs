using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Tests;

public class SubtestDefinitionChoiceTests
{
    [Fact]
    public void CatalogList_KeepsTheCodeAndDoesNotUseItAsTheName()
    {
        var named = Catalog(8, "ANTI-A", " Anti-A ");
        Assert.Equal("Anti-A", named.NameLabel);
        Assert.Equal(" Anti-A ", named.Name);
        Assert.Equal("ANTI-A", named.Code);

        var blank = Catalog(8, "ANTI-A", "   ");
        Assert.Equal("Subtest 8", blank.NameLabel);
        Assert.Equal("   ", blank.Name);
        Assert.Equal("ANTI-A", blank.Code);
        Assert.NotEqual(blank.Code, blank.NameLabel);
        Assert.DoesNotContain("ANTI-A", blank.NameLabel);

        Assert.Equal("Subtest 8", SubtestDefinitionLabel.Name(8, null));
        Assert.Equal("Anti-A", SubtestDefinitionLabel.Name(8, " Anti-A "));
    }

    private static SubtestDefinitionDto Catalog(long id, string code, string name) => new(
        id,
        code,
        name,
        SubtestResultType.GradedReaction,
        Array.Empty<SubtestChoiceDto>(),
        1,
        true,
        false,
        null,
        null,
        null);
}
