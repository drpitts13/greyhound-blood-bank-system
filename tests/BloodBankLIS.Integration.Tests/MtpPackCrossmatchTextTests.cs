using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-326: the pack report prints the crossmatch status the issue recorded; it never infers "crossmatched".
public class MtpPackCrossmatchTextTests
{
    [Theory]
    [InlineData(true, CrossmatchClinicalStatus.NotPerformed, "Uncrossmatched")]
    [InlineData(false, CrossmatchClinicalStatus.NotCrossmatchedEmergency, "Uncrossmatched")]
    [InlineData(false, CrossmatchClinicalStatus.Compatible, "Compatible")]
    [InlineData(false, CrossmatchClinicalStatus.NotPerformed, "Not crossmatched")]
    [InlineData(false, CrossmatchClinicalStatus.Expired, "Crossmatch expired")]
    public void CrossmatchText(bool testsIncomplete, CrossmatchClinicalStatus status, string expected) =>
        Assert.Equal(expected, MtpPackingService.CrossmatchText(new Issue { TestsIncompleteAtIssue = testsIncomplete, CrossmatchStatus = status }));
}
