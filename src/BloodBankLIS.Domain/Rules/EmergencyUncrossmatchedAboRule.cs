using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// SafeTrace / SoftBank uncrossmatched issue policy: emergency and MTP red cells
/// should be group O, and RhD-negative for women of childbearing potential when
/// the recipient is not already known Rh-positive (AABB 5.27 / CAP TRM.40770).
/// </summary>
public static class EmergencyUncrossmatchedAboRule
{
    public const string AboCode = "ISS-EMERG-ABO";
    public const string RhCode = "ISS-EMERG-RH";

    /// <summary>
    /// Emergency ABO decision (product owner, 2026-11-01): red cells that conflict with an established patient ABO
    /// group are a hard stop even at emergency or MTP issue. Group O red cells never conflict, so emergency
    /// transfusion is never blocked by this rule.
    /// </summary>
    public const string EstablishedConflictCode = "ISS-EMERG-ABO-CONFLICT";

    public static IReadOnlyList<RuleResult> Evaluate(
        bool isEmergencyRelease,
        ComponentClass componentClass,
        AboRh unit,
        AboRh patient,
        Sex patientSex,
        int? patientAgeYears,
        bool requireGroupO,
        bool requireONegForChildbearing,
        int childbearingAgeYears)
    {
        var cellular = componentClass is ComponentClass.RedBloodCells or ComponentClass.WholeBlood;
        if (!isEmergencyRelease || !cellular)
        {
            return [RuleResult.Pass(AboCode), RuleResult.Pass(RhCode)];
        }

        var results = new List<RuleResult>(2);

        if (requireGroupO && unit.Abo != AboGroup.O)
        {
            results.Add(RuleResult.Warning(
                AboCode,
                "Uncrossmatched red cells and whole blood should be group O until the recipient type is established and a compatible crossmatch is complete."));
        }
        else
        {
            results.Add(RuleResult.Pass(AboCode, "Uncrossmatched unit is group O."));
        }

        if (requireONegForChildbearing
            && unit.Rh != RhType.Negative
            && patient.Rh != RhType.Positive
            && IsChildbearingPotential(patientSex, patientAgeYears, childbearingAgeYears))
        {
            results.Add(RuleResult.Warning(
                RhCode,
                "Uncrossmatched red cells for a recipient of childbearing potential should be RhD-negative unless the patient is known Rh-positive."));
        }
        else
        {
            results.Add(RuleResult.Pass(RhCode));
        }

        return results;
    }

    /// <summary>
    /// Red-cell direction only: a donor A or B antigen the recipient lacks. Whole blood is judged on its red cells,
    /// so a plasma-direction mismatch (for example group O whole blood for a group A recipient) stays an overridable
    /// warning from the ABO compatibility rule. Non-emergency issue is unaffected; the ordinary ABO hard stop applies.
    /// </summary>
    public static RuleResult EvaluateEstablishedConflict(
        bool isEmergencyRelease,
        ComponentClass componentClass,
        AboRh unit,
        AboRh patient,
        bool patientTypeEstablished)
    {
        var cellular = componentClass is ComponentClass.RedBloodCells or ComponentClass.WholeBlood;
        if (!isEmergencyRelease || !cellular || !patientTypeEstablished
            || patient.Abo == AboGroup.Unknown || unit.Abo == AboGroup.Unknown)
        {
            return RuleResult.Pass(EstablishedConflictCode);
        }

        var conflict = (HasA(unit.Abo) && !HasA(patient.Abo)) || (HasB(unit.Abo) && !HasB(patient.Abo));
        return conflict
            ? RuleResult.HardStop(
                EstablishedConflictCode,
                $"The patient is group {patient.Abo}. Group {unit.Abo} red cells are ABO-incompatible and cannot be issued, even for emergency release. Choose group O or another ABO-compatible unit.")
            : RuleResult.Pass(EstablishedConflictCode);
    }

    private static bool HasA(AboGroup g) => g is AboGroup.A or AboGroup.AB;

    private static bool HasB(AboGroup g) => g is AboGroup.B or AboGroup.AB;

    public static bool IsChildbearingPotential(Sex sex, int? ageYears, int childbearingAgeYears)
    {
        if (sex == Sex.Male)
        {
            return false;
        }

        if (ageYears is int age && age > childbearingAgeYears)
        {
            return false;
        }

        return true;
    }
}
