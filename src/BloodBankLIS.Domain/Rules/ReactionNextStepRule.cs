using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// The one thing to do next on a reaction investigation, so the board can be worked top down instead of read across
/// every column. Most urgent first: a fatality report that is due, a remainder that is not held, an ABO/Rh
/// discrepancy, then the workup in the order the completeness rule checks it, then closing. Nothing here decides a
/// clinical outcome; each step names work the investigation already requires.
/// </summary>
public static class ReactionNextStepRule
{
    public sealed record Facts(
        ReactionInvestigationStatus Status,
        bool FatalityFollowUpDue,
        string? FatalityFollowUpDetail,
        bool RemainderQuarantined,
        string? RemainderHoldNote,
        bool AboRhDiscrepancy,
        bool WorkupIncomplete,
        string? WorkupHoldReason);

    public sealed record Step(int Priority, string Text);

    public const int Done = 99;

    public static Step For(Facts f)
    {
        // A fatality report stays due after closing, so it leads even for a closed case.
        if (f.FatalityFollowUpDue)
        {
            return new Step(1, f.FatalityFollowUpDetail ?? "Fatality reporting is due.");
        }

        if (f.Status == ReactionInvestigationStatus.Closed)
        {
            return new Step(Done, "Closed.");
        }

        if (!f.RemainderQuarantined && string.IsNullOrWhiteSpace(f.RemainderHoldNote))
        {
            return new Step(2, "Hold the remainder of the unit.");
        }

        if (f.AboRhDiscrepancy)
        {
            return new Step(3, "Review the ABO/Rh discrepancy.");
        }

        if (f.WorkupIncomplete)
        {
            return new Step(4, f.WorkupHoldReason ?? "Complete the workup.");
        }

        return new Step(5, "Ready to close: review and sign.");
    }
}
