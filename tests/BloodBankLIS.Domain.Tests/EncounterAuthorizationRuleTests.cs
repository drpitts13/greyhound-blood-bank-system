using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class EncounterAuthorizationRuleTests
{
    [Fact]
    public void Create_WithoutPatientWrite_IsHardStop()
    {
        var result = EncounterAuthorizationRule.EvaluateCreate(hasPatientWrite: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(EncounterAuthorizationRule.CreateCode, result.Code);
    }

    [Fact]
    public void Create_WithPatientWrite_Passes()
    {
        var result = EncounterAuthorizationRule.EvaluateCreate(hasPatientWrite: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Update_WithoutPatientWrite_IsHardStop()
    {
        var result = EncounterAuthorizationRule.EvaluateUpdate(hasPatientWrite: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(EncounterAuthorizationRule.UpdateCode, result.Code);
    }

    [Fact]
    public void Update_WithPatientWrite_Passes()
    {
        var result = EncounterAuthorizationRule.EvaluateUpdate(hasPatientWrite: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
