using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Application.Patients;
using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Compliance;

/// <summary>
/// Which bench queues the dashboard may show. A card is omitted when the
/// signed-in user cannot open that queue. The counts stay on the existing
/// worklists and do not change issue, receipt, result, or investigation gates.
/// </summary>
public static class DashboardQueueCatalog
{
    public sealed record Card(string Key, string Label, string Href);

    public const string Ready = "ready";
    public const string SecondAbo = "second-abo";
    public const string Retrospective = "retrospective";
    public const string InTransit = "in-transit";
    public const string Outstanding = "outstanding";
    public const string Disposition = "disposition";
    public const string PendingTests = "pending-tests";
    public const string ToVerify = "to-verify";
    public const string PendingSpecimens = "pending-specimens";
    public const string Reactions = "reactions";
    public const string Supervisor = "supervisor";
    public const string Handoff = "handoff";
    public const string TemperatureExcursions = "temp-excursions";
    public const string Hl7Errors = "hl7-errors";
    public const string InstrumentFailures = "instrument-failures";
    public const string NearExpiry = "near-expiry";
    public const string ExpiredOnShelf = "expired-on-shelf";
    public const string RecalledToPull = "recalled-to-pull";
    public const string Deviations = "deviations";
    public const string ReagentQc = "reagent-qc";
    public const string LowStock = "low-stock";
    public const string UnmappedInterfaceValues = "interface-unmapped";
    public const string PossibleDuplicates = "possible-duplicates";
    public const string OpenRhig = "open-rhig";
    public const string TubesDueForDiscard = "tubes-due-discard";
    public const string Unidentified = "unidentified-patients";
    public const string BloodOrders = "blood-orders";
    public const string AntibodyWorkups = "antibody-workups";
    public const string Hl7OutboundWaiting = "hl7-outbound";
    public const string OutageOpen = "outage-open";
    public const string OutsideAntibodies = "outside-antibodies";
    public const string SettingsToEnter = "settings-to-enter";
    public const string PanelLotsToReplace = "panel-lots";
    public const string EquipmentChecks = "equipment-checks";

