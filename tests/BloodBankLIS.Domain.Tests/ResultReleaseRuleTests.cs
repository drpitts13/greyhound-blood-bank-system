using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-357
public class ResultReleaseRuleTests
{
    [Theory]
    [InlineData(ResultStatus.Verified, 1, true, "F")]
    [InlineData(ResultStatus.Verified, 2, true, "C")]
    [InlineData(ResultStatus.Invalidated, 2, true, "W")]
    public void ReleasedResults_CarryTheirStatus(ResultStatus status, int version, bool wasVerified, string expected) =>
        Assert.Equal(expected, ResultReleaseRule.ObxStatus(status, version, wasVerified));

    [Theory]
    [InlineData(ResultStatus.Invalidated, 1, false)]
    [InlineData(ResultStatus.Corrected, 2, true)]
    [InlineData(ResultStatus.PendingVerification, 1, false)]
    public void UnverifiedOrNeverReleasedResults_AreNotSent(ResultStatus status, int version, bool wasVerified) =>
        Assert.Null(ResultReleaseRule.ObxStatus(status, version, wasVerified));

    // TEST-BB-358 (OCD-049)
    [Theory]
    [InlineData("C", true)]
    [InlineData("W", true)]
    [InlineData("F", false)]
    [InlineData(null, false)]
    public void OnlyCorrectionsAndWithdrawals_AreSentAutomatically(string? status, bool expected) =>
        Assert.Equal(expected, ResultReleaseRule.SentAutomatically(status));

    // TEST-BB-1018 (OCD-086): with the facility setting on, a first final result goes automatically too.
    [Theory]
    [InlineData("F", true, true)]
    [InlineData("F", false, false)]
    [InlineData("C", false, true)]
    [InlineData(null, true, false)]
    public void FinalResults_FollowTheFacilitySetting(string? status, bool sendFinal, bool expected) =>
        Assert.Equal(expected, ResultReleaseRule.SentAutomatically(status, sendFinal));
}
