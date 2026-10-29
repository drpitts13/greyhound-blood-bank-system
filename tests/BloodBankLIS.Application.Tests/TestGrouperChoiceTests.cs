using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Tests;

public class TestGrouperChoiceTests
{
    [Fact]
    public void CatalogList_KeepsTheCodeAndDoesNotUseItAsTheName()
    {
        var named = Catalog(3, "TNS", " Type and Screen ");
        Assert.Equal("Type and Screen", named.NameLabel);
        Assert.Equal(" Type and Screen ", named.Name);
        Assert.Equal("TNS", named.Code);

        var blank = Catalog(3, "TNS", "   ");
        Assert.Equal("Grouper 3", blank.NameLabel);
        Assert.Equal("   ", blank.Name);
        Assert.Equal("TNS", blank.Code);
        Assert.NotEqual(blank.Code, blank.NameLabel);
        Assert.DoesNotContain("TNS", blank.NameLabel);

        Assert.Equal("Grouper 3", TestGrouperLabel.Name(3, null));
        Assert.Equal("Type and Screen", TestGrouperLabel.Name(3, " Type and Screen "));
    }

    private static TestGrouperDto Catalog(long id, string code, string name) => new(
        id,
        code,
        name,
        Array.Empty<TestGrouperMemberDto>(),
        1,
        true,
        false,
        null,
        null,
        null);
}
