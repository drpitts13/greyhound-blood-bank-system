using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-306: patient-blood-management criteria evaluation. Thresholds here are test
// fixtures only; the software ships none (OCD-038).
public class TransfusionIndicationRuleTests
{
    private static readonly DateTime Ordered = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    private static TransfusionIndicationCriterion Lab(string code, string test, IndicationComparison cmp, decimal threshold, int lookback = 24) => new()
    {
        Code = code, Name = code, ComponentClass = ComponentClass.RedBloodCells, LabTestCode = test,
        Comparison = cmp, Threshold = threshold, Unit = "u", LookbackHours = lookback, IsActive = true
    };

    private static TransfusionIndicationCriterion Attest(string code) => new()
    {
        Code = code, Name = "Attested " + code, ComponentClass = ComponentClass.RedBloodCells, IsActive = true
    };

    [Theory]
    [InlineData(IndicationComparison.LessThan, 5, false)]
    [InlineData(IndicationComparison.LessOrEqual, 5, true)]
    [InlineData(IndicationComparison.GreaterThan, 5, false)]
    [InlineData(IndicationComparison.GreaterOrEqual, 5, true)]
    [InlineData(IndicationComparison.LessThan, 6, true)]
    public void Compare_AppliesTheOperator(IndicationComparison cmp, decimal threshold, bool expected)
    {
        Assert.Equal(expected, TransfusionIndicationRule.Compare(5m, cmp, threshold));
    }

    [Fact]
    public void NoActiveCriteria_IsNoCriteria()
    {
        var inactive = Lab("A", "T1", IndicationComparison.LessThan, 1);
        inactive.IsActive = false;
        Assert.Equal(PbmStatus.NoCriteria, TransfusionIndicationRule.Evaluate([inactive], [], Ordered, null).Status);
    }

    [Fact]
    public void LatestResultInsideLookback_DecidesALabCriterion()
    {
        var criteria = new[] { Lab("LOW", "T1", IndicationComparison.LessThan, 10m, lookback: 12) };
        var met = TransfusionIndicationRule.Evaluate(criteria,
            [new("T1", 12m, "u", Ordered.AddHours(-6)), new("T1", 8m, "u", Ordered.AddHours(-1))], Ordered, null);
        Assert.Equal(PbmStatus.Met, met.Status);
        Assert.Equal("LOW", met.MetCriterionCode);

        var latestHigh = TransfusionIndicationRule.Evaluate(criteria,
            [new("T1", 8m, "u", Ordered.AddHours(-6)), new("T1", 12m, "u", Ordered.AddHours(-1))], Ordered, null);
        Assert.Equal(PbmStatus.OutsideCriteria, latestHigh.Status);
        Assert.Contains("latest T1 12", latestHigh.Detail);

        var stale = TransfusionIndicationRule.Evaluate(criteria, [new("T1", 8m, "u", Ordered.AddHours(-13))], Ordered, null);
        Assert.Equal(PbmStatus.OutsideCriteria, stale.Status);
        Assert.Contains("no result in the last 12 h", stale.Detail);

        var future = TransfusionIndicationRule.Evaluate(criteria, [new("T1", 8m, "u", Ordered.AddMinutes(5))], Ordered, null);
        Assert.Equal(PbmStatus.OutsideCriteria, future.Status);
    }

    [Fact]
    public void SelectedAttestation_MeetsCriteria_ButOnlyIfItIsAnActiveAttestation()
    {
        var criteria = new[] { Lab("LOW", "T1", IndicationComparison.LessThan, 10m), Attest("BLEED") };
        Assert.Equal(PbmStatus.Met, TransfusionIndicationRule.Evaluate(criteria, [], Ordered, "bleed").Status);
        Assert.Equal(PbmStatus.OutsideCriteria, TransfusionIndicationRule.Evaluate(criteria, [], Ordered, "LOW").Status);
        Assert.Equal(PbmStatus.OutsideCriteria, TransfusionIndicationRule.Evaluate(criteria, [], Ordered, "NOPE").Status);
    }

    [Fact]
    public void Justification_RequiredOnlyWhenOutsideAndPolicyOn()
    {
        Assert.Equal(RuleSeverity.HardStop, TransfusionIndicationRule.EvaluateJustification(PbmStatus.OutsideCriteria, true, " ").Severity);
        Assert.Equal(RuleSeverity.Pass, TransfusionIndicationRule.EvaluateJustification(PbmStatus.OutsideCriteria, true, "Symptomatic").Severity);
        Assert.Equal(RuleSeverity.Pass, TransfusionIndicationRule.EvaluateJustification(PbmStatus.OutsideCriteria, false, null).Severity);
        Assert.Equal(RuleSeverity.Pass, TransfusionIndicationRule.EvaluateJustification(PbmStatus.Met, true, null).Severity);
    }

    [Fact]
    public void Validate_LabNeedsComparisonThresholdAndLookback_AttestationNeedsNone()
    {
        var lab = Lab("X", "T1", IndicationComparison.LessThan, 1);
        Assert.False(TransfusionIndicationRule.Validate(lab, false).IsHardStopped);
        lab.Threshold = null;
        Assert.True(TransfusionIndicationRule.Validate(lab, false).IsHardStopped);
        var badLookback = Lab("Y", "T1", IndicationComparison.LessThan, 1, lookback: 721);
        Assert.True(TransfusionIndicationRule.Validate(badLookback, false).IsHardStopped);
        var attest = Attest("Z");
        Assert.False(TransfusionIndicationRule.Validate(attest, false).IsHardStopped);
        attest.Threshold = 3;
        Assert.True(TransfusionIndicationRule.Validate(attest, false).IsHardStopped);
        Assert.True(TransfusionIndicationRule.Validate(Attest("D"), duplicateCode: true).IsHardStopped);
    }
}
