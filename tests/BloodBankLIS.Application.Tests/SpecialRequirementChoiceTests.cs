using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Tests;

public class SpecialRequirementChoiceTests
{
    [Fact]
    public void CatalogList_KeepsTheCodeAndDoesNotUseItAsTheName()
    {
        var named = Catalog(6, "IRRAD", " Irradiated ", "IRRAD");
        Assert.Equal("Irradiated", named.NameLabel);
        Assert.Equal(" Irradiated ", named.Name);
        Assert.Equal("IRRAD", named.Code);
        Assert.Equal("IRRAD", named.ProductAttributeCode);

        var blank = Catalog(6, "IRRAD", "   ", "CMVNEG");
        Assert.Equal("Requirement 6", blank.NameLabel);
        Assert.Equal("   ", blank.Name);
        Assert.Equal("IRRAD", blank.Code);
        Assert.Equal("CMVNEG", blank.ProductAttributeCode);
        Assert.DoesNotContain("IRRAD", blank.NameLabel);
        Assert.DoesNotContain("CMVNEG", blank.NameLabel);

        Assert.Equal("Requirement 6", SpecialRequirementLabel.Name(6, null));
        Assert.Equal("Irradiated", SpecialRequirementLabel.Name(6, " Irradiated "));
    }

    private static SpecialRequirementDefinitionDto Catalog(long id, string code, string name, string? attribute) => new(
        id,
        code,
        name,
        SpecialRequirementLevel.Unit,
        SpecialRequirementEnforcementKind.RequireProductAttribute,
        attribute,
        null,
        10,
        1,
        true,
        false,
        null,
        null,
        null);
}
