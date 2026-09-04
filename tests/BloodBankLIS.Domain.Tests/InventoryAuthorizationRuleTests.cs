using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class InventoryAuthorizationRuleTests
{
    [Fact]
    public void QuarantineRelease_WithoutPermission_IsHardStop()
    {
        var result = InventoryAuthorizationRule.EvaluateQuarantineRelease(hasInventoryRelease: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(InventoryAuthorizationRule.QuarantineReleaseCode, result.Code);
    }

    [Fact]
    public void QuarantineRelease_WithPermission_Passes()
    {
        var result = InventoryAuthorizationRule.EvaluateQuarantineRelease(hasInventoryRelease: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void DirectedConversion_WithoutPermission_IsHardStop()
    {
        var result = InventoryAuthorizationRule.EvaluateDirectedConversion(hasInventoryRelease: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(InventoryAuthorizationRule.DirectedConversionCode, result.Code);
    }

    [Fact]
    public void DirectedConversion_WithPermission_Passes()
    {
        var result = InventoryAuthorizationRule.EvaluateDirectedConversion(hasInventoryRelease: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void HoldRelease_WithoutPermission_IsHardStop()
    {
        var result = InventoryAuthorizationRule.EvaluateHoldRelease(hasInventoryRelease: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(InventoryAuthorizationRule.HoldReleaseCode, result.Code);
    }

    [Fact]
    public void HoldRelease_WithPermission_Passes()
    {
        var result = InventoryAuthorizationRule.EvaluateHoldRelease(hasInventoryRelease: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Modify_WithoutPermission_IsHardStop()
    {
        var result = InventoryAuthorizationRule.EvaluateModify(hasInventoryModify: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(InventoryAuthorizationRule.ModifyCode, result.Code);
    }

    [Fact]
    public void Modify_WithPermission_Passes()
    {
        var result = InventoryAuthorizationRule.EvaluateModify(hasInventoryModify: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void CorrectIdentity_WithoutPermission_IsHardStop()
    {
        var result = InventoryAuthorizationRule.EvaluateCorrectIdentity(hasInventoryCorrectIdentity: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(InventoryAuthorizationRule.CorrectIdentityCode, result.Code);
    }

    [Fact]
    public void CorrectIdentity_WithPermission_Passes()
    {
        var result = InventoryAuthorizationRule.EvaluateCorrectIdentity(hasInventoryCorrectIdentity: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Receive_WithoutPermission_IsHardStop()
    {
        var result = InventoryAuthorizationRule.EvaluateReceive(hasInventoryReceive: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(InventoryAuthorizationRule.ReceiveCode, result.Code);
    }

    [Fact]
    public void Receive_WithPermission_Passes()
    {
        var result = InventoryAuthorizationRule.EvaluateReceive(hasInventoryReceive: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Discard_WithoutPermission_IsHardStop()
    {
        var result = InventoryAuthorizationRule.EvaluateDiscard(hasInventoryDiscard: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(InventoryAuthorizationRule.DiscardCode, result.Code);
    }

    [Fact]
    public void Discard_WithPermission_Passes()
    {
        var result = InventoryAuthorizationRule.EvaluateDiscard(hasInventoryDiscard: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Transfer_WithoutPermission_IsHardStop()
    {
        var result = InventoryAuthorizationRule.EvaluateTransfer(hasInventoryTransfer: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(InventoryAuthorizationRule.TransferCode, result.Code);
    }

    [Fact]
    public void Transfer_WithPermission_Passes()
    {
        var result = InventoryAuthorizationRule.EvaluateTransfer(hasInventoryTransfer: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Recall_WithoutPermission_IsHardStop()
    {
        var result = InventoryAuthorizationRule.EvaluateRecall(hasInventoryRecall: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(InventoryAuthorizationRule.RecallCode, result.Code);
    }

    [Fact]
    public void Recall_WithPermission_Passes()
    {
        var result = InventoryAuthorizationRule.EvaluateRecall(hasInventoryRecall: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void SaveAttribute_WithoutPermission_IsHardStop()
    {
        var result = InventoryAuthorizationRule.EvaluateSaveAttribute(hasInventoryReceive: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(InventoryAuthorizationRule.SaveAttributeCode, result.Code);
    }

    [Fact]
    public void SaveAttribute_WithPermission_Passes()
    {
        var result = InventoryAuthorizationRule.EvaluateSaveAttribute(hasInventoryReceive: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void ReturnToSupplier_WithoutPermission_IsHardStop()
    {
        var result = InventoryAuthorizationRule.EvaluateReturnToSupplier(hasInventoryReceive: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(InventoryAuthorizationRule.ReturnToSupplierCode, result.Code);
    }

    [Fact]
    public void ReturnToSupplier_WithPermission_Passes()
    {
        var result = InventoryAuthorizationRule.EvaluateReturnToSupplier(hasInventoryReceive: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void LocateMissing_WithoutPermission_IsHardStop()
    {
        var result = InventoryAuthorizationRule.EvaluateLocateMissing(hasInventoryRelease: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(InventoryAuthorizationRule.LocateMissingCode, result.Code);
    }

    [Fact]
    public void LocateMissing_WithPermission_Passes()
    {
        var result = InventoryAuthorizationRule.EvaluateLocateMissing(hasInventoryRelease: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void InspectDamaged_WithoutPermission_IsHardStop()
    {
        var result = InventoryAuthorizationRule.EvaluateInspectDamaged(hasInventoryRelease: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(InventoryAuthorizationRule.InspectDamagedCode, result.Code);
    }

    [Fact]
    public void InspectDamaged_WithPermission_Passes()
    {
        var result = InventoryAuthorizationRule.EvaluateInspectDamaged(hasInventoryRelease: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
