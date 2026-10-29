using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class BloodSupplierLabelTests
{
    [Fact]
    public void ForChoice_KeepsTheCodeAndTheSupplierIdWhenTheNameIsBlank()
    {
        Assert.Equal("ARC — American Red Cross", BloodSupplierLabel.ForChoice(9, " ARC ", " American Red Cross "));
        Assert.Equal("ARC — Supplier 9", BloodSupplierLabel.ForChoice(9, " ARC ", "   "));
        Assert.DoesNotContain("ARC — ARC", BloodSupplierLabel.ForChoice(9, "ARC", "   "));
        Assert.Equal("Supplier 9", BloodSupplierLabel.ForChoice(9, "  ", null));
    }

    [Fact]
    public void Name_KeepsTheSupplierIdWhenTheNameIsBlank()
    {
        Assert.Equal("American Red Cross", BloodSupplierLabel.Name(9, " American Red Cross "));
        Assert.Equal("Supplier 9", BloodSupplierLabel.Name(9, "   "));
        Assert.Equal("Supplier 9", BloodSupplierLabel.Name(9, null));
        Assert.DoesNotContain("ARC", BloodSupplierLabel.Name(9, "   "));
    }
}
