using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class AboCompatibilityRuleTests
{
    private static AboRh Rh(AboGroup abo, RhType rh = RhType.Positive) => new(abo, rh);

    private static RuleResult Abo(IReadOnlyList<RuleResult> results) =>
        results.Single(r => r.Code == AboCompatibilityRule.AboCode);

    private static RuleResult RhResult(IReadOnlyList<RuleResult> results) =>
        results.Single(r => r.Code == AboCompatibilityRule.RhCode);

    [Theory]
    // O recipient takes only O.
    [InlineData(AboGroup.O, AboGroup.O, true)]
    [InlineData(AboGroup.O, AboGroup.A, false)]
    [InlineData(AboGroup.O, AboGroup.B, false)]
    [InlineData(AboGroup.O, AboGroup.AB, false)]
    // A recipient takes A, O.
    [InlineData(AboGroup.A, AboGroup.A, true)]
    [InlineData(AboGroup.A, AboGroup.O, true)]
    [InlineData(AboGroup.A, AboGroup.B, false)]
    [InlineData(AboGroup.A, AboGroup.AB, false)]
    // B recipient takes B, O.
    [InlineData(AboGroup.B, AboGroup.B, true)]
    [InlineData(AboGroup.B, AboGroup.O, true)]
    [InlineData(AboGroup.B, AboGroup.A, false)]
    // AB recipient (universal RBC recipient).
    [InlineData(AboGroup.AB, AboGroup.AB, true)]
    [InlineData(AboGroup.AB, AboGroup.A, true)]
    [InlineData(AboGroup.AB, AboGroup.B, true)]
    [InlineData(AboGroup.AB, AboGroup.O, true)]
    public void RbcAbo_MatrixIsEnforced(AboGroup recipient, AboGroup donor, bool compatible)
    {
        var results = AboCompatibilityRule.Evaluate(Rh(recipient), Rh(donor), ComponentClass.RedBloodCells);
        Assert.Equal(compatible ? RuleSeverity.Pass : RuleSeverity.HardStop, Abo(results).Severity);
    }

    [Theory]
    // Plasma is the inverse direction: AB is the universal plasma donor.
    [InlineData(AboGroup.O, AboGroup.AB, true)]
    [InlineData(AboGroup.A, AboGroup.AB, true)]
    [InlineData(AboGroup.AB, AboGroup.O, false)]
    [InlineData(AboGroup.AB, AboGroup.AB, true)]
    public void PlasmaAbo_UsesInverseMatrix(AboGroup recipient, AboGroup donor, bool compatible)
    {
        var results = AboCompatibilityRule.Evaluate(Rh(recipient), Rh(donor), ComponentClass.Plasma);
        Assert.Equal(compatible ? RuleSeverity.Pass : RuleSeverity.HardStop, Abo(results).Severity);
    }

    [Fact]
    public void RhPositiveToRhNegative_Rbc_IsHardStop()
    {
        var results = AboCompatibilityRule.Evaluate(Rh(AboGroup.O, RhType.Negative), Rh(AboGroup.O, RhType.Positive), ComponentClass.RedBloodCells);
        Assert.Equal(RuleSeverity.HardStop, RhResult(results).Severity);
    }

    [Fact]
    public void RhNegativeRecipient_RhNegativeDonor_Passes()
    {
        var results = AboCompatibilityRule.Evaluate(Rh(AboGroup.O, RhType.Negative), Rh(AboGroup.O, RhType.Negative), ComponentClass.RedBloodCells);
        Assert.Equal(RuleSeverity.Pass, RhResult(results).Severity);
    }

    [Fact]
    public void Rh_IsNotStrict_ForPlasma()
    {
        var results = AboCompatibilityRule.Evaluate(Rh(AboGroup.AB, RhType.Negative), Rh(AboGroup.AB, RhType.Positive), ComponentClass.Plasma);
        Assert.Equal(RuleSeverity.Pass, RhResult(results).Severity);
    }

    [Fact]
    public void UnknownAbo_IsHardStop()
    {
        var results = AboCompatibilityRule.Evaluate(Rh(AboGroup.Unknown), Rh(AboGroup.O), ComponentClass.RedBloodCells);
        Assert.Contains(results, r => r.Code == AboCompatibilityRule.UnknownTypeCode && r.Severity == RuleSeverity.HardStop);
    }
}
