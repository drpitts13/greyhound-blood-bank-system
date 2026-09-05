using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ResultLifecycleRuleTests
{
    [Theory]
    [InlineData(ResultSource.Manual, ResultStatus.Entered)]
    [InlineData(ResultSource.Calculated, ResultStatus.Entered)]
    [InlineData(ResultSource.Instrument, ResultStatus.PendingVerification)]
    [InlineData(ResultSource.Interface, ResultStatus.PendingVerification)]
    public void InitialStatus_DependsOnSource(ResultSource source, ResultStatus expected)
    {
        Assert.Equal(expected, ResultLifecycleRule.InitialStatus(source));
    }

    [Theory]
    [InlineData(ResultStatus.Entered, true)]
    [InlineData(ResultStatus.PendingVerification, true)]
    [InlineData(ResultStatus.Corrected, true)]
    [InlineData(ResultStatus.Verified, false)]
    [InlineData(ResultStatus.Invalidated, false)]
    public void CanVerify_OnlyUnreleasedClinicalStatuses(ResultStatus status, bool expected)
    {
        Assert.Equal(expected, ResultLifecycleRule.CanVerify(status));
    }

    [Fact]
    public void Verified_IsNotUpdatedInPlace()
    {
        Assert.False(ResultLifecycleRule.CanUpdateInPlace(ResultStatus.Verified));
        Assert.True(ResultLifecycleRule.CanCorrect(ResultStatus.Verified));
        Assert.True(ResultLifecycleRule.CreatesNewVersionOnInvalidate(ResultStatus.Verified));
    }

    [Fact]
    public void Corrected_InvalidationRestoresPriorVerified()
    {
        Assert.True(ResultLifecycleRule.RestoresPriorVerifiedOnInvalidate(ResultStatus.Corrected));
        Assert.False(ResultLifecycleRule.CreatesNewVersionOnInvalidate(ResultStatus.Corrected));
    }

    [Fact]
    public void IsCurrentRow_IsOnlyUnsuperseded()
    {
        Assert.True(ResultLifecycleRule.IsCurrentRow(null));
        Assert.False(ResultLifecycleRule.IsCurrentRow(12));
    }
}
