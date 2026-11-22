using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// OCD-053 (decided 2026-11-03): emergency or massive-transfusion release may go ahead before a specimen is
/// drawn, with controls. Without a current specimen (none on file, or the one on file has expired) only group O
/// red cells or whole blood, and group AB or A plasma, may be released, as an overridable warning that the
/// emergency override (permission, reason, authorizer) must accept. Everything else stays a hard stop. Routine
/// issue is unchanged: no specimen is still a hard stop.
/// </summary>
public static class EmergencyPreSpecimenRule
{
    public const string Code = "ISS-PRE-SPECIMEN";

    public const string CollectNow = "Collect a pre-transfusion specimen now.";

    /// <summary>Whether the rule replaces the specimen checks: an emergency issue with no usable specimen.</summary>
    public static bool Applies(bool isEmergencyRelease, bool specimenExists, DateTime? specimenExpiresUtc, DateTime nowUtc) =>
        isEmergencyRelease
        && (!specimenExists || (specimenExpiresUtc is DateTime expires && nowUtc >= expires));

    public static RuleResult Evaluate(ComponentClass componentClass, AboRh unit)
    {
        switch (componentClass)
        {
            case ComponentClass.RedBloodCells or ComponentClass.WholeBlood:
                return unit.Abo == AboGroup.O
                    ? RuleResult.Warning(Code, $"No current specimen. Group O red cells may be released as an emergency with an authorized override. {CollectNow}")
                    : RuleResult.HardStop(Code, $"No current specimen, so only group O red cells can be released. This unit is group {unit.Abo}.");
            case ComponentClass.Plasma:
                return unit.Abo is AboGroup.AB or AboGroup.A
                    ? RuleResult.Warning(Code, $"No current specimen. Group {unit.Abo} plasma may be released as an emergency with an authorized override. {CollectNow}")
                    : RuleResult.HardStop(Code, $"No current specimen, so only group AB or A plasma can be released. This unit is group {unit.Abo}.");
            default:
                return RuleResult.HardStop(Code, $"No current specimen. Before a specimen is drawn only group O red cells and group AB or A plasma can be released, not {componentClass}.");
        }
    }
}
