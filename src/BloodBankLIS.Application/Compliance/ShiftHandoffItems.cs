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

        Add(h.ActiveMtp, "Massive transfusion active", "/issuing", urgent: true);
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
        Add(h.OpenReactions, "Reaction investigations", "/reactions",
            urgent: h.FatalityFollowUpDue > 0, detail: Note((h.FatalityFollowUpDue, "fatality follow-up")));
        Add(h.ExpiredOnShelf, "Expired on shelf", "/inventory/discard-expired", urgent: true, detail: "to discard");
        Add(h.NearExpiryUnits, "Near expiry", "/inventory?board=near", detail: Note((h.NearExpiryReserved, "reserved")));
        Add(h.ProductsBelowMinimum, "Below minimum stock", "/inventory/minimum-stock");
        Add(h.OpenDeviations, "Open deviations", "/deviations");
        Add(h.InstrumentFailuresOpen, "Instrument failures", "/instruments?show=open",
            detail: Note((h.InstrumentResultsHeld, "held for review")));
        Add(h.Hl7ErrorsOpen, "HL7 errors", "/hl7");
        Add(h.UnmappedInterfaceValues, "Unmapped interface values", "/admin/hl7/translations?tab=unmapped");
        Add(h.RhigOpen, "RhIG evaluations", "/rhig", detail: Note((h.RhigToGive, "to give")));
        // A chart still unidentified splits the patient's history until it is merged into the identified chart.
        Add(h.UnidentifiedPatients, "Unidentified patients", "/patients", urgent: true, detail: "to merge once identified");
        Add(h.TubesDueForDiscard, "Tubes due for discard", "/specimens/store");
        // Blood the next shift must still crossmatch and reserve; a passed needed-by time is urgent.
        Add(h.BloodOrdersToPrepare, "Blood orders to prepare", "/blood-orders",
            urgent: h.BloodOrdersPastNeededBy > 0, detail: Note((h.BloodOrdersPastNeededBy, "past needed-by")));

        return items.OrderBy(i => i.Urgent ? 0 : 1).ToList();
    }
}
