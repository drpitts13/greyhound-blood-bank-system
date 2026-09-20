using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

public static class ActionDefinitionValidator
{
    public const string CodeRequired = "ACTION.CODE.REQUIRED";
    public const string NameRequired = "ACTION.NAME.REQUIRED";
    public const string CodeDuplicate = "ACTION.CODE.DUPLICATE";
    public const string ModificationTypeRequired = "ACTION.MODTYPE.REQUIRED";
    public const string ModificationTypeInvalid = "ACTION.MODTYPE.INVALID";

    public static RuleEvaluation Validate(ActionDefinition entity, bool duplicateCode)
    {
        var results = new List<RuleResult>();

        if (string.IsNullOrWhiteSpace(entity.Code))
        {
            results.Add(RuleResult.HardStop(CodeRequired, "Action code is required."));
        }

        if (string.IsNullOrWhiteSpace(entity.Name))
        {
            results.Add(RuleResult.HardStop(NameRequired, "Action name is required."));
        }

        if (duplicateCode)
        {
            results.Add(RuleResult.HardStop(CodeDuplicate, $"Another action already uses '{entity.Code}'."));
        }

        if (entity.Category == ActionCategory.Modification)
        {
            if (entity.ModificationType is null)
            {
                results.Add(RuleResult.HardStop(ModificationTypeRequired,
                    "A modification action must specify a 1:1 modification type."));
            }
            else if (!OrderActionValidator.IsOrderAttachable(entity.ModificationType))
            {
                results.Add(RuleResult.HardStop(ModificationTypeInvalid,
                    "Order-attachable modification actions must be irradiate, wash, thaw, volume reduction, or leukoreduction."));
            }
        }

        return new RuleEvaluation(results);
    }
}
