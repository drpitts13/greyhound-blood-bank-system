using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ActionCatalogValidatorTests
{
    [Fact]
    public void ActionDefinition_RejectsMissingCode()
    {
        var entity = new ActionDefinition { Name = "Irradiate", Category = ActionCategory.Modification, ModificationType = ModificationType.Irradiate };
        var result = ActionDefinitionValidator.Validate(entity, duplicateCode: false);
        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, r => r.Code == ActionDefinitionValidator.CodeRequired);
    }

    [Fact]
    public void ActionDefinition_RejectsDivide()
    {
        var entity = new ActionDefinition
        {
            Code = "DIV",
            Name = "Divide",
            Category = ActionCategory.Modification,
            ModificationType = ModificationType.Divide
        };
        var result = ActionDefinitionValidator.Validate(entity, duplicateCode: false);
        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, r => r.Code == ActionDefinitionValidator.ModificationTypeInvalid);
    }

    [Fact]
    public void ActionDefinition_AcceptsIrradiate()
    {
        var entity = new ActionDefinition
        {
            Code = "IRRAD",
            Name = "Irradiate",
            Category = ActionCategory.Modification,
            ModificationType = ModificationType.Irradiate
        };
        var result = ActionDefinitionValidator.Validate(entity, duplicateCode: false);
        Assert.False(result.IsHardStopped);
    }

    [Fact]
    public void ActionBilling_RejectsWrongTrigger()
    {
        var row = new ActionBilling
        {
            ActionDefinitionId = 1,
            ChargeCodeId = 1,
            Trigger = BillingTriggerType.UnitIssued
        };
        var result = ActionBillingValidator.Validate(row, chargeCodeMissing: false, actionMissing: false, duplicateActive: false);
        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, r => r.Code == ActionBillingValidator.TriggerInvalid);
    }

    [Fact]
    public void ActionBilling_AcceptsValid()
    {
        var row = new ActionBilling
        {
            ActionDefinitionId = 1,
            ChargeCodeId = 1,
            Trigger = BillingTriggerType.ActionCompleted
        };
        var result = ActionBillingValidator.Validate(row, chargeCodeMissing: false, actionMissing: false, duplicateActive: false);
        Assert.False(result.IsHardStopped);
    }

    [Fact]
    public void OrderAction_RejectsTestOnlyOrder()
    {
        var action = new ActionDefinition
        {
            Code = "IRRAD",
            Name = "Irradiate",
            Category = ActionCategory.Modification,
            ModificationType = ModificationType.Irradiate,
            IsActive = true
        };
        var result = OrderActionValidator.ValidateAttach(hasProductLine: false, action, duplicatePending: false);
        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, r => r.Code == OrderActionValidator.ProductOrderRequiredCode);
    }

    [Fact]
    public void OrderAction_RejectsDuplicatePending()
    {
        var action = new ActionDefinition
        {
            Code = "IRRAD",
            Name = "Irradiate",
            Category = ActionCategory.Modification,
            ModificationType = ModificationType.Irradiate,
            IsActive = true
        };
        var result = OrderActionValidator.ValidateAttach(hasProductLine: true, action, duplicatePending: true);
        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, r => r.Code == OrderActionValidator.DuplicatePendingCode);
    }

    [Fact]
    public void OrderAction_RemoveCompleted_IsHardStop()
    {
        var result = OrderActionValidator.ValidateRemove(OrderActionStatus.Completed);
        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, r => r.Code == OrderActionValidator.RemoveNotPendingCode);
    }
}
