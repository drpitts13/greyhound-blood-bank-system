using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Reference;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Tests;

public class TestDefinitionChoiceTests
{
    [Fact]
    public void OrderChoice_KeepsTheCodeAndDoesNotUseItAsTheName()
    {
        var named = TestDefinitionListItemDto.From(Test(12, "ABORH", " ABO/Rh "));
        Assert.Equal("ABO/Rh", named.NameLabel);
        Assert.Equal(" ABO/Rh ", named.Name);
        Assert.Equal("ABORH", named.Code);

        var blank = TestDefinitionListItemDto.From(Test(12, "ABORH", "   "));
        Assert.Equal("Test 12", blank.NameLabel);
        Assert.Equal("   ", blank.Name);
        Assert.Equal("ABORH", blank.Code);
        Assert.NotEqual(blank.Code, blank.NameLabel);

        Assert.Equal("Test 12", TestDefinitionLabel.Name(12, null));
        Assert.Equal("ABO/Rh", TestDefinitionLabel.Name(12, " ABO/Rh "));
    }

    [Fact]
    public void CatalogList_KeepsTheCodeAndDoesNotUseItAsTheName()
    {
        var named = Catalog(12, "ABORH", " ABO/Rh ");
        Assert.Equal("ABO/Rh", named.NameLabel);
        Assert.Equal(" ABO/Rh ", named.Name);
        Assert.Equal("ABORH", named.Code);

        var blank = Catalog(12, "ABORH", "   ");
        Assert.Equal("Test 12", blank.NameLabel);
        Assert.Equal("   ", blank.Name);
        Assert.Equal("ABORH", blank.Code);
        Assert.NotEqual(blank.Code, blank.NameLabel);
        Assert.DoesNotContain("ABORH", blank.NameLabel);
    }

    private static TestDefinitionDto Catalog(long id, string code, string name) => new(
        id,
        code,
        name,
        TestCategory.Other,
        ResultValueType.Coded,
        null,
        Array.Empty<PanelSubtestAssignmentDto>(),
        Array.Empty<InterpretationLogicRowDto>(),
        null,
        null,
        null,
        0,
        false,
        null,
        true,
        false,
        false,
        false,
        Array.Empty<string>(),
        null,
        false,
        1,
        true,
        false,
        null,
        null,
        null);

    private static TestDefinition Test(long id, string code, string name) => new()
    {
        Id = id,
        Code = code,
        Name = name,
        Category = TestCategory.Other,
        IsActive = true
    };
}
