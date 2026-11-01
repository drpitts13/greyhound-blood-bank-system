using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-317: eluate findings post only alloantibodies, only after a second person verifies (OCD-042).
public class EluateVerificationRuleTests
{
    [Fact]
    public void Verify_NeedsACompletedWorkup_AnotherPerson_AndOnlyOnce()
    {
        Assert.Equal(RuleSeverity.Pass, EluateVerificationRule.EvaluateVerify(DatElutionWorkupStatus.Completed, "tech1", "sup1", false).Severity);
        Assert.Equal(EluateVerificationRule.StatusCode, EluateVerificationRule.EvaluateVerify(DatElutionWorkupStatus.Open, "tech1", "sup1", false).Code);
        Assert.Equal(EluateVerificationRule.SelfCode, EluateVerificationRule.EvaluateVerify(DatElutionWorkupStatus.Completed, "tech1", " TECH1 ", false).Code);
        Assert.Contains("already verified", EluateVerificationRule.EvaluateVerify(DatElutionWorkupStatus.Completed, "tech1", "sup1", true).Message);
    }

    [Theory]
    [InlineData(EluateFindingKind.Alloantibody, true)]
    [InlineData(EluateFindingKind.Autoantibody, false)]
    [InlineData(EluateFindingKind.Panreactive, false)]
    [InlineData(EluateFindingKind.Nonspecific, false)]
    public void OnlyAlloantibodiesPost(EluateFindingKind kind, bool posts) =>
        Assert.Equal(posts, EluateVerificationRule.Posts(kind));

    [Fact]
    public void Alloantibody_MustBeNamed_AndInTheCatalog()
    {
        Assert.Equal(RuleSeverity.HardStop, EluateVerificationRule.EvaluateFinding(EluateFindingKind.Alloantibody, " ", false).Severity);
        Assert.Contains("not a catalog antibody", EluateVerificationRule.EvaluateFinding(EluateFindingKind.Alloantibody, "anti-Zz", false).Message);
        Assert.Equal(RuleSeverity.Pass, EluateVerificationRule.EvaluateFinding(EluateFindingKind.Alloantibody, "anti-E", true).Severity);
        Assert.Equal(RuleSeverity.Pass, EluateVerificationRule.EvaluateFinding(EluateFindingKind.Panreactive, null, false).Severity);
    }

    [Fact]
    public void FindingsAndVoid_AreLockedOnceVerified()
    {
        Assert.Equal(RuleSeverity.HardStop, EluateVerificationRule.EvaluateFindingsEditable(DatElutionWorkupStatus.Completed, verified: true).Severity);
        Assert.Equal(RuleSeverity.HardStop, EluateVerificationRule.EvaluateFindingsEditable(DatElutionWorkupStatus.Voided, verified: false).Severity);
        Assert.Equal(RuleSeverity.Pass, EluateVerificationRule.EvaluateFindingsEditable(DatElutionWorkupStatus.Completed, verified: false).Severity);
        Assert.Equal(RuleSeverity.HardStop, EluateVerificationRule.EvaluateVoid(verified: true).Severity);
        Assert.Equal(RuleSeverity.Pass, EluateVerificationRule.EvaluateVoid(verified: false).Severity);
    }
}
