namespace BloodBankLIS.Application.Compliance;

/// <summary>One queue in a shift handoff: what it is, how many, anything needing attention, and where to work it.</summary>
public sealed record ShiftHandoffItem(string Label, int Count, string Href, bool Urgent, string? Detail = null);

/// <summary>
/// The handoff as a list the next shift can click through: only queues with something in them, the safety items first
/// (and marked urgent), each linking to the board where it is worked. The same counts as the summary line.
/// </summary>
public static class ShiftHandoffItems
{
    public static IReadOnlyList<ShiftHandoffItem> For(ShiftHandoffDto h)
    {
        var items = new List<ShiftHandoffItem>();

        void Add(int count, string label, string href, bool urgent = false, string? detail = null)
        {
            if (count > 0)
            {
                items.Add(new ShiftHandoffItem(label, count, href, urgent, detail));
            }
        }

        static string? Note(params (int Count, string Text)[] parts)
        {
            var text = string.Join(", ", parts.Where(p => p.Count > 0).Select(p => $"{p.Count} {p.Text}"));
            return text.Length == 0 ? null : text;
        }

        // An idle protocol (nothing issued for the reminder minutes, OCD-076) is named so the next shift closes it if the bleeding is controlled.
        Add(h.ActiveMtp, "Massive transfusion active", "/issuing", urgent: true, Note((h.ActiveMtpIdle, "idle, to close")));
        Add(h.RetrospectiveIncompatible, "Incompatible after release", "/issuing?desk=retro", urgent: true);
        Add(h.CriticalDeviations, "Critical deviation open", "/deviations", urgent: true);
        Add(h.ReagentLotsFailedQc, "Reagent QC failed", "/reagents", urgent: true,
            Note((h.ReagentResultsToReview, h.ReagentResultsToReview == 1 ? "result to review" : "results to review")));
        Add(h.TransfusionsRunning, "Transfusing", "/issuing?desk=issued",
            urgent: h.TransfusionsCompletionOverdue > 0, detail: Note((h.TransfusionsCompletionOverdue, "completion overdue")));
        Add(h.RetrospectiveDue, "Retrospective crossmatch", "/issuing?desk=retro",
            urgent: h.RetrospectiveOverdue > 0, detail: Note((h.RetrospectiveOverdue, "overdue")));
        Add(h.OutstandingIssued, "Outstanding issued", "/issuing?desk=issued");
        Add(h.InTransit, "In transit", "/issuing?desk=transit",
            detail: Note((h.InTransitOverdue, "overdue"), (h.CoolersNotChecked, "cooler not checked"), (h.CoolersWithDifferences, "cooler with differences")));
        Add(h.ReservationsLapsed, "Past reservation", "/issuing?desk=ready");
        Add(h.PendingTests, "Pending tests", "/test-worklist",
            detail: Note((h.ResultsAwaitingVerification, "to verify"), (h.PendingTestsVerifyBeforeExpiry, "to verify before specimen expiry")));
        Add(h.PendingSpecimens, "Pending specimens", "/test-worklist?tab=specimens");
        // A reaction under investigation while the patient still has units out or reserved: urgent, so the next shift
        // checks with the physician before another unit goes (OCD-078).
        Add(h.OpenReactions, "Reaction investigations", "/reactions",
            urgent: h.FatalityFollowUpDue > 0 || h.ReactionsWithUnitsOut > 0,
            detail: Note((h.FatalityFollowUpDue, "fatality follow-up"), (h.ReactionsWithUnitsOut, "with units out or reserved")));
        Add(h.ExpiredOnShelf, "Expired on shelf", "/inventory/discard-expired", urgent: true, detail: "to discard");
        Add(h.NearExpiryUnits, "Near expiry", "/inventory?board=near", detail: Note((h.NearExpiryReserved, "reserved")));
        Add(h.ProductsBelowMinimum, "Below minimum stock", "/inventory/minimum-stock");
        Add(h.OpenDeviations, "Open deviations", "/deviations");
        Add(h.InstrumentFailuresOpen, "Instrument failures", "/instruments?show=open",
            detail: Note((h.InstrumentResultsHeld, "held for review")));
        Add(h.Hl7ErrorsOpen, "HL7 errors", "/hl7");
        // Results and charges that have not gone to the EHR or billing and will not by themselves (Cycle 661).
        Add(h.Hl7OutboundNotSent, "HL7 outbound not sent", "/hl7");
        // An outage left open after recovery makes every later paper time look inside it (Cycle 663).
        Add(h.OutagesOpen, "Outage still open", "/downtime", urgent: true, detail: "end it when the system is back");
        // Cycle 679: the home queues that carry over a shift. Every unit on the disposition board is already overdue.
        Add(h.DispositionOverdue, "Final disposition overdue", "/issuing?desk=disposition", urgent: true, detail: "record the outcome");
        Add(h.OutsideAntibodiesToConfirm, "Outside antibodies to confirm", "/antibody-id#outside-reports");
        Add(h.UnmappedInterfaceValues, "Unmapped interface values", "/admin/hl7/translations?tab=unmapped");
        Add(h.RhigOpen, "RhIG evaluations", "/rhig", detail: Note((h.RhigToGive, "to give")));
        Add(h.AntibodyWorkupsOpen, "Antibody workups", "/antibody-id", detail: Note((h.AntibodyWorkupsToReview, "to review")));
        // A chart still unidentified splits the patient's history until it is merged into the identified chart.
        Add(h.UnidentifiedPatients, "Unidentified patients", "/patients", urgent: true, detail: "to merge once identified");
        Add(h.TubesDueForDiscard, "Tubes due for discard", "/specimens/store");
        // Blood the next shift must still crossmatch and reserve; urgent when a needed-by time has passed or is within the
        // facility's warning window (OCD-075).
        Add(h.BloodOrdersToPrepare, "Blood orders to prepare", "/blood-orders",
            urgent: h.BloodOrdersPastNeededBy > 0 || h.BloodOrdersNeededSoon > 0,
            detail: Note((h.BloodOrdersPastNeededBy, "past needed-by"), (h.BloodOrdersNeededSoon, "needed soon")));

        return items.OrderBy(i => i.Urgent ? 0 : 1).ToList();
    }
}
