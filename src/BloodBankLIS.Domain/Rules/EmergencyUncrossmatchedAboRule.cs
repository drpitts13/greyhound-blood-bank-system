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
