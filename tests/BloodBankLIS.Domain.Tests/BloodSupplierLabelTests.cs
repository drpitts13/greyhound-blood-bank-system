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
}
