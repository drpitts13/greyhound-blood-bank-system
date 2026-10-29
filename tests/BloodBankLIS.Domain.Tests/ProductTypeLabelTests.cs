using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ProductTypeLabelTests
{
    [Fact]
    public void ForChoice_KeepsTheCodeAndTheProductIdWhenTheNameIsBlank()
    {
        Assert.Equal("E0336 — Red Blood Cells", ProductTypeLabel.ForChoice(4, " E0336 ", " Red Blood Cells "));
        Assert.Equal("E0336 — Product 4", ProductTypeLabel.ForChoice(4, " E0336 ", "   "));
        Assert.DoesNotContain("E0336 — E0336", ProductTypeLabel.ForChoice(4, "E0336", "   "));
        Assert.Equal("Product 4", ProductTypeLabel.ForChoice(4, "  ", null));
    }

    [Fact]
    public void Name_KeepsTheProductIdWhenTheNameIsBlank()
    {
        Assert.Equal("Red Blood Cells", ProductTypeLabel.Name(4, " Red Blood Cells "));
        Assert.Equal("Product 4", ProductTypeLabel.Name(4, "   "));
        Assert.Equal("Product 4", ProductTypeLabel.Name(4, null));
        Assert.DoesNotContain("E0336", ProductTypeLabel.Name(4, "   "));
    }
}
