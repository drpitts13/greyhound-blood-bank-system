using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Whether a quarantined unit may go back to stock, given why it is held. Each stop follows a gate or request the
/// system already has: an expired unit cannot be issued; the issue gate already refuses a unit whose verified retype
/// disagrees with its label; a reaction remainder is the evidence for an open investigation; and a unit returned with
/// disposal requested is waiting to be discarded. Every other reason is released with the usual second-verifier check.
/// </summary>
public static class QuarantineReleaseRule
{
    public const string Code = "QREL-REASON";

    public sealed record Decision(bool CanRelease, string NextStep, RuleResult Result);

    public static Decision Evaluate(
        UnitQuarantineReason reason,
        bool openReactionInvestigation,
        bool verifiedRetypeMismatch,
        bool expired)
    {
        if (expired)
        {
            return Stop("Expired: discard it.", "This unit has expired, so it cannot go back to stock. Discard it.");
        }

        if (verifiedRetypeMismatch)
        {
            return Stop("Retype disagrees with the label: discard or return it to the supplier.",
                "The verified ABO/Rh retype disagrees with the label, so the unit cannot be issued. Discard it or return it to the supplier.");
        }

        if (reason == UnitQuarantineReason.ReactionRemainder && openReactionInvestigation)
        {
            return Stop("Keep held until the reaction investigation is closed.",
                "This is the remainder of a unit in an open reaction investigation. Close the investigation before releasing it.");
        }

        if (reason == UnitQuarantineReason.ReturnedForDisposal)
        {
            return Stop("Disposal was requested: discard it.",
                "This unit was returned with disposal requested. Discard it instead of releasing it.");
        }

        return new Decision(true, "Review, then release with a second verifier, or discard.", RuleResult.Pass(Code));
    }

    private static Decision Stop(string nextStep, string message) =>
        new(false, nextStep, RuleResult.HardStop(Code, message));
}
