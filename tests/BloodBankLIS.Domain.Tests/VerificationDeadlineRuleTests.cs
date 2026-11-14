using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-368: a result waiting for verification names the time its specimen expires, after which it cannot be verified.
public class VerificationDeadlineRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Window = TimeSpan.FromHours(24);

    [Theory]
    [InlineData(ResultStatus.Entered)]
    [InlineData(ResultStatus.PendingVerification)]
    public void PendingResult_WithSpecimenExpiringInsideTheWindow_GetsTheDeadline(ResultStatus status)
    {
        var notice = VerificationDeadlineRule.Notice(status, Now.AddHours(3), Now, Window);
        Assert.Equal("Verify before 2026-11-02 15:00 UTC, when the specimen expires. After that this result cannot be verified.", notice);
    }

    [Fact]
    public void NoDeadline_OutsideTheWindow_AfterExpiry_OrWithoutAnExpiration()
    {
        Assert.Null(VerificationDeadlineRule.Notice(ResultStatus.PendingVerification, Now.AddHours(25), Now, Window));
        Assert.Null(VerificationDeadlineRule.Notice(ResultStatus.PendingVerification, Now, Now, Window));
        Assert.Null(VerificationDeadlineRule.Notice(ResultStatus.PendingVerification, null, Now, Window));
    }

    [Theory]
    [InlineData(ResultStatus.Corrected)]
    [InlineData(ResultStatus.Verified)]
    [InlineData(ResultStatus.Invalidated)]
    public void NoDeadline_ForCorrectionsOrFinishedResults(ResultStatus status)
    {
        Assert.Null(VerificationDeadlineRule.Notice(status, Now.AddHours(1), Now, Window));
    }

    [Fact]
    public void OnlyAPendingCorrection_IsAllowedAnExpiredSpecimen()
    {
        Assert.True(VerificationDeadlineRule.AllowsExpiredSpecimen(ResultStatus.Corrected));
        Assert.False(VerificationDeadlineRule.AllowsExpiredSpecimen(ResultStatus.PendingVerification));
        Assert.False(VerificationDeadlineRule.AllowsExpiredSpecimen(null));
    }
}
