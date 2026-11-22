using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Reserving red cells or plasma for a patient with no current ABO/Rh (end-user testing pass 154: the ABO check ran only
/// when the patient had a type, so a group B red cell could be reserved for an untyped newborn and held for them until
/// issue refused it). The same groups the pre-specimen emergency rule allows (OCD-053) are the ones that suit any
/// recipient: group O red cells or whole blood, group AB or A plasma. Any other group of these is refused until the
/// patient's type is recorded. Other components are not affected.
/// </summary>
public static class UntypedPatientAllocationRule
{
    public const string Code = "ALLOC-UNTYPED";

    public static RuleResult Evaluate(ComponentClass componentClass, AboGroup unitAbo) => componentClass switch
    {
        ComponentClass.RedBloodCells or ComponentClass.WholeBlood when unitAbo != AboGroup.O =>
            RuleResult.HardStop(Code, $"The patient has no ABO/Rh on file, so only group O red cells can be reserved. This unit is group {unitAbo}. Record the patient's type first."),
        ComponentClass.Plasma when unitAbo is not (AboGroup.AB or AboGroup.A) =>
            RuleResult.HardStop(Code, $"The patient has no ABO/Rh on file, so only group AB or A plasma can be reserved. This unit is group {unitAbo}. Record the patient's type first."),
        _ => RuleResult.Pass(Code)
    };
}
