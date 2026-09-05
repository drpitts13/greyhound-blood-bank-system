using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class CompatibilityRuleValidatorTests
{
    [Fact]
    public void Version_MissingIdentifier_IsHardStopped()
    {
        var version = new CompatibilityRuleVersion
        {
            PolicyVersion = "P1",
            EffectiveDate = new DateOnly(2026, 1, 1),
            Notes = "Reviewed."
        };
        var result = CompatibilityRuleValidator.ValidateVersion(version, false, null, requireReason: false);
        Assert.Contains(result.HardStops, r => r.Code == CompatibilityRuleValidator.VersionRequiredCode);
    }

    [Fact]
    public void Version_UpdateWithoutReason_IsHardStopped()
    {
        var version = new CompatibilityRuleVersion
        {
            Version = "2.0",
            PolicyVersion = "P1",
            EffectiveDate = new DateOnly(2026, 1, 1),
            Notes = "Reviewed."
        };
        var result = CompatibilityRuleValidator.ValidateVersion(version, false, "short", requireReason: true);
        Assert.Contains(result.HardStops, r => r.Code == CompatibilityRuleValidator.ReasonCode);
    }

    [Fact]
    public void Rule_InvalidJson_IsHardStopped()
    {
        var rule = new CompatibilityRule
        {
            RuleCode = "ISS-ABO-COMPAT",
            ComponentClass = ComponentClass.RedBloodCells,
            Severity = "HardStop",
            Description = "ABO",
            ExpressionJson = "not-json"
        };
        var result = CompatibilityRuleValidator.ValidateRule(rule, true, false);
        Assert.Contains(result.HardStops, r => r.Code == CompatibilityRuleValidator.ExpressionCode);
    }

    [Fact]
    public void Rule_DuplicateCode_IsHardStopped()
    {
        var rule = new CompatibilityRule
        {
            RuleCode = "ISS-ABO-COMPAT",
            ComponentClass = ComponentClass.RedBloodCells,
            Severity = "HardStop",
            Description = "ABO",
            ExpressionJson = "{}"
        };
        var result = CompatibilityRuleValidator.ValidateRule(rule, true, true);
        Assert.Contains(result.HardStops, r => r.Code == CompatibilityRuleValidator.RuleCodeDuplicate);
    }

    [Fact]
    public void Rule_ValidObject_Passes()
    {
        var rule = new CompatibilityRule
        {
            RuleCode = "ISS-ABO-COMPAT",
            ComponentClass = ComponentClass.RedBloodCells,
            Severity = "Warning",
            Description = "ABO",
            ExpressionJson = """{"alwaysFail":false}"""
        };
        var result = CompatibilityRuleValidator.ValidateRule(rule, true, false);
        Assert.False(result.IsHardStopped);
    }

    [Fact]
    public void Catalog_IncludesAboAndExmCodes()
    {
        Assert.Contains(CompatibilityRuleCatalog.Defaults, d => d.RuleCode == AboCompatibilityRule.AboCode);
        Assert.Contains(CompatibilityRuleCatalog.Defaults, d => d.RuleCode == ElectronicCrossmatchEligibilityRule.Code);
    }
}
