using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// OCD-040: a unit's appearance at issue, ward receipt, and receipt into inventory is what a person saw on the bag.
/// Unrecorded appearance is refused where inspection is required, unless the facility treats an unrecorded
/// appearance as acceptable (<see cref="FacilityPolicyKeys.InspectionDefaultAcceptable"/>, default off). A resolved
/// default is reported so callers can record that nobody chose it.
/// </summary>
public static class AppearanceAttestationRule
{
    public const string Code = "APPEAR-REQUIRED";

    public sealed record Resolved(RuleResult Result, UnitAppearance Appearance, bool UsedDefault);

    /// <param name="step">Where the inspection happens, used in the message (for example "issuing the unit").</param>
    /// <param name="inspectionRequired">False only where another facility policy already makes inspection optional.</param>
    public static Resolved Resolve(UnitAppearance? appearance, bool facilityDefaultAcceptable, string step, bool inspectionRequired = true)
    {
        if (appearance is UnitAppearance recorded)
        {
            return new Resolved(RuleResult.Pass(Code), recorded, false);
        }

        if (inspectionRequired && !facilityDefaultAcceptable)
        {
            return new Resolved(
                RuleResult.HardStop(Code, $"Look at the bag and record its appearance before {step}."),
                UnitAppearance.Acceptable,
                false);
        }

        return new Resolved(RuleResult.Pass(Code), UnitAppearance.Acceptable, true);
    }
}
