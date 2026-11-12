using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class DatElutionWorkupRuleTests
{
    // TEST-BB-1013: reagent grades must agree with the DAT result; a reacting control is a warning; grades may be left
    // not tested (end-user testing pass 9).
    [Fact]
    public void Grades_MustAgreeWithTheResult()
    {
        static RuleSeverity Grades(DatWorkupResult r, ReactionGrade poly, ReactionGrade igg, ReactionGrade c3) =>
            DatElutionWorkupRule.EvaluateGrades(r, poly, igg, c3, ReactionGrade.NotTested)
                .Single(x => x.Code == DatElutionWorkupRule.GradesCode).Severity;

        Assert.Equal(RuleSeverity.Pass, Grades(DatWorkupResult.Positive, ReactionGrade.TwoPlus, ReactionGrade.TwoPlus, ReactionGrade.Negative));
        Assert.Equal(RuleSeverity.Pass, Grades(DatWorkupResult.Positive, ReactionGrade.NotTested, ReactionGrade.NotTested, ReactionGrade.NotTested));
        Assert.Equal(RuleSeverity.Pass, Grades(DatWorkupResult.Negative, ReactionGrade.Negative, ReactionGrade.Negative, ReactionGrade.Negative));
        Assert.Equal(RuleSeverity.HardStop, Grades(DatWorkupResult.Negative, ReactionGrade.NotTested, ReactionGrade.OnePlus, ReactionGrade.NotTested));
        Assert.Equal(RuleSeverity.HardStop, Grades(DatWorkupResult.Positive, ReactionGrade.Negative, ReactionGrade.Negative, ReactionGrade.NotTested));

        var control = DatElutionWorkupRule.EvaluateGrades(
            DatWorkupResult.Positive, ReactionGrade.OnePlus, ReactionGrade.NotTested, ReactionGrade.NotTested, ReactionGrade.Weak);
        Assert.Equal(RuleSeverity.Warning, control.Single(x => x.Code == DatElutionWorkupRule.ControlCode).Severity);

        Assert.Equal("Polyspecific 2+ · IgG 2+ · C3 0", DatElutionWorkupRule.GradeSummary(
            ReactionGrade.TwoPlus, ReactionGrade.TwoPlus, ReactionGrade.Negative, ReactionGrade.NotTested));
        Assert.Null(DatElutionWorkupRule.GradeSummary(
            ReactionGrade.NotTested, ReactionGrade.NotTested, ReactionGrade.NotTested, ReactionGrade.NotTested));
    }

    [Fact]
    public void Permission_WithoutImmunoRecord_IsHardStop()
    {
        var result = DatElutionWorkupRule.EvaluatePermission(false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(DatElutionWorkupRule.PermissionCode, result.Code);
    }

    [Fact]
    public void Specimen_LinkedNotAccepted_IsHardStop()
    {
        var result = DatElutionWorkupRule.EvaluateSpecimen(SpecimenStatus.Collected, specimenLinked: true);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(DatElutionWorkupRule.SpecimenCode, result.Code);
    }

    [Fact]
    public void Complete_PositiveDatWithoutElution_IsHardStop()
    {
        var result = DatElutionWorkupRule.EvaluateComplete(
            DatWorkupResult.Positive, elutionResult: null, elutionNotPerformedReason: null);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(DatElutionWorkupRule.ElutionCode, result.Code);
    }

    [Fact]
    public void Complete_PositiveDatWithNotPerformedReason_Passes()
    {
        Assert.Equal(
            RuleSeverity.Pass,
            DatElutionWorkupRule.EvaluateComplete(
                DatWorkupResult.Positive, null, "Insufficient sample volume").Severity);
    }

    [Fact]
    public void Complete_NegativeDat_PassesWithoutElution()
    {
        Assert.Equal(
            RuleSeverity.Pass,
            DatElutionWorkupRule.EvaluateComplete(DatWorkupResult.Negative, null, null).Severity);
    }

    [Fact]
    public void VoidReason_Blank_IsHardStop()
    {
        var result = DatElutionWorkupRule.EvaluateVoidReason("  ");
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(DatElutionWorkupRule.VoidReasonCode, result.Code);
    }
}
