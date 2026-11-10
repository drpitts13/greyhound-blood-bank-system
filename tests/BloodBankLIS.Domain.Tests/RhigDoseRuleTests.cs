using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

/// <summary>OCD-069 (decided 2026-11-10): the suggested RhIG decision and the dose calculated from facility settings.</summary>
public class RhigDoseRuleTests
{
    private static RhigDoseRule.Settings Set(RhigDoseRule.Rounding rounding) => new(30, 5000, rounding);

    // TEST-BB-868: fetal whole blood = % × maternal volume; vials = that ÷ mL per vial, rounded as set (up; nearest plus
    // one; up plus one), never below one when rounding up; nothing is calculated without a positive screen's
    // quantitation or with any setting unset; the explanation shows the working.
    [Fact]
    public void Calculate_UsesTheFacilitySettings()
    {
        // 0.6% × 5000 mL = 30 mL; ÷ 30 = 1.0.
        Assert.Equal(1, RhigDoseRule.Calculate(FetalBleedScreen.Positive, 0.6m, Set(RhigDoseRule.Rounding.RoundUp)).Vials);
        Assert.Equal(2, RhigDoseRule.Calculate(FetalBleedScreen.Positive, 0.6m, Set(RhigDoseRule.Rounding.NearestPlusOne)).Vials);
        Assert.Equal(2, RhigDoseRule.Calculate(FetalBleedScreen.Positive, 0.6m, Set(RhigDoseRule.Rounding.UpPlusOne)).Vials);
        // 0.9% → 45 mL; ÷ 30 = 1.5.
        Assert.Equal(2, RhigDoseRule.Calculate(FetalBleedScreen.Positive, 0.9m, Set(RhigDoseRule.Rounding.RoundUp)).Vials);
        Assert.Equal(3, RhigDoseRule.Calculate(FetalBleedScreen.Positive, 0.9m, Set(RhigDoseRule.Rounding.NearestPlusOne)).Vials);
        Assert.Equal(3, RhigDoseRule.Calculate(FetalBleedScreen.Positive, 0.9m, Set(RhigDoseRule.Rounding.UpPlusOne)).Vials);
        // 0.1% → 5 mL; ÷ 30 = 0.17: rounding up still gives one vial.
        Assert.Equal(1, RhigDoseRule.Calculate(FetalBleedScreen.Positive, 0.1m, Set(RhigDoseRule.Rounding.RoundUp)).Vials);

        var explained = RhigDoseRule.Calculate(FetalBleedScreen.Positive, 0.9m, Set(RhigDoseRule.Rounding.NearestPlusOne)).Explanation;
        Assert.Equal("0.9% × 5000 mL = 45 mL fetal blood; ÷ 30 mL per vial = 1.5; rounded to the nearest and one added: 3 vials.", explained);

        Assert.Null(RhigDoseRule.Calculate(FetalBleedScreen.Negative, null, Set(RhigDoseRule.Rounding.RoundUp)).Vials);
        Assert.Null(RhigDoseRule.Calculate(FetalBleedScreen.Positive, null, Set(RhigDoseRule.Rounding.RoundUp)).Vials);
        var unset = RhigDoseRule.Calculate(FetalBleedScreen.Positive, 0.6m, new RhigDoseRule.Settings(30, 0, RhigDoseRule.Rounding.RoundUp));
        Assert.Null(unset.Vials);
        Assert.Contains("not set on Facility Policies", unset.Explanation);
        Assert.Null(RhigDoseRule.Calculate(FetalBleedScreen.Positive, 0.6m, new RhigDoseRule.Settings(30, 5000, RhigDoseRule.Rounding.NotSet)).Vials);
    }

    // TEST-BB-868 (continued): Rh positive or an Rh-negative infant suggests not a candidate; Rh negative with no anti-D and
    // an infant Rh positive or not known suggests a candidate; no Rh type on file, or anti-D on history, suggests nothing.
    [Fact]
    public void Suggest_FromTheFactsOnFile()
    {
        Assert.Equal(RhigDecision.NotCandidate, RhigDoseRule.Suggest(RhType.Positive, false, RhType.Positive).Suggested);
        Assert.Equal(RhigDecision.Candidate, RhigDoseRule.Suggest(RhType.Negative, false, RhType.Positive).Suggested);
        Assert.Equal(RhigDecision.Candidate, RhigDoseRule.Suggest(RhType.Negative, false, RhType.Unknown).Suggested);
        Assert.Equal(RhigDecision.NotCandidate, RhigDoseRule.Suggest(RhType.Negative, false, RhType.Negative).Suggested);

        var antiD = RhigDoseRule.Suggest(RhType.Negative, true, RhType.Positive);
        Assert.Null(antiD.Suggested);
        Assert.Contains("passive", antiD.Reason);
        Assert.Null(RhigDoseRule.Suggest(null, false, RhType.Positive).Suggested);
        Assert.Null(RhigDoseRule.Suggest(RhType.Unknown, false, RhType.Positive).Suggested);
    }
}