    public static IReadOnlyList<Card> Select(Func<string, bool> has)
    {
        ArgumentNullException.ThrowIfNull(has);
        var cards = new List<Card>();
        var canIssue = has(PermissionCodes.IssueCreate);
        var canIssuedBoard = canIssue
            || has(PermissionCodes.IssueReturn)
            || has(PermissionCodes.TransfusionDocument);

        // Blood orders whose units are not all reserved yet: what to crossmatch and reserve next.
        Add(cards, has(PermissionCodes.CompatibilityAllocate), BloodOrders, "Blood orders to prepare", "/blood-orders");
        Add(cards, canIssue, Ready, "Ready to issue", "/issuing?desk=ready");
        Add(cards, canIssue, SecondAbo, "Second ABO/Rh", "/issuing?desk=second");
        Add(cards, canIssue, Retrospective, "Retrospective crossmatch", "/issuing?desk=retro");
        Add(cards, canIssue, InTransit, "In transit", "/issuing?desk=transit");
        Add(cards, canIssuedBoard, Outstanding, "Outstanding issued", "/issuing?desk=issued");
        Add(cards, canIssuedBoard, Disposition, "Final disposition", "/issuing?desk=disposition");
        Add(cards, has(PermissionCodes.ResultEnter), PendingTests, "Pending tests", "/test-worklist");
        Add(cards, has(PermissionCodes.ResultVerify), ToVerify, "Results to verify", "/test-worklist?show=verify");
        Add(cards, has(PermissionCodes.SpecimenAccession), PendingSpecimens, "Pending specimens", "/test-worklist?tab=specimens");
        // Stored tubes past the facility's retention date (OCD-070): whoever stores tubes pulls and discards them.
        Add(cards, has(PermissionCodes.SpecimenAccession), TubesDueForDiscard, "Tubes due for discard", "/specimens/store");
        // Daily reagent QC is bench work: whoever enters results records and reviews it.
        Add(cards, has(PermissionCodes.ResultEnter), ReagentQc, "Reagent QC", "/reagents");
        // Equipment checks failed, overdue, due today or never done, on equipment in service (Cycle 709): bench work.
        Add(cards, has(PermissionCodes.ResultEnter), EquipmentChecks, "Equipment checks to do", "/equipment");
        Add(cards, has(PermissionCodes.ReactionInvestigate), Reactions, "Reaction investigations", "/reactions");
        // RhIG evaluations not yet completed: recorded by the bench, completed by a verifier (OCD-069).
        Add(cards, has(PermissionCodes.ResultVerify) || has(PermissionCodes.ImmunoRecord), OpenRhig, "Open RhIG evaluations", "/rhig");
        // Antibody workups not completed: read by the bench (immuno.record), reviewed by a supervisor (immuno.override).
        Add(cards, has(PermissionCodes.ImmunoRecord) || has(PermissionCodes.ImmunoOverride), AntibodyWorkups, "Antibody workups", "/antibody-id");
        // Antibodies reported by another facility, used for selection, not yet confirmed here (OCD-073): the bench confirms.
        Add(cards, has(PermissionCodes.ImmunoRecord), OutsideAntibodies, "Outside antibodies to confirm", "/antibody-id#outside-reports");
        // Panel lots expiring with nothing entered to follow them, or none current (Cycle 707): whoever enters lots
        // (admin) acts; the bench sees it so the lot is ordered in time and lands on the worklist note.
        Add(cards, has(PermissionCodes.AdminConfigView) || has(PermissionCodes.ImmunoRecord), PanelLotsToReplace, "Panel lots to replace",
            has(PermissionCodes.AdminConfigView) ? "/admin/antibody-panel-lots" : "/antibody-id");
        Add(cards, has(PermissionCodes.ImmunoOverride)
            || has(PermissionCodes.ResultVerify)
            || has(PermissionCodes.InventoryRelease) || has(PermissionCodes.OverrideApprove), Supervisor, "Supervisor queue", "/supervisor");
        Add(cards, has(PermissionCodes.InventoryReceive)
            || has(PermissionCodes.DeviationManage), TemperatureExcursions, "Temperature excursions", "/storage-temps");
        Add(cards, has(PermissionCodes.InventoryReceive) || has(PermissionCodes.InventoryDiscard)
            || canIssue || has(PermissionCodes.CompatibilityAllocate), NearExpiry, "Near expiry", "/inventory?board=near");
        // Products below their minimum stock level: whoever keeps or uses the stock should see a shortage before it bites.
        Add(cards, has(PermissionCodes.InventoryReceive) || has(PermissionCodes.InventoryDiscard)
            || canIssue || has(PermissionCodes.CompatibilityAllocate), LowStock, "Below minimum stock", "/inventory/minimum-stock");
        // Expired units still recorded on a shelf: pull and discard them (inventory.discard).
        Add(cards, has(PermissionCodes.InventoryDiscard), ExpiredOnShelf, "Expired on shelf", "/inventory/discard-expired");
        // Recalled units not yet pulled (Cycle 688): quarantine or discard them.
        Add(cards, has(PermissionCodes.InventoryDiscard) || has(PermissionCodes.InventoryRelease), RecalledToPull, "Recalled units to pull", "/inventory?board=recalled");
        Add(cards, has(PermissionCodes.InstrumentManage), InstrumentFailures, "Instrument failures", "/instruments?show=open");
        Add(cards, has(PermissionCodes.Hl7Manage), Hl7Errors, "HL7 errors", "/hl7");
        // Results and charges that have not gone out and will not by themselves (Cycle 660): never an error, so not above.
        Add(cards, has(PermissionCodes.Hl7Manage), Hl7OutboundWaiting, "HL7 outbound not sent", "/hl7");
        // Values an interface sent or received that no translation covered: whoever keeps the translation table fixes them.
        Add(cards, has(PermissionCodes.AdminHl7Manage) || has(PermissionCodes.AdminInstrumentsManage), UnmappedInterfaceValues, "Unmapped interface values", "/admin/hl7/translations?tab=unmapped");
        Add(cards, has(PermissionCodes.DeviationManage), Deviations, "Open deviations", "/deviations");
        // Charts that may be one person split the ABO/Rh and antibody history: whoever merges charts reviews them.
        Add(cards, has(PermissionCodes.PatientMerge), PossibleDuplicates, "Possible duplicates", "/patients/duplicates");
        // Emergency charts registered without identification: merged into the identified chart once known.
        Add(cards, has(PermissionCodes.PatientMerge), Unidentified, "Unidentified patients", "/patients");
        Add(cards, has(PermissionCodes.ShiftHandoff), Handoff, "Shift handoff", "/handoff");
        // Settings the medical director decided to use whose values are not entered yet (Cycle 677).
        Add(cards, has(PermissionCodes.AdminConfigEdit), SettingsToEnter, "Facility settings to enter", "/admin/facility-policies?show=to-enter");
        // A recorded outage not yet ended (Cycle 663): while open, every later paper time counts as inside it.
        Add(cards, canIssue, OutageOpen, "Outage still open", "/downtime");
        return cards;
    }

