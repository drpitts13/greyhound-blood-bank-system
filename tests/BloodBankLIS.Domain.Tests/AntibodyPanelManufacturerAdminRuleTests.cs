using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class AntibodyPanelManufacturerAdminRuleTests
{
    [Fact]
    public void Create_WithoutPermission_IsHardStop()
    {
        var result = AntibodyPanelManufacturerAdminRule.EvaluateCreate(hasAdminConfigEdit: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AntibodyPanelManufacturerAdminRule.CreatePermCode, result.Code);
    }

    [Fact]
    public void CreateDraft_BlankCode_IsHardStop()
    {
        var stops = AntibodyPanelManufacturerAdminRule.EvaluateCreateDraft("  ", "Greyhound", duplicateCode: false);
        Assert.Contains(stops, r => r.Code == AntibodyPanelManufacturerAdminRule.IdentityCode);
    }

    [Fact]
    public void CreateDraft_DuplicateCode_IsHardStop()
    {
        var stops = AntibodyPanelManufacturerAdminRule.EvaluateCreateDraft("GHP", "Greyhound", duplicateCode: true);
        Assert.Contains(stops, r => r.Code == AntibodyPanelManufacturerAdminRule.DuplicateCode);
    }

    [Fact]
    public void CreateDraft_Valid_Passes()
    {
        Assert.Empty(AntibodyPanelManufacturerAdminRule.EvaluateCreateDraft("GHP", "Greyhound", duplicateCode: false));
    }

    [Fact]
    public void Deactivate_WithoutReason_IsHardStop()
    {
        var result = AntibodyPanelManufacturerAdminRule.EvaluateDeactivateReason(null);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AntibodyPanelManufacturerAdminRule.DeactivateReasonCode, result.Code);
    }
}
