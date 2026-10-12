using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class DatElutionWorkupRuleTests
{
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