    /// <summary>
    /// Extra line when a stored overdue or follow-up count is already above zero.
    /// Zero stays silent so a routine queue looks like a count alone.
    /// </summary>
    /// <summary>
    /// The Results to verify card: lines this user can verify now (the worklist's To verify view), and of those how
    /// many must be verified before their specimen expires.
    /// </summary>
    public static (int Count, string? Note) ToVerifyCard(IEnumerable<TestWorkItemDto> pending, string? userName)
    {
        var mine = TestWorklistView.ToVerify(pending, userName);
        var dueSoon = PendingTestAttentionRule.CountVerifyBeforeExpiry(mine);
        return (mine.Count, AttentionNote(dueSoon, "before its specimen expires", "before their specimens expire"));
    }

    /// <summary>
    /// The Pending tests card note: results to verify before their specimen expires, and lines whose tube was
    /// rejected and need a new specimen. Either brings the card under Needs attention.
    /// </summary>
    public static string? PendingTestsNote(IEnumerable<TestWorkItemDto> pending)
    {
        var items = pending.ToList();
        var parts = new[]
        {
            AttentionNote(PendingTestAttentionRule.CountVerifyBeforeExpiry(items), "to verify before its specimen expires", "to verify before their specimens expire"),
            AttentionNote(TestWorklistView.Recollect(items).Count, "needs a new specimen", "need a new specimen"),
            AttentionNote(TestWorklistView.RushOrdersWithoutSpecimen(items), "STAT/urgent order waiting for a specimen", "STAT/urgent orders waiting for a specimen")
        }.Where(p => p is not null).ToList();
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    /// <summary>
    /// The Instrument failures card: open failures, and how many of them are results an instrument error code held,
    /// which wait on a review (release or re-run) rather than a fix and replay. Given the time, it also says how long the
    /// oldest open failure has waited: a result that did not file is a patient result not yet reported.
    /// </summary>
    public static (int Count, string? Note) InstrumentFailuresCard(IEnumerable<InstrumentMessageSummaryDto> failures, DateTime? nowUtc = null)
    {
        var open = failures.Where(f => f.OpenFailure).ToList();
        var parts = new[]
        {
            AttentionNote(open.Count(f => f.Held), "held for an instrument error code", "held for instrument error codes"),
            nowUtc is { } now ? OldestWaitingNote(open.Select(f => (DateTime?)f.ReceivedUtc), now) : null
        }.Where(p => p is not null).ToList();
        return (open.Count, parts.Count == 0 ? null : string.Join("; ", parts));
    }

    /// <summary>
    /// The Possible duplicates card note: pairs whose charts carry the same identifier from another system, the
    /// strongest sign that two charts are one person. Name and birthday pairs stay in the count only.
    /// </summary>
    public static string? PossibleDuplicatesNote(IEnumerable<PossibleDuplicatePairDto> pairs) =>
        AttentionNote(
            pairs.Count(p => PossibleDuplicatePatientRule.IsSharedIdentifierReason(p.Reason)),
            "shares an external ID",
            "share an external ID");

    /// <summary>The Blood orders to prepare card note: rush orders, and orders waiting for a specimen.</summary>
    public static string? BloodOrdersNote(IEnumerable<BloodBankLIS.Application.PatientWorkspace.BloodOrderToPrepareDto> rows)
    {
        var list = rows.ToList();
        var parts = new[]
        {
            AttentionNote(list.Count(r => r.Rush), "rush", "rush"),
            AttentionNote(list.Count(r => r.NeededByPassed), "past its needed-by time", "past their needed-by time"),
            AttentionNote(list.Count(r => r.NeededBySoon), "needed soon", "needed soon"),
            AttentionNote(list.Count(r => r.SpecimenExpiresBeforeNeeded), "specimen expires before it is needed", "specimens expire before they are needed"),
            AttentionNote(list.Count(r => !r.HasUsableSpecimen), "needs a specimen", "need a specimen"),
            AttentionNote(list.Count(r => r.ExpiredReservations is { Count: > 0 }), "lost a reserved unit to expiry", "lost reserved units to expiry")
        }.Where(p => p is not null).ToList();
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    /// <summary>The Open RhIG evaluations card note: those filled in and waiting only to be completed.</summary>
    public static string? RhigQueueNote(IEnumerable<BloodBankLIS.Application.Immunohematology.RhigQueueItemDto> items)
    {
        var list = items.ToList();
        var parts = new[]
        {
            AttentionNote(list.Count(i => i.ReadyToComplete), "ready to complete", "ready to complete"),
            AttentionNote(list.Count(i => i.Evaluation.AwaitsGiving), "to give", "to give")
        }.Where(p => p is not null).ToList();
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    /// <summary>
    /// The Antibody workups card note: open workups that need more than their next routine step. Units reserved or
    /// issued for the patient, an interpretation withdrawn by a later change, a pending ABO/Rh or antigen correction, a
    /// rejected, cancelled or expired specimen, or a withdrawn or expired lot. Any of these brings the card under Needs
    /// attention; reactions, interpretation and review to do stay in the count (reviews are on the Supervisor queue).
    /// </summary>
    public static string? AntibodyWorkupsNote(BloodBankLIS.Application.Immunohematology.AntibodyIdOpenWorklistSummaryDto summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        var parts = new[]
        {
            AttentionNote(summary.ReservedOrIssuedUnitCount, "with units reserved or issued", "with units reserved or issued"),
            AttentionNote(summary.WithdrawnJudgmentCount, "to re-interpret", "to re-interpret"),
            AttentionNote(summary.PendingTypeCorrectionCount, "with a pending type correction", "with a pending type correction"),
            AttentionNote(summary.SpecimenAttentionCount, "on a rejected, cancelled or expired specimen", "on a rejected, cancelled or expired specimen"),
            AttentionNote(summary.LotAttentionCount, "on a withdrawn or expired lot", "on a withdrawn or expired lot")
        }.Where(p => p is not null).ToList();
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    /// <summary>
    /// The HL7 outbound card note: how long the oldest stuck message has waited, and how many wait for an interface or
    /// have spent their retries. Any stuck message is worth attention: a result or charge is not reaching its system.
    /// </summary>
    public static string? OutboundWaitingNote(IReadOnlyCollection<(DateTime QueuedUtc, string Reason)> stuck, DateTime nowUtc)
    {
        if (stuck.Count == 0)
        {
            return null;
        }

        var oldest = $"oldest {AlertInboxView.OpenFor(stuck.Min(s => s.QueuedUtc), nowUtc)}";
        var parts = new[]
        {
            oldest,
            AttentionNote(stuck.Count(s => s.Reason == OutboundMessageWaitRule.NoInterface), "with no interface set up", "with no interface set up"),
            AttentionNote(stuck.Count(s => s.Reason.Contains(OutboundMessageWaitRule.RetriesSpent, StringComparison.Ordinal)), "with retries spent", "with retries spent")
        }.Where(p => p is not null).ToList();
        return string.Join("; ", parts);
    }

    /// <summary>
    /// The open-outage card note: how long it has been open and the paper issues back-entered in it, and that it ends
    /// on Downtime Snapshot once the system is back.
    /// </summary>
    public static string? OutageOpenNote(DowntimeEpisodeDto? open, DateTime nowUtc) => open is null
        ? null
        : string.Join("; ", new[]
        {
            $"{open.Description}, open {AlertInboxView.OpenFor(open.StartedUtc, nowUtc)}",
            AttentionNote(open.BackEntries, "paper issue back-entered", "paper issues back-entered"),
            "end it when the system is back"
        }.Where(p => p is not null));

    /// <summary>
    /// The outside-antibodies card note: how long the oldest has waited, and how many have no documented report (recorded
    /// before documentation was required). Waiting is not overdue, so the note informs without urgency.
    /// </summary>
    public static string? OutsideAntibodiesNote(IReadOnlyCollection<OutsideAntibodyToConfirmDto> waiting, DateTime nowUtc)
    {
        if (waiting.Count == 0)
        {
            return null;
        }

        var parts = new[]
        {
            $"oldest {AlertInboxView.OpenFor(waiting.Min(w => w.RecordedUtc), nowUtc)}",
            AttentionNote(waiting.Count(w => !w.Documented), "report not documented", "reports not documented")
        }.Where(p => p is not null);
        return string.Join("; ", parts);
    }

    /// <summary>
    /// Cycle 698: the Recalled units to pull card note: how long the oldest recall has waited, and how many were issued
    /// when recalled (on the ward, returned through Issue &amp; Transfuse rather than scanned on the board).
    /// </summary>
    public static string? RecalledToPullNote(IReadOnlyCollection<BloodBankLIS.Application.Inventory.RecalledWorkItemDto> recalled, DateTime nowUtc)
    {
        if (recalled.Count == 0)
        {
            return null;
        }

        var oldest = recalled.Select(r => r.RecalledUtc).Where(t => t is not null).Min();
        var parts = new[]
        {
            oldest is DateTime t ? $"oldest {AlertInboxView.OpenFor(t, nowUtc)}" : null,
            AttentionNote(recalled.Count(r => r.WasStatus is UnitStatus.Issued or UnitStatus.TransfusionStarted), "on the ward", "on the ward")
        }.Where(p => p is not null);
        return string.Join("; ", parts) is { Length: > 0 } note ? note : null;
    }

    /// <summary>
    /// The Expired on shelf note (end-user testing pass 55: the card was a bare count): how long the oldest has been
    /// expired, and where they are (the place when one, else how many places), so the tech knows where to pull from.
    /// </summary>
    public static string? ExpiredOnShelfNote(IReadOnlyCollection<ExpiredUnitDto> expired, DateTime nowUtc)
    {
        if (expired.Count == 0)
        {
            return null;
        }

        var oldest = expired.Min(u => u.ExpiresUtc);
        var places = expired.Select(u => string.IsNullOrWhiteSpace(u.LocationName) ? null : u.LocationName.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var where = places.Count == 1
            ? places[0] is string place ? $"in {place}" : "no place recorded"
            : $"in {places.Count} places";
        return $"oldest expired {AlertInboxView.OpenFor(oldest, nowUtc)} ago; {where}";
    }

    /// <summary>The settings-to-enter card note: the names, the first three and how many more.</summary>
    public static string? SettingsToEnterNote(IReadOnlyList<string> displayNames)
    {
        if (displayNames.Count == 0)
        {
            return null;
        }

        var named = string.Join("; ", displayNames.Take(3));
        return displayNames.Count > 3 ? $"{named}; and {displayNames.Count - 3} more" : named;
    }

    public static string? AttentionNote(int count, string singular, string plural)
    {
        if (count <= 0)
        {
            return null;
        }

        return count == 1 ? $"1 {singular}" : $"{count} {plural}";
    }

    /// <summary>
    /// The In transit card note: units past the arrival window, and coolers out that were not checked before they left
    /// or were checked with differences. Either brings the card under Needs attention. No cooler list, no cooler note.
    /// </summary>
    public static string? InTransitNote(int overdue, IEnumerable<OpenCoolerDto>? coolers)
    {
        var open = coolers?.ToList() ?? [];
        var parts = new[]
        {
            AttentionNote(overdue, "past the arrival window", "past the arrival window"),
            AttentionNote(open.Count(c => c.LastCheckedUtc is null), "cooler not checked", "coolers not checked"),
            AttentionNote(open.Count(c => c.LastCheckedUtc is not null && c.LastCheckReady != true), "cooler checked with differences", "coolers checked with differences")
        }.Where(p => p is not null).ToList();
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    /// <summary>
    /// The Reactions card note: fatality follow-ups, open investigations still waiting for their post-transfusion
    /// specimen (it should be drawn promptly after the reaction), and open investigations whose workup tests are not all
    /// verified yet. Closed cases count only for fatality follow-up.
    /// </summary>
    public static string? ReactionsNote(IEnumerable<ReactionInvestigationDto> investigations)
    {
        var all = investigations.ToList();
        var open = all.Where(r => OpenReactionInvestigationRule.NeedsAttention(r.Status)).ToList();
        var parts = new[]
        {
            AttentionNote(OpenReactionInvestigationRule.CountFatalityFollowUpDue(all), "fatality follow-up", "fatality follow-ups"),
            // The patient still has units issued or reserved: check with the physician before another goes (OCD-078).
            AttentionNote(open.Count(r => r.PatientUnitsOutOrReserved > 0), "with units out or reserved", "with units out or reserved"),
            AttentionNote(open.Count(r => r.PostWorkup is null), "waiting for the post-transfusion specimen", "waiting for the post-transfusion specimen"),
            AttentionNote(open.Count(r => (r.PreWorkup?.Pending ?? 0) + (r.PostWorkup?.Pending ?? 0) > 0), "with workup tests to verify", "with workup tests to verify")
        }.Where(p => p is not null).ToList();
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    /// <summary>
    /// The Outstanding issued card note: transfusions started at the bedside and still not completed after the
    /// facility's reminder hours (the same flag as the shift handoff and the Issued units badge).
    /// </summary>
    public static string? OutstandingNote(IEnumerable<OutstandingIssueWorkItemDto> outstanding) =>
        AttentionNote(outstanding.Count(o => o.TransfusionCompletionOverdue), "transfusion completion overdue", "transfusion completions overdue");

    /// <summary>
    /// The Second ABO/Rh card note: patients whose reserved units wait on a second type with no ABO/Rh ordered, so
    /// nothing is in motion to release them. Patients with one ordered stay in the count only.
    /// </summary>
    public static string? SecondAboNote(IEnumerable<SecondAboWorkItemDto> items) =>
        AttentionNote(items.Count(i => i.AboOrderNumber is null), "with no ABO/Rh ordered", "with no ABO/Rh ordered");

    /// <summary>
    /// The Unidentified patients card note: charts open past the facility reminder hours (OCD-077), naming the oldest's
    /// age. Charts within the window stay in the count only, so a trauma registered an hour ago is not flagged.
    /// </summary>
    public static string? UnidentifiedNote(IEnumerable<PatientDto> unidentified, DateTime nowUtc)
    {
        var past = unidentified.Where(p => p.UnidentifiedPastReminder).ToList();
        if (past.Count == 0)
        {
            return null;
        }

        var oldest = AlertInboxView.OpenFor(past.Min(p => p.CreatedUtc), nowUtc);
        return $"{AttentionNote(past.Count, "not identified", "not identified")}, oldest open {oldest}";
    }

    /// <summary>
    /// The HL7 errors card note: how long the oldest unresolved error has waited (an order, result, or admission that has
    /// not reached its destination for that long). The card leads under Needs attention whenever it has errors.
    /// </summary>
    public static string? OldestWaitingNote(IEnumerable<DateTime?> queuedUtc, DateTime nowUtc)
    {
        var oldest = queuedUtc.Where(t => t is not null).Min();
        return oldest is null ? null : $"oldest waiting {AlertInboxView.OpenFor(oldest.Value, nowUtc)}";
    }

    /// <summary>
    /// The Final disposition card note: how long the oldest unit has been past its window. Every unit on the board is
    /// already overdue, so the note brings the card under Needs attention and says how far behind it is.
    /// </summary>
    public static string? DispositionNote(IEnumerable<OverdueDispositionWorkItemDto> overdue, DateTime nowUtc)
    {
        var oldest = overdue.Select(o => (DateTime?)o.DueUtc).Min();
        return oldest is null ? null : $"oldest {AlertInboxView.OpenFor(oldest.Value, nowUtc)} past due";
    }

    /// <summary>
    /// The Supervisor queue card note: results that must be verified before their specimen expires, missing or damaged
    /// units, and overrides waiting for a second-person review. Antibody ID reviews and quarantine releases stay in the
    /// count only; they have no deadline of their own here.
    /// </summary>
    public static string? SupervisorNote(IEnumerable<SupervisorQueueItemDto> items)
    {
        var all = items.ToList();
        var parts = new[]
        {
            AttentionNote(all.Count(i => i.Kind == SupervisorQueueRule.KindResultVerify && i.DueSoon), "to verify before its specimen expires", "to verify before their specimens expire"),
            AttentionNote(all.Count(i => i.Kind == SupervisorQueueRule.KindDiscrepancy), "missing or damaged unit", "missing or damaged units"),
            // Cycle 701 (OCD-083): transfusions the EHR reported over a unit's status, waiting for review.
            AttentionNote(all.Count(i => i.Kind == SupervisorQueueRule.KindTransfusionReported), "reported transfusion to review", "reported transfusions to review"),
            AttentionNote(all.Count(i => i.Kind == SupervisorQueueRule.KindOverrideReview), "override to review", "overrides to review")
        }.Where(p => p is not null).ToList();
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    /// <summary>
    /// The Open deviations card: every deviation not yet closed, naming the critical ones (which puts the card under
    /// Needs attention). Major and minor stay in the count.
    /// </summary>
    /// <summary>
    /// Temperature excursions: the count is locations' excursions still out of range (urgent); those back in range with
    /// their deviation still open are named in the note, and counted under open deviations (end-user testing pass 40).
    /// </summary>
    public static (int Count, string? Note) ExcursionsCard(IEnumerable<StorageTemperatureReadingDto> open)
    {
        var rows = open.ToList();
        var ongoing = rows.Count(r => !r.BackInRange);
        var recovered = rows.Where(r => r.BackInRange).Select(r => r.LocationCode).Distinct().Count();
        return (ongoing, recovered == 0
            ? null
            : $"{recovered} back in range: close {(recovered == 1 ? "its deviation" : "their deviations")}");
    }

    public static (int Count, string? Note) DeviationsCard(IEnumerable<DeviationDto> deviations)
    {
        var open = deviations.Where(d => d.Status != DeviationStatus.Closed).ToList();
        return (open.Count, AttentionNote(open.Count(d => d.Severity == DeviationSeverity.Critical), "critical", "critical"));
    }

    /// <summary>
    /// The Reagent QC card note: failed QC (results on that lot are at risk) and overdue daily QC. QC waiting for review
    /// stays in the count only.
    /// </summary>
    /// <summary>The home card for panel lots: the count needing action, and the soonest message.</summary>
    public static (int Count, string? Note) PanelLotsCard(IReadOnlyCollection<AntibodyPanelLotSupplyItemDto> items)
    {
        var act = items.Where(i => i.NeedsAction).ToList();
        if (act.Count == 0)
        {
            return (0, null);
        }

        var first = act[0].Message;
        return (act.Count, act.Count == 1 ? first : $"{first} (+{act.Count - 1} more)");
    }

    /// <summary>"1 failed; 2 overdue": failed and overdue checks; checks merely due today or never done need no note.</summary>
    public static string? EquipmentChecksNote(IEnumerable<EquipmentCheckDueDto> due)
    {
        var items = due.ToList();
        var parts = new[]
        {
            AttentionNote(items.Count(i => i.State == nameof(EquipmentCheckRule.State.Failed)), "failed", "failed"),
            AttentionNote(items.Count(i => i.State == nameof(EquipmentCheckRule.State.Overdue)), "overdue", "overdue")
        }.Where(p => p is not null).ToList();
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    public static string? ReagentQcNote(IEnumerable<ReagentQcWorkItemDto> work)
    {
        var items = work.ToList();
        var parts = new[]
        {
            AttentionNote(items.Count(i => i.Kind == ReagentQcWorklistRule.Failed), "failed", "failed"),
            AttentionNote(items.Count(i => i.Kind == ReagentQcWorklistRule.Overdue), "overdue", "overdue"),
            AttentionNote(items.Count(i => i.Kind == ReagentQcWorklistRule.Expired), "lot expired", "lots expired"),
            AttentionNote(items.Count(i => i.Kind == ReagentQcWorklistRule.Replace), "lot to replace", "lots to replace")
        }.Where(p => p is not null).ToList();
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    /// <summary>
    /// The Below minimum stock card: products (by ABO when the level names one) with fewer available units than their
    /// minimum, the largest shortfall first, named with what is on hand ("RBC O: 2 of 6"), so the shortage is seen without
    /// opening the page. Any shortage needs attention.
    /// </summary>
    public static (int Count, string? Note) LowStockCard(IEnumerable<MinimumStockRowDto> rows, int named = 3)
    {
        var shortages = rows.Where(r => r.Shortfall > 0)
            .OrderByDescending(r => r.Shortfall).ThenBy(r => r.ProductCode).ThenBy(r => r.Abo)
            .ToList();
        if (shortages.Count == 0)
        {
            return (0, null);
        }

        var names = shortages.Take(named)
            .Select(r => $"{(string.IsNullOrWhiteSpace(r.ProductCode) ? $"Product {r.ProductTypeId}" : r.ProductCode)} {r.Abo?.ToString() ?? "any ABO"}: {r.OnHand} of {r.MinimumAvailable}"
                + (r.OnOrder > 0 ? $", {r.OnOrder} on order" + (r.OnOrderOverdue > 0 ? " (overdue)" : string.Empty) : string.Empty));
        var more = shortages.Count > named ? $" and {shortages.Count - named} more" : string.Empty;
        return (shortages.Count, string.Join("; ", names) + more);
    }

    /// <summary>Near-expiry units reserved for a patient: issue them first or reserve a replacement.</summary>
    public static string? NearExpiryNote(int reservedCount) =>
        AttentionNote(reservedCount, "reserved for a patient", "reserved for patients");

    public static string? HandoffNote(bool open) =>
        open ? "Needs acknowledgment" : null;

    /// <summary>
    /// The open handoff's card note: it needs acknowledgment, and which urgent items it hands over (up to three, then
    /// how many more), so the incoming technologist sees them from the home screen.
    /// </summary>
    public static string? HandoffNote(ShiftHandoffDto? open)
    {
        if (open is null)
        {
            return null;
        }

        var urgent = ShiftHandoffItems.For(open).Where(i => i.Urgent).Select(i => i.Label).ToList();
        if (urgent.Count == 0)
        {
            return HandoffNote(true);
        }

        var named = string.Join(", ", urgent.Take(3)) + (urgent.Count > 3 ? $", and {urgent.Count - 3} more" : "");
        return $"Needs acknowledgment; urgent: {named}";
    }

    private static void Add(List<Card> cards, bool show, string key, string label, string href)
    {
        if (show)
        {
            cards.Add(new Card(key, label, href));
        }
    }
}
