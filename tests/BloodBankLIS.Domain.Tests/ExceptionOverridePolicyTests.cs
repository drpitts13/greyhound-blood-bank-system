using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ExceptionOverridePolicyTests
{
    [Fact]
    public void CanOverride_RequiresActiveOverridableAndSufficientLevel()
    {
        var def = new ExceptionDefinition
        {
            RuleCode = AboRhDeltaRule.DeltaCode,
            Name = "Delta",
            MinSecurityLevel = 2,
            IsOverridable = true,
            IsActive = true
        };

        Assert.False(ExceptionOverridePolicy.CanOverride(1, def));
        Assert.True(ExceptionOverridePolicy.CanOverride(2, def));
        Assert.True(ExceptionOverridePolicy.CanOverride(3, def));
        Assert.False(ExceptionOverridePolicy.CanOverride(3, null));

        def.IsActive = false;
        Assert.False(ExceptionOverridePolicy.CanOverride(3, def));

        def.IsActive = true;
        def.IsOverridable = false;
        Assert.False(ExceptionOverridePolicy.CanOverride(3, def));
    }

    [Fact]
    public void EvaluateAccess_ReturnsHardStopWhenLevelTooLow()
    {
        var def = new ExceptionDefinition
        {
            RuleCode = AboRhDeltaRule.DeltaCode,
            Name = "Delta",
            MinSecurityLevel = 2,
            IsOverridable = true,
            IsActive = true
        };

        var denied = ExceptionOverridePolicy.EvaluateAccess(1, def, AboRhDeltaRule.DeltaCode);
        Assert.Equal(RuleSeverity.HardStop, denied.Severity);
        Assert.Equal("EXC-SECURITY-LEVEL", denied.Code);

        var allowed = ExceptionOverridePolicy.EvaluateAccess(2, def, AboRhDeltaRule.DeltaCode);
        Assert.Equal(RuleSeverity.Pass, allowed.Severity);
    }

    [Fact]
    public void EvaluateAccess_MissingDefinition_IsHardStop()
    {
        var result = ExceptionOverridePolicy.EvaluateAccess(3, null, AboRhDeltaRule.DeltaCode);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal("EXC-DEF-MISSING", result.Code);
    }
}
