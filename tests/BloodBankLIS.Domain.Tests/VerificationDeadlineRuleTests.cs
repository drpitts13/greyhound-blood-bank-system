using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-368: a result waiting for verification names the time its specimen expires.
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
        Assert.Equal($"Verify before {ClinicalTimeText.Local(Now.AddHours(3))}, when the specimen expires, so a repeat can still be run on it. It can be verified after that (OCD-093), but nothing new can be tested on the tube.", notice);
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
    public void APendingCorrection_IsAllowedAnExpiredSpecimen()
    {
        Assert.True(VerificationDeadlineRule.AllowsExpiredSpecimen(ResultStatus.Corrected));
        Assert.False(VerificationDeadlineRule.AllowsExpiredSpecimen(ResultStatus.PendingVerification));
        Assert.False(VerificationDeadlineRule.AllowsExpiredSpecimen(null));
    }

    // TEST-BB-1249 (OCD-093, end-user testing pass 227): a result entered (and tested) while the specimen was in date may be
    // verified after it expires; one entered or tested at or after expiry, or with no entry time, may not.
    [Fact]
    public void AnEnteredResult_FromBeforeExpiry_IsAllowedAnExpiredSpecimen()
    {
        var expires = Now.AddHours(-1);
        Assert.True(VerificationDeadlineRule.AllowsExpiredSpecimen(ResultStatus.Entered, Now.AddHours(-5), null, expires));
        Assert.True(VerificationDeadlineRule.AllowsExpiredSpecimen(ResultStatus.PendingVerification, Now.AddHours(-5), Now.AddHours(-6), expires));
        Assert.False(VerificationDeadlineRule.AllowsExpiredSpecimen(ResultStatus.Entered, expires, null, expires));
        Assert.False(VerificationDeadlineRule.AllowsExpiredSpecimen(ResultStatus.Entered, Now.AddHours(-5), expires, expires));
        Assert.False(VerificationDeadlineRule.AllowsExpiredSpecimen(ResultStatus.Entered, null, null, expires));
        Assert.False(VerificationDeadlineRule.AllowsExpiredSpecimen(ResultStatus.Verified, Now.AddHours(-5), null, expires));
        Assert.True(ExpiredSpecimenVerificationRule.EnteredWhileInDate(Now.AddHours(-5), null, expires));
        Assert.False(ExpiredSpecimenVerificationRule.EnteredWhileInDate(Now.AddHours(-5), null, null));
    }
}
