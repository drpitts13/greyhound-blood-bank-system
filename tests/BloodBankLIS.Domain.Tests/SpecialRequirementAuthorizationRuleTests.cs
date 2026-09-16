using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Config;

namespace BloodBankLIS.Domain.Tests;

public class SpecialRequirementAuthorizationRuleTests
{
    [Fact]
    public void Create_WithoutPermission_IsHardStop()
    {
        var result = SpecialRequirementAuthorizationRule.EvaluateCreate(false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(SpecialRequirementAuthorizationRule.CreateCode, result.Code);
    }

    [Fact]
    public void Create_WithPermission_Passes()
    {
        Assert.Equal(RuleSeverity.Pass, SpecialRequirementAuthorizationRule.EvaluateCreate(true).Severity);
    }
}

public class SpecialRequirementDefinitionValidatorTests
{
    [Fact]
    public void MissingCode_IsHardStop()
    {
        var d = new SpecialRequirementDefinition { Name = "Irradiated", EnforcementKind = SpecialRequirementEnforcementKind.RequireProductAttribute, ProductAttributeCode = "IRRAD" };
        var evaluation = SpecialRequirementDefinitionValidator.Validate(d, duplicateActiveCode: false, knownProductAttribute: true);
        Assert.Contains(evaluation.HardStops, r => r.Code == "SRDEF.CODE.REQUIRED");
    }

    [Fact]
    public void ProductAttributeRequired_WhenKindRequiresIt()
    {
        var d = new SpecialRequirementDefinition { Code = "IRRAD", Name = "Irradiated", EnforcementKind = SpecialRequirementEnforcementKind.RequireProductAttribute };
        var evaluation = SpecialRequirementDefinitionValidator.Validate(d, false, knownProductAttribute: true);
        Assert.Contains(evaluation.HardStops, r => r.Code == "SRDEF.ATTR.REQUIRED");
    }

    [Fact]
    public void UnknownProductAttribute_IsHardStop()
    {
        var d = new SpecialRequirementDefinition
        {
            Code = "IRRAD",
            Name = "Irradiated",
            EnforcementKind = SpecialRequirementEnforcementKind.RequireProductAttribute,
            ProductAttributeCode = "NOPE"
        };
        var evaluation = SpecialRequirementDefinitionValidator.Validate(d, false, knownProductAttribute: false);
        Assert.Contains(evaluation.HardStops, r => r.Code == "SRDEF.ATTR.UNKNOWN");
    }

    [Fact]
    public void ValidDefinition_Passes()
    {
        var d = new SpecialRequirementDefinition
        {
            Code = "WARMER",
            Name = "Blood warmer",
            Level = SpecialRequirementLevel.Issuing,
            EnforcementKind = SpecialRequirementEnforcementKind.RequireIssueAcknowledgment
        };
        var evaluation = SpecialRequirementDefinitionValidator.Validate(d, false, true);
        Assert.False(evaluation.IsHardStopped);
    }
}
