using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

// TEST-BB-675: the reserve form offers each open order once per product it asks for: two red cell lines are one
// choice (the order was listed twice), red cells and plasma are two; test lines, cancelled orders, and lines without a
// product are not offered.
public class ChartAllocatableOrdersTests
{
    private static OrderLineDto Line(long id, OrderCategory category, long? productTypeId, string name) =>
        new(id, (int)id, category, name, null, OrderType.Other, productTypeId, FulfillmentStatus.Ordered, null);

    private static PatientOrderDto Order(long id, string number, OrderStatus status, params OrderLineDto[] lines) => new(
        id, 7, 1, "VIS-1", number, OrderCategory.Product, "Blood", OrderPriority.Routine, status, 1, "OR", null, null, 3, "ACC-3",
        null, FulfillmentStatus.Ordered, OrderSource.Manual, new DateTime(2026, 11, 8, 8, 0, 0, DateTimeKind.Utc), null, lines);

    [Fact]
    public void EachOrderOncePerProduct()
    {
        var offered = ChartAllocatableOrders.From(
        [
            Order(1, "ORD-TWO-RBC", OrderStatus.New,
                Line(11, OrderCategory.Product, 5, "Red Blood Cells, Leukoreduced"),
                Line(12, OrderCategory.Product, 5, "Red Blood Cells, Leukoreduced")),
            Order(2, "ORD-MIXED", OrderStatus.InProcess,
                Line(21, OrderCategory.Product, 5, "Red Blood Cells, Leukoreduced"),
                Line(22, OrderCategory.Product, 6, "Fresh Frozen Plasma"),
                Line(23, OrderCategory.Test, null, "ABO/Rh")),
            Order(3, "ORD-CANCELLED", OrderStatus.Cancelled, Line(31, OrderCategory.Product, 5, "Red Blood Cells, Leukoreduced")),
            Order(4, "ORD-NO-PRODUCT", OrderStatus.New, Line(41, OrderCategory.Product, null, "Unknown product"))
        ], patientId: 7);

        Assert.Equal(
            new[] { ("ORD-TWO-RBC", 5L), ("ORD-MIXED", 5L), ("ORD-MIXED", 6L) },
            offered.Select(o => (o.OrderNumber, o.ProductTypeId)).ToArray());
        Assert.All(offered, o => Assert.Equal((7L, (long?)3, "ACC-3"), (o.PatientId, o.SpecimenId, o.AccessionNumber)));
        Assert.Equal("Fresh Frozen Plasma", offered.Single(o => o.ProductTypeId == 6).OrderName);
    }

    private static CompatibleUnitDto Unit(long id, long productTypeId, string code) =>
        new(id, $"W-{id}", productTypeId, code, code, true, AboGroup.O, RhType.Positive, "O+", new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc));

    // TEST-BB-676: with an order chosen the picker offers only compatible units of that order's product(s) and says how
    // many (or that there are none); with no order chosen it offers every compatible unit and says nothing.
    [Fact]
    public void UnitsFor_TheChosenOrdersProduct_AndSaysHowMany()
    {
        var orders = ChartAllocatableOrders.From(
        [
            Order(1, "ORD-RBC", OrderStatus.New, Line(11, OrderCategory.Product, 5, "Red Blood Cells, Leukoreduced"), Line(12, OrderCategory.Product, 5, "Red Blood Cells, Leukoreduced")),
            Order(2, "ORD-MIXED", OrderStatus.New, Line(21, OrderCategory.Product, 5, "Red Blood Cells, Leukoreduced"), Line(22, OrderCategory.Product, 6, "Fresh Frozen Plasma")),
            Order(3, "ORD-CRYO", OrderStatus.New, Line(31, OrderCategory.Product, 9, "Cryoprecipitate"))
        ], patientId: 7);
        IReadOnlyList<CompatibleUnitDto> compatible = [Unit(100, 7, "PLT-A"), Unit(101, 5, "RBC-LR"), Unit(102, 6, "FFP"), Unit(103, 5, "RBC-LR")];

        var red = ChartAllocatableOrders.UnitsFor(compatible, orders, 1);
        Assert.Equal(new long[] { 101, 103 }, red.Select(u => u.BloodUnitId).ToArray());
        Assert.Equal("2 compatible units of Red Blood Cells, Leukoreduced in stock.", ChartAllocatableOrders.OfferedLine(red, orders, 1));

        var mixed = ChartAllocatableOrders.UnitsFor(compatible, orders, 2);
        Assert.Equal(new long[] { 101, 102, 103 }, mixed.Select(u => u.BloodUnitId).ToArray());
        Assert.Equal("3 compatible units of Red Blood Cells, Leukoreduced or Fresh Frozen Plasma in stock.", ChartAllocatableOrders.OfferedLine(mixed, orders, 2));

        var none = ChartAllocatableOrders.UnitsFor(compatible, orders, 3);
        Assert.Empty(none);
        Assert.Equal("No compatible units of Cryoprecipitate in stock.", ChartAllocatableOrders.OfferedLine(none, orders, 3));

        Assert.Same(compatible, ChartAllocatableOrders.UnitsFor(compatible, orders, 0));
        Assert.Null(ChartAllocatableOrders.OfferedLine(compatible, orders, 0));
    }
}
