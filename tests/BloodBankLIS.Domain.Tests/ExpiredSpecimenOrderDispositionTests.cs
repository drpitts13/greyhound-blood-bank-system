using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ExpiredSpecimenOrderDispositionTests
{
    private static readonly DateTime Now = new(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc);
    private static readonly IReadOnlySet<string> CatalogCodes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SER-XM" };

    [Fact]
    public void AtExpiration_IsExpired()
    {
        Assert.True(ExpiredSpecimenOrderDisposition.SpecimenIsExpired(Now, Now));
    }

    [Fact]
    public void BeforeExpiration_IsNotExpired()
    {
        Assert.False(ExpiredSpecimenOrderDisposition.SpecimenIsExpired(Now.AddMinutes(1), Now));
    }

    [Fact]
    public void MissingExpiration_IsNotExpired()
    {
        Assert.False(ExpiredSpecimenOrderDisposition.SpecimenIsExpired(null, Now));
    }

    [Theory]
    [InlineData(OrderStatus.New, true)]
    [InlineData(OrderStatus.InProcess, true)]
    [InlineData(OrderStatus.OnHold, true)]
    [InlineData(OrderStatus.PartiallyComplete, true)]
    [InlineData(OrderStatus.Completed, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Discontinued, false)]
    public void Open_ExcludesClosedStatuses(OrderStatus status, bool open)
    {
        Assert.Equal(open, ExpiredSpecimenOrderDisposition.IsOpen(status));
    }

    [Fact]
    public void CrossmatchLine_IsPendingCrossmatchOrder()
    {
        var pending = ExpiredSpecimenOrderDisposition.IsPendingCrossmatchOrder(
            OrderStatus.InProcess,
            OrderType.Crossmatch,
            [Line(OrderCategory.Test, OrderType.Crossmatch, "XM")],
            CatalogCodes,
            _ => false);

        Assert.True(pending);
    }

    [Fact]
    public void CatalogCrossmatchCode_IsPendingCrossmatchOrder()
    {
        var pending = ExpiredSpecimenOrderDisposition.IsPendingCrossmatchOrder(
            OrderStatus.New,
            OrderType.Other,
            [Line(OrderCategory.Test, OrderType.Other, "ser-xm")],
            CatalogCodes,
            _ => false);

        Assert.True(pending);
    }

    [Fact]
    public void ProductThatRequiresCrossmatch_IsPendingCrossmatchOrder()
    {
        var pending = ExpiredSpecimenOrderDisposition.IsPendingCrossmatchOrder(
            OrderStatus.New,
            OrderType.Other,
            [Line(OrderCategory.Product, OrderType.Other, null, productTypeId: 4)],
            CatalogCodes,
            id => id == 4);

        Assert.True(pending);
    }

    [Fact]
    public void TypeAndScreen_IsNotCrossmatchOrder()
    {
        var pending = ExpiredSpecimenOrderDisposition.IsPendingCrossmatchOrder(
            OrderStatus.New,
            OrderType.TypeAndScreen,
            [Line(OrderCategory.Test, OrderType.TypeAndScreen, "ABSC")],
            CatalogCodes,
            _ => false);

        Assert.False(pending);
    }

    [Fact]
    public void InactiveCrossmatchLine_DoesNotKeepTheOrderAsCrossmatch()
    {
        var pending = ExpiredSpecimenOrderDisposition.IsPendingCrossmatchOrder(
            OrderStatus.InProcess,
            OrderType.Crossmatch,
            [
                Line(OrderCategory.Test, OrderType.Crossmatch, "XM", isActive: false),
                Line(OrderCategory.Test, OrderType.TypeAndScreen, "ABSC")
            ],
            CatalogCodes,
            _ => false);

        Assert.False(pending);
    }

    [Fact]
    public void HeaderCrossmatch_WithNoActiveLines_StaysCrossmatch()
    {
        var pending = ExpiredSpecimenOrderDisposition.IsPendingCrossmatchOrder(
            OrderStatus.New,
            OrderType.Crossmatch,
            [],
            CatalogCodes,
            _ => false);

        Assert.True(pending);
    }

    [Fact]
    public void ClosedCrossmatch_IsNotPending()
    {
        var pending = ExpiredSpecimenOrderDisposition.IsPendingCrossmatchOrder(
            OrderStatus.Completed,
            OrderType.Crossmatch,
            [Line(OrderCategory.Test, OrderType.Crossmatch, "XM")],
            CatalogCodes,
            _ => false);

        Assert.False(pending);
    }

    [Fact]
    public void ExpiredCrossmatch_Unlinks()
    {
        var action = ExpiredSpecimenOrderDisposition.Decide(
            specimenExpired: true, orderOpen: true, pendingCrossmatchOrder: true);

        Assert.Equal(ExpiredSpecimenOrderAction.Unlink, action);
    }

    [Fact]
    public void ExpiredOtherOrder_Cancels()
    {
        var action = ExpiredSpecimenOrderDisposition.Decide(
            specimenExpired: true, orderOpen: true, pendingCrossmatchOrder: false);

        Assert.Equal(ExpiredSpecimenOrderAction.Cancel, action);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, false, false)]
    public void CurrentSpecimen_OrClosedOrder_DoesNothing(bool expired, bool open, bool crossmatch)
    {
        var action = ExpiredSpecimenOrderDisposition.Decide(expired, open, crossmatch);
        Assert.Equal(ExpiredSpecimenOrderAction.None, action);
    }

    private static OrderLineDispositionFacts Line(
        OrderCategory category,
        OrderType orderType,
        string? testCode,
        long? productTypeId = null,
        bool isActive = true) =>
        new(category, orderType, testCode, productTypeId, isActive);
}
