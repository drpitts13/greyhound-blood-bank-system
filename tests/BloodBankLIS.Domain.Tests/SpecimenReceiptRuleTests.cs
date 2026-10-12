using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class SpecimenReceiptRuleTests
{
    private static readonly DateTime Now = new(2026, 10, 4, 21, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Receive_CollectedInDate_Passes()
    {
        var result = SpecimenReceiptRule.EvaluateReceive(SpecimenStatus.Collected, Now.AddHours(12), Now);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
        Assert.Equal(SpecimenReceiptRule.ReceiveStatusCode, result.Code);
    }

    [Fact]
    public void Receive_NotCollected_IsHardStop()
    {
        var result = SpecimenReceiptRule.EvaluateReceive(SpecimenStatus.Received, Now.AddHours(12), Now);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(SpecimenReceiptRule.ReceiveStatusCode, result.Code);
    }

    [Fact]
    public void Receive_Expired_IsHardStop()
    {
        var result = SpecimenReceiptRule.EvaluateReceive(SpecimenStatus.Collected, Now.AddMinutes(-1), Now);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(SpecimenReceiptRule.ReceiveExpiredCode, result.Code);
    }

    [Fact]
    public void Accept_ReceivedInDate_Passes()
    {
        var result = SpecimenReceiptRule.EvaluateAccept(SpecimenStatus.Received, Now.AddHours(12), Now);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
        Assert.Equal(SpecimenReceiptRule.AcceptStatusCode, result.Code);
    }

    [Fact]
    public void Accept_Collected_IsHardStop()
    {
        var result = SpecimenReceiptRule.EvaluateAccept(SpecimenStatus.Collected, Now.AddHours(12), Now);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(SpecimenReceiptRule.AcceptStatusCode, result.Code);
    }

    [Fact]
    public void Accept_Expired_IsHardStop()
    {
        var result = SpecimenReceiptRule.EvaluateAccept(SpecimenStatus.Received, Now, Now);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(SpecimenReceiptRule.AcceptExpiredCode, result.Code);
    }

    [Fact]
    public void PendingReceipt_IncludesCollectedAndReceivedOnly()
    {
        Assert.True(SpecimenReceiptRule.IsPendingReceipt(SpecimenStatus.Collected));
        Assert.True(SpecimenReceiptRule.IsPendingReceipt(SpecimenStatus.Received));
        Assert.False(SpecimenReceiptRule.IsPendingReceipt(SpecimenStatus.Accepted));
        Assert.False(SpecimenReceiptRule.IsPendingReceipt(SpecimenStatus.Rejected));
    }
}
