using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Reference;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Tests;

public class ProductTypeChoiceTests
{
    [Fact]
    public void ChoiceLabel_KeepsTheCodeAndTheProductIdWhenTheNameIsBlank()
    {
        var named = ProductTypeDto.From(Product(4, " RBC-LR ", " Red Blood Cells ", " E0336 "));
        Assert.Equal("E0336 — Red Blood Cells", named.ChoiceLabel);
        Assert.Equal(" Red Blood Cells ", named.Name);
        Assert.Equal(" RBC-LR ", named.ProductCode);

        var blank = ProductTypeDto.From(Product(4, "RBC-LR", "   ", "E0336"));
        Assert.Equal("E0336 — Product 4", blank.ChoiceLabel);
        Assert.Equal("   ", blank.Name);
        Assert.Equal("RBC-LR", blank.ProductCode);
        Assert.DoesNotContain("E0336 — E0336", blank.ChoiceLabel);

        var facilityCode = ProductTypeDto.From(Product(4, " RBC-LR ", "   ", null));
        Assert.Equal("RBC-LR — Product 4", facilityCode.ChoiceLabel);
        Assert.DoesNotContain("RBC-LR — RBC-LR", facilityCode.ChoiceLabel);
    }

    [Fact]
    public void OrderChoice_KeepsTheCodeAndDoesNotUseItAsTheName()
    {
        var blank = ProductTypeDto.From(Product(4, "RBC-LR", "   ", "E0336"));
        Assert.Equal("E0336 — Product 4", blank.ChoiceLabel);
        Assert.Equal("   ", blank.Name);
        Assert.NotEqual(blank.DisplayProductCode, blank.ChoiceLabel);
        Assert.DoesNotContain("E0336 — E0336", blank.ChoiceLabel);

        var named = ProductTypeDto.From(Product(4, "RBC-LR", " Red Blood Cells ", null));
        Assert.Equal("RBC-LR — Red Blood Cells", named.ChoiceLabel);
        Assert.Equal(" Red Blood Cells ", named.Name);
    }

    [Fact]
    public void ChargeRuleChoice_KeepsTheProductCodeAndDoesNotUseTheIsbtCodeAsTheName()
    {
        var blank = Definition(4, "RBC-LR", "   ", "E0336");
        Assert.Equal("RBC-LR — Product 4", blank.ChoiceLabel);
        Assert.Equal("   ", blank.Name);
        Assert.Equal("RBC-LR", blank.ProductCode);
        Assert.Equal("E0336", blank.Isbt128ProductCode);
        Assert.DoesNotContain("E0336", blank.ChoiceLabel);

        var named = Definition(4, " RBC-LR ", " Red Blood Cells ", "E0336");
        Assert.Equal("RBC-LR — Red Blood Cells", named.ChoiceLabel);
        Assert.Equal(" Red Blood Cells ", named.Name);
    }

    [Fact]
    public void NameLabel_KeepsTheProductIdWhenTheNameIsBlank()
    {
        var named = Definition(4, "RBC-LR", " Red Blood Cells ", null);
        Assert.Equal("Red Blood Cells", named.NameLabel);
        Assert.Equal(" Red Blood Cells ", named.Name);
        Assert.Equal("RBC-LR", named.ProductCode);

        var blank = Definition(4, "RBC-LR", "   ", "E0336");
        Assert.Equal("Product 4", blank.NameLabel);
        Assert.Equal("   ", blank.Name);
        Assert.Equal("RBC-LR", blank.ProductCode);
        Assert.Equal("E0336", blank.Isbt128ProductCode);
        Assert.DoesNotContain("RBC-LR", blank.NameLabel);
        Assert.DoesNotContain("E0336", blank.NameLabel);
    }

    private static ProductType Product(long id, string code, string name, string? isbt) => new()
    {
        Id = id,
        ProductCode = code,
        Name = name,
        Isbt128ProductCode = isbt
    };

    private static ProductDefinitionDto Definition(long id, string code, string name, string? isbt) => new(
        id,
        code,
        name,
        ComponentClass.RedBloodCells,
        null,
        null,
        true,
        true,
        true,
        false,
        isbt,
        null,
        null,
        null,
        null,
        null,
        1,
        true,
        Array.Empty<ProductAttributeAssignmentDto>());
}
