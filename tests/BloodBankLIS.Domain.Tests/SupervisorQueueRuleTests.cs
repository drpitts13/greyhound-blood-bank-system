using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class SupervisorQueueRuleTests
{
    [Fact]
    public void Access_WithoutAnyPermission_IsHardStop()
    {
        var result = SupervisorQueueRule.EvaluateAccess(false, false, false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(SupervisorQueueRule.PermissionCode, result.Code);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Access_WithAnyPermission_Passes(bool immuno, bool verify, bool release)
    {
        Assert.Equal(
            RuleSeverity.Pass,
            SupervisorQueueRule.EvaluateAccess(immuno, verify, release).Severity);
    }

    [Fact]
    public void Include_RespectsPermissionFlags()
    {
        Assert.True(SupervisorQueueRule.IncludeAntibodyReview(true));
        Assert.False(SupervisorQueueRule.IncludeAntibodyReview(false));
        Assert.True(SupervisorQueueRule.IncludeResultVerify(true));
        Assert.False(SupervisorQueueRule.IncludeResultVerify(false));
        Assert.True(SupervisorQueueRule.IncludeQuarantineRelease(true));
        Assert.False(SupervisorQueueRule.IncludeQuarantineRelease(false));
    }

    [Fact]
    public void KindSortOrder_PutsAntibodyReviewFirst()
    {
        Assert.True(
            SupervisorQueueRule.KindSortOrder(SupervisorQueueRule.KindAntibodyReview)
            < SupervisorQueueRule.KindSortOrder(SupervisorQueueRule.KindResultVerify));
        Assert.True(
            SupervisorQueueRule.KindSortOrder(SupervisorQueueRule.KindResultVerify)
            < SupervisorQueueRule.KindSortOrder(SupervisorQueueRule.KindQuarantineRelease));
    }
}
