using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

public static class OrderActionValidator
{
    public const string ProductOrderRequiredCode = "ORDACT.PRODUCT.REQUIRED";
    public const string ActionRequiredCode = "ORDACT.ACTION.REQUIRED";
    public const string ActionInactiveCode = "ORDACT.ACTION.INACTIVE";
    public const string CategoryInvalidCode = "ORDACT.CATEGORY.INVALID";
    public const string TypeInvalidCode = "ORDACT.TYPE.INVALID";
    public const string DuplicatePendingCode = "ORDACT.DUPLICATE";
    public const string RemoveNotPendingCode = "ORDACT.REMOVE.NOT-PENDING";

    public static bool IsOrderAttachable(ModificationType? type) =>
        type is ModificationType.Irradiate
            or ModificationType.Wash
            or ModificationType.Thaw
            or ModificationType.VolumeReduction
            or ModificationType.Leukoreduction;

    public static RuleEvaluation ValidateAttach(
        bool hasProductLine,
        ActionDefinition? action,
        bool duplicatePending)
    {
        var results = new List<RuleResult>();

        if (!hasProductLine)
        {
            results.Add(RuleResult.HardStop(ProductOrderRequiredCode,
                "Actions can only be added to an order that has a product line."));
        }

        if (action is null)
        {
            results.Add(RuleResult.HardStop(ActionRequiredCode, "Action was not found."));
            return new RuleEvaluation(results);
        }

        if (!action.IsActive)
        {
            results.Add(RuleResult.HardStop(ActionInactiveCode, $"Action '{action.Code}' is not active."));
        }

        if (action.Category == ActionCategory.Modification)
        {
            if (!IsOrderAttachable(action.ModificationType))
            {
                results.Add(RuleResult.HardStop(TypeInvalidCode,
                    "Only irradiate, wash, thaw, volume reduction, and leukoreduction can be added to a product order."));
            }
        }
        else if (action.Category != ActionCategory.UnitAntigenTyping)
        {
            results.Add(RuleResult.HardStop(CategoryInvalidCode,
                $"Action '{action.Code}' cannot be added to a product order."));
        }

        if (duplicatePending)
        {
            results.Add(RuleResult.HardStop(DuplicatePendingCode,
                $"This order already has a pending '{action.Code}' action."));
        }

        return new RuleEvaluation(results);
    }

    public static RuleEvaluation ValidateRemove(OrderActionStatus status)
    {
        return status == OrderActionStatus.Pending
            ? new RuleEvaluation([RuleResult.Pass(RemoveNotPendingCode)])
            : new RuleEvaluation([RuleResult.HardStop(RemoveNotPendingCode,
                "Only a pending action can be removed from an order.")]);
    }
}
