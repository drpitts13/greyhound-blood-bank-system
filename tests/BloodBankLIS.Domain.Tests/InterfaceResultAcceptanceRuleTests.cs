using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class InterfaceResultAcceptanceRuleTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("F")]
    [InlineData("C")]
    [InlineData("P")]
    [InlineData("R")]
    [InlineData("f")]
    public void AcceptedStatuses_Pass(string? status)
    {
        Assert.Equal(RuleSeverity.Pass, InterfaceResultAcceptanceRule.Evaluate(status).Severity);
    }

    [Theory]
    [InlineData("X")]
    [InlineData("D")]
    [InlineData("I")]
    [InlineData("W")]
    public void DestructiveStatuses_HardStop(string status)
    {
        var result = InterfaceResultAcceptanceRule.Evaluate(status);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(InterfaceResultAcceptanceRule.Code, result.Code);
    }

    [Theory]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Discontinued)]
    public void ClosedOrder_HardStop(OrderStatus status)
    {
        var result = InterfaceResultAcceptanceRule.EvaluateOrder(status);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(InterfaceResultAcceptanceRule.ClosedOrderCode, result.Code);
    }

    [Theory]
    [InlineData(OrderStatus.New)]
    [InlineData(OrderStatus.InProcess)]
    [InlineData(OrderStatus.OnHold)]
    [InlineData(OrderStatus.Completed)]
    public void OpenOrder_Passes(OrderStatus status)
    {
        Assert.Equal(RuleSeverity.Pass, InterfaceResultAcceptanceRule.EvaluateOrder(status).Severity);
    }
}
