using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// AABB computer-crossmatch requires two concordant ABO/Rh determinations, at least
/// one of which is current. Historical rows that match the current type satisfy this,
/// unless they were tested on the same specimen as the current type: a second test of
/// the same tube cannot catch a tube drawn from the wrong patient.
/// </summary>
public static class SecondAboDeterminationRule
{
    public const string Code = ElectronicCrossmatchEligibilityRule.Code;
    public const string IssueCode = "ISS-SECOND-ABO";

    /// <param name="SpecimenId">The specimen tested, when known; null for manual or historical types.</param>
    public sealed record Determination(AboRh BloodType, bool IsCurrent, long? SpecimenId = null);

    /// <summary>Determinations from stored history, leaving out types whose source result was invalidated.</summary>
    public static IReadOnlyList<Determination> FromHistory(IEnumerable<PatientBloodTypeHistory> rows) =>
        rows.Where(r => r.InvalidatedUtc is null).Select(r => new Determination(r.BloodType, r.IsCurrent, r.SpecimenId)).ToList();

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
            && h.BloodType == current.BloodType
            && !SameSpecimen(h, current));
    }

    /// <summary>Both determinations are known to come from the same tube.</summary>
    public static bool SameSpecimen(Determination a, Determination b) =>
        a.SpecimenId is long first && b.SpecimenId is long second && first == second;

    /// <summary>
    /// Chart notice when routine red-cell issue would hard-stop for a missing
    /// second concordant type. No notice when the facility does not require it,
    /// when the second type is on file, or when there is no current type.
    /// </summary>
    public static string? MissingCellularIssueNotice(bool required, IReadOnlyList<Determination> history)
    {
        ArgumentNullException.ThrowIfNull(history);
        if (!required || HasSecondConcordant(history))
        {
            return null;
        }

        var current = history.FirstOrDefault(h => h.IsCurrent && h.BloodType.IsKnown);
        if (current is null)
        {
            return null;
        }

        return "Red cell and whole-blood issue needs a second concordant ABO/Rh from a separate specimen.";
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
            "Red cell and whole-blood issue requires two concordant ABO/Rh determinations from separate specimens (current plus historical or second sample).");
    }
}
