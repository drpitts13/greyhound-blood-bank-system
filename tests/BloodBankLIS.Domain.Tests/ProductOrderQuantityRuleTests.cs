using BloodBankLIS.Domain.Interfaces;

namespace BloodBankLIS.Domain.Tests;

public class ProductOrderQuantityRuleTests
{
    // TEST-BB-1138: a blood product order quantity is units (blank is one) or a volume in mL; fractions, unknown units
    // and amounts over the limits are refused (end-user testing pass 119).
    [Theory]
    [InlineData(null, null, 1, null, false)]
    [InlineData("2", null, 2, null, false)]
    [InlineData("3", "U", 3, null, false)]
    [InlineData("15", "mL", 1, 15, false)]
    [InlineData("1.5", "U", 0, null, true)]
    [InlineData("2", "g", 0, null, true)]
    [InlineData("21", "U", 0, null, true)]
    [InlineData("1001", "ML", 0, null, true)]
    [InlineData(null, "mL", 0, null, true)]
    public void Read(string? quantity, string? units, int expectedUnits, int? expectedMl, bool error)
    {
        var (u, ml, problem) = ProductOrderQuantityRule.Read(quantity, units);
        Assert.Equal(error, problem is not null);
        if (!error)
        {
            Assert.Equal((expectedUnits, expectedMl), (u, ml));
        }
    }
}
