using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// AABB computer-crossmatch requires two concordant ABO/Rh determinations, at least
/// one of which is current. Historical rows that match the current type satisfy this.
/// </summary>
public static class SecondAboDeterminationRule
{
    public const string Code = ElectronicCrossmatchEligibilityRule.Code;
    public const string IssueCode = "ISS-SECOND-ABO";

    public sealed record Determination(AboRh BloodType, bool IsCurrent);

    public static bool HasSecondConcordant(IReadOnlyList<Determination> history)
    {
        ArgumentNullException.ThrowIfNull(history);

        var current = history.FirstOrDefault(h => h.IsCurrent && h.BloodType.IsKnown);
        if (current is null)
        {
            return false;
        }

        return history.Any(h =>
            !h.IsCurrent
            && h.BloodType.IsKnown
            && h.BloodType == current.BloodType);
    }

    /// <summary>
    /// SafeTrace / SoftBank second-sample (or historical type) check for cellular issue.
    /// Emergency and MTP are overridable; routine RBC/WB issue is a hard stop.
    /// </summary>
    public static RuleResult EvaluateForCellularIssue(
        bool required,
        bool hasSecondConcordant,
        ComponentClass componentClass,
        bool isEmergencyRelease)
    {
        var cellular = componentClass is ComponentClass.RedBloodCells or ComponentClass.WholeBlood;
        if (!required || !cellular)
        {
            return RuleResult.Pass(IssueCode);
        }

        if (hasSecondConcordant)
        {
            return RuleResult.Pass(IssueCode, "Two concordant ABO/Rh determinations are on file.");
        }

        if (isEmergencyRelease)
        {
            return RuleResult.Warning(
                IssueCode,
                "Second ABO/Rh determination is not on file; emergency or MTP issue requires an authorized override.");
        }

        return RuleResult.HardStop(
            IssueCode,
            "Red cell and whole-blood issue requires two concordant ABO/Rh determinations (current plus historical or second sample).");
    }
}
