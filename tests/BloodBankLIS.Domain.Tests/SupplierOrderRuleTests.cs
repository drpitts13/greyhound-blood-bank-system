using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-301: supplier order lifecycle rules.
public class SupplierOrderRuleTests
{
    [Theory]
    [InlineData(SupplierOrderStatus.Draft, RuleSeverity.Pass)]
    [InlineData(SupplierOrderStatus.Submitted, RuleSeverity.HardStop)]
    [InlineData(SupplierOrderStatus.Received, RuleSeverity.HardStop)]
    [InlineData(SupplierOrderStatus.Cancelled, RuleSeverity.HardStop)]
    public void OnlyADraftCanBeEdited(SupplierOrderStatus status, RuleSeverity expected)
    {
        Assert.Equal(expected, SupplierOrderRule.EvaluateEdit(status).Severity);
    }

    [Theory]
    [InlineData(0, RuleSeverity.Pass)]
    [InlineData(999, RuleSeverity.Pass)]
    [InlineData(-1, RuleSeverity.HardStop)]
    [InlineData(1000, RuleSeverity.HardStop)]
    public void QuantityIsBounded(int quantity, RuleSeverity expected)
    {
        Assert.Equal(expected, SupplierOrderRule.EvaluateQuantity(quantity).Severity);
    }

    [Fact]
    public void Submit_NeedsADraftWithUnitsAndAValidSignature()
    {
        Assert.Equal(SupplierOrderRule.NotDraftCode, SupplierOrderRule.EvaluateSubmit(SupplierOrderStatus.Submitted, 3, true).Code);
        Assert.Equal(SupplierOrderRule.EmptyCode, SupplierOrderRule.EvaluateSubmit(SupplierOrderStatus.Draft, 0, true).Code);
        var unsigned = SupplierOrderRule.EvaluateSubmit(SupplierOrderStatus.Draft, 3, false);
        Assert.Equal(RuleSeverity.HardStop, unsigned.Severity);
        Assert.Equal(SupplierOrderRule.SignatureCode, unsigned.Code);
        Assert.Equal(RuleSeverity.Pass, SupplierOrderRule.EvaluateSubmit(SupplierOrderStatus.Draft, 3, true).Severity);
    }

    [Fact]
    public void Receive_NeedsASubmittedOrderAndTheSameSupplier()
    {
        Assert.Equal(SupplierOrderRule.NotSubmittedCode, SupplierOrderRule.EvaluateReceive(SupplierOrderStatus.Draft, 1, 1).Code);
        Assert.Equal(RuleSeverity.HardStop, SupplierOrderRule.EvaluateReceive(SupplierOrderStatus.Submitted, 1, 2).Severity);
        Assert.Equal(RuleSeverity.Pass, SupplierOrderRule.EvaluateReceive(SupplierOrderStatus.Submitted, 1, 1).Severity);
    }

    [Fact]
    public void Cancel_NeedsAReason_AndNotAReceivedOrder()
    {
        Assert.Equal(RuleSeverity.HardStop, SupplierOrderRule.EvaluateCancel(SupplierOrderStatus.Draft, " ").Severity);
        Assert.Equal(RuleSeverity.HardStop, SupplierOrderRule.EvaluateCancel(SupplierOrderStatus.Received, "late").Severity);
        Assert.Equal(RuleSeverity.Pass, SupplierOrderRule.EvaluateCancel(SupplierOrderStatus.Submitted, "Supplier out of stock").Severity);
    }

    // TEST-BB-510: a submitted order is due its window after submit and overdue only after that; drafts, received and
    // cancelled orders, and an order never submitted, are never overdue.
    [Fact]
    public void Arrival_IsDueAfterTheWindow_AndOverdueOnlyWhileSubmitted()
    {
        var submitted = new DateTime(2026, 11, 6, 8, 0, 0, DateTimeKind.Utc);
        Assert.Equal(submitted.AddHours(24), SupplierOrderRule.ArrivalDueUtc(submitted, 24));
        Assert.Equal(submitted.AddHours(1), SupplierOrderRule.ArrivalDueUtc(submitted, 0));
        Assert.Null(SupplierOrderRule.ArrivalDueUtc(null, 24));

        Assert.False(SupplierOrderRule.IsArrivalOverdue(SupplierOrderStatus.Submitted, submitted, 24, submitted.AddHours(24)));
        Assert.True(SupplierOrderRule.IsArrivalOverdue(SupplierOrderStatus.Submitted, submitted, 24, submitted.AddHours(24).AddMinutes(1)));
        Assert.False(SupplierOrderRule.IsArrivalOverdue(SupplierOrderStatus.Received, submitted, 24, submitted.AddDays(3)));
        Assert.False(SupplierOrderRule.IsArrivalOverdue(SupplierOrderStatus.Cancelled, submitted, 24, submitted.AddDays(3)));
        Assert.False(SupplierOrderRule.IsArrivalOverdue(SupplierOrderStatus.Draft, null, 24, submitted.AddDays(3)));
    }
}
