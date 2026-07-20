using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Tests;

public class OrderLineBuilderCrossmatchTests
{
    [Fact]
    public void WithCrossmatchLineIfNeeded_DoesNotDoubleAdd_WhenXmPresent()
    {
        var products = new Dictionary<long, ProductType>
        {
            [1] = new() { Id = 1, ProductCode = "RBC-LR", RequiresCrossmatch = true }
        };
        var lines = new List<OrderLineInputDto>
        {
            new(OrderCategory.Product, null, 1),
            new(OrderCategory.Test, "XM", null)
        };

        var result = OrderLineBuilder.WithCrossmatchLineIfNeeded(lines, products);

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result.Count(l => OrderLineBuilder.IsCrossmatchTestCode(l.TestCode)));
    }

    [Fact]
    public void WithCrossmatchLineIfNeeded_DoesNotDoubleAdd_WhenCxmPresent()
    {
        var products = new Dictionary<long, ProductType>
        {
            [1] = new() { Id = 1, ProductCode = "RBC-LR", RequiresCrossmatch = true }
        };
        var lines = new List<OrderLineInputDto>
        {
            new(OrderCategory.Product, null, 1),
            new(OrderCategory.Test, "CXM", null)
        };

        var result = OrderLineBuilder.WithCrossmatchLineIfNeeded(lines, products);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, l => string.Equals(l.TestCode, "XM", StringComparison.OrdinalIgnoreCase)
                                           && result.Count(x => OrderLineBuilder.IsCrossmatchTestCode(x.TestCode)) > 1);
    }

    [Fact]
    public void WithCrossmatchLineIfNeeded_AddsXm_WhenMissing()
    {
        var products = new Dictionary<long, ProductType>
        {
            [1] = new() { Id = 1, ProductCode = "RBC-LR", RequiresCrossmatch = true }
        };
        var lines = new List<OrderLineInputDto>
        {
            new(OrderCategory.Product, null, 1)
        };

        var result = OrderLineBuilder.WithCrossmatchLineIfNeeded(lines, products);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, l => string.Equals(l.TestCode, "XM", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MapTestOrderType_MapsCxmToCrossmatch() =>
        Assert.Equal(OrderType.Crossmatch, OrderLineBuilder.MapTestOrderType("CXM"));
}
