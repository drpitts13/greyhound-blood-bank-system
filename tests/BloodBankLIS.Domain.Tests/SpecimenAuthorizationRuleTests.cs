using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class SpecimenAuthorizationRuleTests
{
    [Fact]
    public void Accession_WithoutPermission_IsHardStop()
    {
        var result = SpecimenAuthorizationRule.EvaluateAccession(hasSpecimenAccession: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(SpecimenAuthorizationRule.AccessionCode, result.Code);
    }

    [Fact]
    public void Accession_WithPermission_Passes()
    {
        var result = SpecimenAuthorizationRule.EvaluateAccession(hasSpecimenAccession: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Edit_WithoutPermission_IsHardStop()
    {
        var result = SpecimenAuthorizationRule.EvaluateEdit(hasSpecimenEdit: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(SpecimenAuthorizationRule.EditCode, result.Code);
    }

    [Fact]
    public void Edit_WithPermission_Passes()
    {
        var result = SpecimenAuthorizationRule.EvaluateEdit(hasSpecimenEdit: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Reject_WithoutPermission_IsHardStop()
    {
        var result = SpecimenAuthorizationRule.EvaluateReject(hasSpecimenReject: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(SpecimenAuthorizationRule.RejectCode, result.Code);
    }

    [Fact]
    public void Reject_WithPermission_Passes()
    {
        var result = SpecimenAuthorizationRule.EvaluateReject(hasSpecimenReject: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
