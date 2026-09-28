using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class BillingDftExportRuleTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(Hl7MessageStatus.Acked)]
    public void AcceptedOrMissingDft_Passes(Hl7MessageStatus? status)
    {
        Assert.Equal(RuleSeverity.Pass, BillingDftExportRule.Evaluate(status).Severity);
    }

    [Theory]
    [InlineData(Hl7MessageStatus.Received)]
    [InlineData(Hl7MessageStatus.Errored)]
    [InlineData(Hl7MessageStatus.Nacked)]
    [InlineData(Hl7MessageStatus.Processed)]
    public void UnacceptedDft_HardStops(Hl7MessageStatus status)
    {
        var result = BillingDftExportRule.Evaluate(status);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(BillingDftExportRule.Code, result.Code);
    }
}
