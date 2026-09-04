using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class PatientAuthorizationRuleTests
{
    [Fact]
    public void Write_WithoutPermission_IsHardStop()
    {
        var result = PatientAuthorizationRule.EvaluateWrite(hasPatientWrite: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(PatientAuthorizationRule.WriteCode, result.Code);
    }

    [Fact]
    public void Write_WithPermission_Passes()
    {
        var result = PatientAuthorizationRule.EvaluateWrite(hasPatientWrite: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Create_WithoutPermission_IsHardStop()
    {
        var result = PatientAuthorizationRule.EvaluateCreate(hasPatientWrite: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(PatientAuthorizationRule.CreateCode, result.Code);
    }

    [Fact]
    public void Create_WithPermission_Passes()
    {
        var result = PatientAuthorizationRule.EvaluateCreate(hasPatientWrite: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
