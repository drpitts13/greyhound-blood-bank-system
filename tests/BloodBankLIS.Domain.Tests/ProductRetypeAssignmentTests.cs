using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ProductRetypeAssignmentTests
{
    [Fact]
    public void Resolve_PicksRhSpecificTest()
    {
        var product = new ProductType
        {
            RequiresRetype = true,
            RhPositiveRetypeTestId = 11,
            RhNegativeRetypeTestId = 22
        };

        Assert.Equal(11, ProductRetypeAssignment.ResolveTestId(product, RhType.Positive));
        Assert.Equal(22, ProductRetypeAssignment.ResolveTestId(product, RhType.Negative));
        Assert.Null(ProductRetypeAssignment.ResolveTestId(product, RhType.Unknown));
    }

    [Fact]
    public void Evaluate_UnknownRh_HardStops()
    {
        var product = new ProductType
        {
            ProductCode = "RBC-LR",
            RequiresRetype = true,
            RhPositiveRetypeTestId = 1,
            RhNegativeRetypeTestId = 2
        };

        var result = ProductRetypeAssignment.Evaluate(product, RhType.Unknown);

        Assert.NotNull(result);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ProductRetypeAssignment.MissingRhCode, result.Code);
    }

    [Fact]
    public void Evaluate_MissingAssignment_HardStops()
    {
        var product = new ProductType
        {
            ProductCode = "RBC-LR",
            RequiresRetype = true,
            RhPositiveRetypeTestId = 1
        };

        var result = ProductRetypeAssignment.Evaluate(product, RhType.Negative);

        Assert.NotNull(result);
        Assert.Equal(ProductRetypeAssignment.MissingTestCode, result.Code);
    }

    [Fact]
    public void Evaluate_NonRetypeProduct_Passes()
    {
        var product = new ProductType { RequiresRetype = false };

        Assert.Null(ProductRetypeAssignment.Evaluate(product, RhType.Unknown));
    }
}
