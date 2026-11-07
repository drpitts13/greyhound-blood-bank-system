using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-573: an RhIG evaluation lists the facts on file without concluding from them, and is completed only when it
// says what was decided (OCD-069: candidacy and dose are the facility's).
public class RhigEvaluationRuleTests
{
    [Fact]
    public void Facts_StateWhatIsOnFile()
    {
        Assert.Equal(
            [
                "Patient's current Rh type on file: negative.",
                "Anti-D is on the patient's antibody history.",
                "Infant Rh: positive.",
                "Fetal bleed screen positive; quantitation 0.4% fetal cells."
            ],
            RhigEvaluationRule.Facts(RhType.Negative, true, RhType.Positive, FetalBleedScreen.Positive, 0.4m));
        Assert.Equal(
            ["No current Rh type on file for the patient.", "Infant Rh: not known.", "Fetal bleed screen not done."],
            RhigEvaluationRule.Facts(null, false, RhType.Unknown, FetalBleedScreen.NotDone, null));
    }

    [Theory]
    [InlineData(RhigDecision.Undetermined, null, null, FetalBleedScreen.NotDone, null, "candidate for RhIG")]
    [InlineData(RhigDecision.Candidate, null, null, FetalBleedScreen.Negative, null, "vials recommended")]
    [InlineData(RhigDecision.Candidate, 1, null, FetalBleedScreen.Positive, null, "quantitation")]
    [InlineData(RhigDecision.NotCandidate, null, " ", FetalBleedScreen.NotDone, null, "not a candidate")]
    public void Complete_NeedsTheDecisionRecorded(RhigDecision decision, int? vials, string? note, FetalBleedScreen screen, double? pct, string expected)
    {
        var result = RhigEvaluationRule.EvaluateComplete(decision, vials, note, screen, pct is null ? null : (decimal)pct);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Contains(expected, result.Message);
    }

    [Fact]
    public void Complete_Passes_WhenTheDecisionIsRecorded()
    {
        Assert.Equal(RuleSeverity.Pass, RhigEvaluationRule.EvaluateComplete(RhigDecision.Candidate, 2, null, FetalBleedScreen.Positive, 1.2m).Severity);
        Assert.Equal(RuleSeverity.Pass, RhigEvaluationRule.EvaluateComplete(RhigDecision.NotCandidate, null, "Infant Rh negative", FetalBleedScreen.NotDone, null).Severity);
        Assert.Equal(RuleSeverity.HardStop, RhigEvaluationRule.EvaluateValues(51, null).Severity);
        Assert.Equal(RuleSeverity.HardStop, RhigEvaluationRule.EvaluateValues(1, 101m).Severity);
    }
}
