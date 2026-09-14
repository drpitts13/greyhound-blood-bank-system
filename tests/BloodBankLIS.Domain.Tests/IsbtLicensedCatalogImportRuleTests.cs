using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class IsbtLicensedCatalogImportRuleTests
{
    [Fact]
    public void Permission_WithoutEdit_IsHardStop()
    {
        var result = IsbtLicensedCatalogImportRule.EvaluatePermission(false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(IsbtLicensedCatalogImportRule.PermissionCode, result.Code);
    }

    [Fact]
    public void WithoutLicenseAcknowledgment_IsHardStop()
    {
        var result = IsbtLicensedCatalogImportRule.Evaluate(false, "ICCBBA-ST-1", 1, 0);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(IsbtLicensedCatalogImportRule.LicenseCode, result.Code);
    }

    [Fact]
    public void PlaceholderVersion_IsHardStop()
    {
        var result = IsbtLicensedCatalogImportRule.Evaluate(true, "PLACEHOLDER-REQUIRES-ICCBBA", 1, 0);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(IsbtLicensedCatalogImportRule.VersionCode, result.Code);
    }

    [Fact]
    public void PendingIccbbaVersion_IsHardStop()
    {
        var result = IsbtLicensedCatalogImportRule.Evaluate(true, "US-PUBLIC-SUBSET-PENDING-ICCBBA", 1, 0);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(IsbtLicensedCatalogImportRule.VersionCode, result.Code);
    }

    [Fact]
    public void EmptyPayload_IsHardStop()
    {
        var result = IsbtLicensedCatalogImportRule.Evaluate(true, "ICCBBA-ST-1", 0, 0);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(IsbtLicensedCatalogImportRule.EmptyCode, result.Code);
    }

    [Fact]
    public void LicensedPayload_Passes()
    {
        var result = IsbtLicensedCatalogImportRule.Evaluate(true, "ICCBBA-ST-1", 1, 0);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
