using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class AntibodyIdentificationWorklistRuleTests
{
    [Theory]
    [InlineData(AntibodyWorkupStatus.InProgress, AntibodyIdWorklistNextAction.RecordReactions)]
    [InlineData(AntibodyWorkupStatus.PendingInterpretation, AntibodyIdWorklistNextAction.Interpret)]
    [InlineData(AntibodyWorkupStatus.PendingSupervisorReview, AntibodyIdWorklistNextAction.Review)]
    [InlineData(AntibodyWorkupStatus.Completed, AntibodyIdWorklistNextAction.None)]
    [InlineData(AntibodyWorkupStatus.Voided, AntibodyIdWorklistNextAction.None)]
    public void NextAction_FollowsOpenStatus(
        AntibodyWorkupStatus status,
        AntibodyIdWorklistNextAction expected)
    {
        Assert.Equal(expected, AntibodyIdentificationWorklistRule.NextAction(status));
    }

    [Fact]
    public void SpecimenAttention_EmptyFilter_ShowsAll()
    {
        Assert.True(AntibodyIdentificationWorklistRule.MatchesSpecimenAttention(false, false, null));
        Assert.True(AntibodyIdentificationWorklistRule.MatchesSpecimenAttention(true, false, " "));
    }

    [Fact]
    public void SpecimenAttention_UnusableFilter_HidesExpiredAndClean()
    {
        Assert.True(AntibodyIdentificationWorklistRule.MatchesSpecimenAttention(
            true, false, AntibodyIdentificationWorklistRule.UnusableSpecimenFilter));
        Assert.False(AntibodyIdentificationWorklistRule.MatchesSpecimenAttention(
            false, true, AntibodyIdentificationWorklistRule.UnusableSpecimenFilter));
        Assert.False(AntibodyIdentificationWorklistRule.MatchesSpecimenAttention(
            false, false, AntibodyIdentificationWorklistRule.UnusableSpecimenFilter));
    }

    [Fact]
    public void SpecimenAttention_ExpiredFilter_HidesUnusableAndClean()
    {
        Assert.True(AntibodyIdentificationWorklistRule.MatchesSpecimenAttention(
            false, true, AntibodyIdentificationWorklistRule.ExpiredSpecimenFilter));
        Assert.False(AntibodyIdentificationWorklistRule.MatchesSpecimenAttention(
            true, false, AntibodyIdentificationWorklistRule.ExpiredSpecimenFilter));
        Assert.False(AntibodyIdentificationWorklistRule.MatchesSpecimenAttention(
            false, false, AntibodyIdentificationWorklistRule.ExpiredSpecimenFilter));
    }

    [Fact]
    public void SpecimenAttention_UnacceptedFilter_HidesUnusableExpiredAndClean()
    {
        Assert.True(AntibodyIdentificationWorklistRule.MatchesSpecimenAttention(
            false, false, AntibodyIdentificationWorklistRule.UnacceptedSpecimenFilter, true));
        Assert.False(AntibodyIdentificationWorklistRule.MatchesSpecimenAttention(
            true, false, AntibodyIdentificationWorklistRule.UnacceptedSpecimenFilter));
        Assert.False(AntibodyIdentificationWorklistRule.MatchesSpecimenAttention(
            false, true, AntibodyIdentificationWorklistRule.UnacceptedSpecimenFilter));
        Assert.False(AntibodyIdentificationWorklistRule.MatchesSpecimenAttention(
            false, false, AntibodyIdentificationWorklistRule.UnacceptedSpecimenFilter));
    }

    [Fact]
    public void SpecimenAttention_NotReadyFilter_HidesOtherAttentionAndClean()
    {
        Assert.True(AntibodyIdentificationWorklistRule.MatchesSpecimenAttention(
            false, false, AntibodyIdentificationWorklistRule.NotReadySpecimenFilter, false, true));
        Assert.False(AntibodyIdentificationWorklistRule.MatchesSpecimenAttention(
            true, false, AntibodyIdentificationWorklistRule.NotReadySpecimenFilter));
        Assert.False(AntibodyIdentificationWorklistRule.MatchesSpecimenAttention(
            false, true, AntibodyIdentificationWorklistRule.NotReadySpecimenFilter));
        Assert.False(AntibodyIdentificationWorklistRule.MatchesSpecimenAttention(
            false, false, AntibodyIdentificationWorklistRule.NotReadySpecimenFilter, true));
        Assert.False(AntibodyIdentificationWorklistRule.MatchesSpecimenAttention(
            false, false, AntibodyIdentificationWorklistRule.NotReadySpecimenFilter));
    }

    [Fact]
    public void NextAction_EmptyFilter_ShowsAll()
    {
        Assert.True(AntibodyIdentificationWorklistRule.MatchesNextAction(
            AntibodyIdWorklistNextAction.Interpret, null));
    }

    [Theory]
    [InlineData(AntibodyIdentificationWorklistRule.RecordReactionsFilter, AntibodyIdWorklistNextAction.RecordReactions, true)]
    [InlineData(AntibodyIdentificationWorklistRule.RecordReactionsFilter, AntibodyIdWorklistNextAction.Interpret, false)]
    [InlineData(AntibodyIdentificationWorklistRule.InterpretFilter, AntibodyIdWorklistNextAction.Interpret, true)]
    [InlineData(AntibodyIdentificationWorklistRule.InterpretFilter, AntibodyIdWorklistNextAction.Review, false)]
    [InlineData(AntibodyIdentificationWorklistRule.ReviewFilter, AntibodyIdWorklistNextAction.Review, true)]
    [InlineData(AntibodyIdentificationWorklistRule.ReviewFilter, AntibodyIdWorklistNextAction.RecordReactions, false)]
    public void NextAction_Filter_KeepsMatchingRows(
        string filter,
        AntibodyIdWorklistNextAction action,
        bool expected)
    {
        Assert.Equal(expected, AntibodyIdentificationWorklistRule.MatchesNextAction(action, filter));
    }

    [Fact]
    public void LotAttention_EmptyFilter_ShowsAll()
    {
        Assert.True(AntibodyIdentificationWorklistRule.MatchesLotAttention(false, false, null));
        Assert.True(AntibodyIdentificationWorklistRule.MatchesLotAttention(true, false, " "));
    }

    [Fact]
    public void LotAttention_Filter_KeepsInactiveOrExpired()
    {
        Assert.True(AntibodyIdentificationWorklistRule.MatchesLotAttention(
            true, false, AntibodyIdentificationWorklistRule.LotAttentionFilter));
        Assert.True(AntibodyIdentificationWorklistRule.MatchesLotAttention(
            false, true, AntibodyIdentificationWorklistRule.LotAttentionFilter));
        Assert.True(AntibodyIdentificationWorklistRule.MatchesLotAttention(
            true, true, AntibodyIdentificationWorklistRule.LotAttentionFilter));
        Assert.False(AntibodyIdentificationWorklistRule.MatchesLotAttention(
            false, false, AntibodyIdentificationWorklistRule.LotAttentionFilter));
    }

    [Fact]
    public void WithdrawnJudgment_EmptyFilter_ShowsAll()
    {
        Assert.True(AntibodyIdentificationWorklistRule.MatchesWithdrawnJudgment(false, null));
        Assert.True(AntibodyIdentificationWorklistRule.MatchesWithdrawnJudgment(true, " "));
    }

    [Fact]
    public void WithdrawnJudgment_Filter_KeepsWithdrawnOnly()
    {
        Assert.True(AntibodyIdentificationWorklistRule.MatchesWithdrawnJudgment(
            true, AntibodyIdentificationWorklistRule.WithdrawnJudgmentFilter));
        Assert.False(AntibodyIdentificationWorklistRule.MatchesWithdrawnJudgment(
            false, AntibodyIdentificationWorklistRule.WithdrawnJudgmentFilter));
    }

    [Fact]
    public void PendingTypeCorrection_EmptyFilter_ShowsAll()
    {
        Assert.True(AntibodyIdentificationWorklistRule.MatchesPendingTypeCorrection(false, null));
        Assert.True(AntibodyIdentificationWorklistRule.MatchesPendingTypeCorrection(true, " "));
    }

    [Fact]
    public void PendingTypeCorrection_Filter_KeepsPendingOnly()
    {
        Assert.True(AntibodyIdentificationWorklistRule.MatchesPendingTypeCorrection(
            true, AntibodyIdentificationWorklistRule.PendingTypeCorrectionFilter));
        Assert.False(AntibodyIdentificationWorklistRule.MatchesPendingTypeCorrection(
            false, AntibodyIdentificationWorklistRule.PendingTypeCorrectionFilter));
    }

    [Fact]
    public void OpenProducts_EmptyFilter_ShowsAll()
    {
        Assert.True(AntibodyIdentificationWorklistRule.MatchesOpenProducts(false, null));
        Assert.True(AntibodyIdentificationWorklistRule.MatchesOpenProducts(true, " "));
    }

    [Fact]
    public void OpenProducts_Filter_KeepsReservedOrIssuedOnly()
    {
        Assert.True(AntibodyIdentificationWorklistRule.MatchesOpenProducts(
            true, AntibodyIdentificationWorklistRule.OpenProductsFilter));
        Assert.False(AntibodyIdentificationWorklistRule.MatchesOpenProducts(
            false, AntibodyIdentificationWorklistRule.OpenProductsFilter));
    }
}
