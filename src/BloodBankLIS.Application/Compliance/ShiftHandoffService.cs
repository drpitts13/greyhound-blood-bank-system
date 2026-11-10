using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Compliance;

public sealed record CreateShiftHandoffRequest(string Notes, string? ToUser = null);

public sealed record ShiftHandoffDto(
    long Id,
    DateTime RecordedUtc,
    string FromUser,
    string? ToUser,
    string Notes,
    int RetrospectiveDue,
    int RetrospectiveOverdue,
    int OutstandingIssued,
    int InTransit,
    int InTransitOverdue,
    int PendingTests,
    int PendingSpecimens,
    int OpenReactions,
    int FatalityFollowUpDue,
    string? AcknowledgedBy,
    DateTime? AcknowledgedUtc,
    int ActiveMtp = 0,
    int TransfusionsRunning = 0,
    int TransfusionsCompletionOverdue = 0,
    int RetrospectiveIncompatible = 0,
    int PendingTestsVerifyBeforeExpiry = 0,
    int ResultsAwaitingVerification = 0,
    int ReservationsLapsed = 0,
    int NearExpiryUnits = 0,
    int NearExpiryReserved = 0,
    int CoolersInTransit = 0,
    int CoolersNotChecked = 0,
    int CoolersWithDifferences = 0,
    int ExpiredOnShelf = 0,
    int OpenDeviations = 0,
    int CriticalDeviations = 0,
    int ProductsBelowMinimum = 0,
    int InstrumentFailuresOpen = 0,
    int InstrumentResultsHeld = 0,
    int ReagentLotsFailedQc = 0,
    int ReagentResultsToReview = 0,
    int Hl7ErrorsOpen = 0,
    int UnmappedInterfaceValues = 0,
    int RhigOpen = 0,
    int RhigToGive = 0,
    int UnidentifiedPatients = 0,
    int TubesDueForDiscard = 0,
    int BloodOrdersToPrepare = 0,
    int BloodOrdersPastNeededBy = 0,
    int BloodOrdersNeededSoon = 0,
    int ActiveMtpIdle = 0,
    int ReactionsWithUnitsOut = 0,
    int AntibodyWorkupsOpen = 0,
    int AntibodyWorkupsToReview = 0,
    int Hl7OutboundNotSent = 0,
    int OutagesOpen = 0)
{
    public bool IsAcknowledged => AcknowledgedUtc is not null;

    /// <summary>One line for the handoff list: safety items first, then each queue with what needs attention.</summary>
    public string QueueSummary =>
        (ActiveMtp > 0 ? $"MASSIVE TRANSFUSION ACTIVE: {ActiveMtp}{(ActiveMtpIdle > 0 ? $" ({ActiveMtpIdle} idle, to close)" : "")}; " : "") +
        (RetrospectiveIncompatible > 0 ? $"INCOMPATIBLE AFTER RELEASE: {RetrospectiveIncompatible}; " : "") +
        (CriticalDeviations > 0 ? $"CRITICAL DEVIATION OPEN: {CriticalDeviations}; " : "") +
        (ReagentLotsFailedQc > 0
            ? $"REAGENT QC FAILED: {ReagentLotsFailedQc} {(ReagentLotsFailedQc == 1 ? "lot" : "lots")}" +
              (ReagentResultsToReview > 0 ? $", {ReagentResultsToReview} {(ReagentResultsToReview == 1 ? "result" : "results")} to review" : "") + "; "
            : "") +
        (TransfusionsRunning > 0
            ? $"transfusing {TransfusionsRunning}" + (TransfusionsCompletionOverdue > 0 ? $", {TransfusionsCompletionOverdue} completion overdue" : "") + "; "
            : "") +
        $"Retro {RetrospectiveDue}" +
        (RetrospectiveOverdue > 0 ? $", {RetrospectiveOverdue} overdue" : "") +
        $"; issued {OutstandingIssued}; transit {InTransit}" +
        (InTransitOverdue > 0 ? $", {InTransitOverdue} overdue" : "") +
        (CoolersInTransit > 0
            ? $"; coolers out {CoolersInTransit}" +
              (CoolersNotChecked > 0 ? $", {CoolersNotChecked} not checked" : "") +
              (CoolersWithDifferences > 0 ? $", {CoolersWithDifferences} checked with differences" : "")
            : "") +
        (ReservationsLapsed > 0 ? $"; {ReservationsLapsed} past reservation" : "") +
        $"; tests {PendingTests}" +
        (ResultsAwaitingVerification > 0 ? $", {ResultsAwaitingVerification} to verify" : "") +
        (PendingTestsVerifyBeforeExpiry > 0 ? $", {PendingTestsVerifyBeforeExpiry} to verify before specimen expiry" : "") +
        $"; specimens {PendingSpecimens}; reactions {OpenReactions}" +
        (FatalityFollowUpDue > 0 ? $", {FatalityFollowUpDue} fatality follow-up" : "") + (ReactionsWithUnitsOut > 0 ? $", {ReactionsWithUnitsOut} with units out or reserved" : "") +
        (NearExpiryUnits > 0
            ? $"; near expiry {NearExpiryUnits}" + (NearExpiryReserved > 0 ? $", {NearExpiryReserved} reserved" : "")
            : "") +
        (ExpiredOnShelf > 0 ? $"; EXPIRED ON SHELF {ExpiredOnShelf}, to discard" : "") +
        (ProductsBelowMinimum > 0 ? $"; BELOW MINIMUM STOCK {ProductsBelowMinimum} {(ProductsBelowMinimum == 1 ? "product" : "products")}" : "") +
        (OpenDeviations > 0 ? $"; deviations open {OpenDeviations}" : "") +
        (InstrumentFailuresOpen > 0
            ? $"; instrument failures {InstrumentFailuresOpen}" + (InstrumentResultsHeld > 0 ? $", {InstrumentResultsHeld} held for review" : "")
            : "") +
        (Hl7ErrorsOpen > 0 ? $"; HL7 errors {Hl7ErrorsOpen}" : "") +
        (Hl7OutboundNotSent > 0 ? $"; HL7 outbound not sent {Hl7OutboundNotSent}" : "") +
        (OutagesOpen > 0 ? $"; OUTAGE STILL OPEN, end it when the system is back" : "") +
        (UnmappedInterfaceValues > 0 ? $"; unmapped interface values {UnmappedInterfaceValues}" : "") +
        (RhigOpen > 0 ? $"; RhIG evaluations {RhigOpen}" + (RhigToGive > 0 ? $", {RhigToGive} to give" : "") : "") +
        (AntibodyWorkupsOpen > 0
            ? $"; antibody workups {AntibodyWorkupsOpen}" + (AntibodyWorkupsToReview > 0 ? $", {AntibodyWorkupsToReview} to review" : "")
            : "") +
        (UnidentifiedPatients > 0 ? $"; UNIDENTIFIED PATIENTS {UnidentifiedPatients}, to merge once identified" : "") +
        (TubesDueForDiscard > 0 ? $"; tubes due for discard {TubesDueForDiscard}" : "") +
        (BloodOrdersToPrepare > 0
            ? $"; blood orders to prepare {BloodOrdersToPrepare}" + (BloodOrdersPastNeededBy > 0 ? $", {BloodOrdersPastNeededBy} PAST NEEDED-BY" : "")
                + (BloodOrdersNeededSoon > 0 ? $", {BloodOrdersNeededSoon} needed soon" : "")
            : "");

    public static ShiftHandoffDto From(ShiftHandoff h) => new(
        h.Id, h.RecordedUtc, h.FromUser, h.ToUser, h.Notes,
        h.RetrospectiveDue, h.RetrospectiveOverdue, h.OutstandingIssued,
        h.InTransit, h.InTransitOverdue, h.PendingTests, h.PendingSpecimens,
        h.OpenReactions, h.FatalityFollowUpDue, h.AcknowledgedBy, h.AcknowledgedUtc, h.ActiveMtp,
        h.TransfusionsRunning, h.TransfusionsCompletionOverdue, h.RetrospectiveIncompatible,
        h.PendingTestsVerifyBeforeExpiry, h.ResultsAwaitingVerification, h.ReservationsLapsed,
        h.NearExpiryUnits, h.NearExpiryReserved, h.CoolersInTransit, h.CoolersNotChecked, h.CoolersWithDifferences,
        h.ExpiredOnShelf, h.OpenDeviations, h.CriticalDeviations, h.ProductsBelowMinimum,
        h.InstrumentFailuresOpen, h.InstrumentResultsHeld, h.ReagentLotsFailedQc, h.ReagentResultsToReview,
        h.Hl7ErrorsOpen, h.UnmappedInterfaceValues, h.RhigOpen, h.RhigToGive, h.UnidentifiedPatients, h.TubesDueForDiscard,
        h.BloodOrdersToPrepare, h.BloodOrdersPastNeededBy, h.BloodOrdersNeededSoon, h.ActiveMtpIdle, h.ReactionsWithUnitsOut,
        h.AntibodyWorkupsOpen, h.AntibodyWorkupsToReview, h.Hl7OutboundNotSent, h.OutagesOpen);
}

/// <summary>
/// Records and acknowledges end-of-shift packets. Snapshot counts use the same
/// queues as the dashboard and do not change clinical gates.
/// </summary>
public sealed class ShiftHandoffService
{
    private readonly IRepository<ShiftHandoff> _handoffs;
    private readonly IssuingService? _issuing;
    private readonly TestWorklistService? _tests;
    private readonly SpecimenService? _specimens;
    private readonly ReactionInvestigationService? _reactions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter? _audit;
    private readonly IPermissionEvaluator? _permissions;
    private readonly IRepository<User>? _users;
    private readonly IRepository<MtpActivation>? _mtpActivations;
    private readonly IRepository<Issue>? _issueRecords;
    private readonly FacilityPolicyService? _policy;
    private readonly InventoryService? _inventory;
    private readonly CoolerCheckService? _coolers;
    // Read directly, like the other counts: whoever hands off sees open deviations without deviation.manage.
    private readonly IRepository<Deviation>? _deviations;
    // Read without admin.config.edit: minimum stock levels are configuration, the shortage is shift work.
    private readonly MinimumStockService? _minimumStock;
    private readonly IRepository<InstrumentMessageLog>? _instrumentMessages;
    private readonly ReagentQcService? _reagentQc;
    private readonly IRepository<InterfaceErrorQueueItem>? _hl7Errors;
    private readonly IRepository<Hl7MessageLog>? _hl7Messages;
    private readonly IRepository<DowntimeEpisode>? _outages;
    private readonly IRepository<InterfaceUnmappedValue>? _unmapped;
    private readonly IRepository<RhigEvaluation>? _rhig;
    private readonly IRepository<Patient>? _patientCharts;
    private readonly BloodBankLIS.Application.Specimens.SpecimenStorageService? _tubeStorage;
    private readonly BloodBankLIS.Application.PatientWorkspace.BloodOrderWorklistService? _bloodOrders;
    private readonly IRepository<AntibodyIdentificationWorkup>? _antibodyWorkups;

    public ShiftHandoffService(
        IRepository<ShiftHandoff> handoffs,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IssuingService? issuing = null,
        TestWorklistService? tests = null,
        SpecimenService? specimens = null,
        ReactionInvestigationService? reactions = null,
        IAuditWriter? audit = null,
        IPermissionEvaluator? permissions = null,
        IRepository<User>? users = null,
        IRepository<MtpActivation>? mtpActivations = null,
        InventoryService? inventory = null,
        CoolerCheckService? coolers = null,
        IRepository<Deviation>? deviations = null,
        MinimumStockService? minimumStock = null,
        IRepository<InstrumentMessageLog>? instrumentMessages = null,
        ReagentQcService? reagentQc = null,
        IRepository<InterfaceErrorQueueItem>? hl7Errors = null,
        IRepository<InterfaceUnmappedValue>? unmapped = null,
        IRepository<RhigEvaluation>? rhig = null,
        IRepository<Patient>? patients = null,
        BloodBankLIS.Application.Specimens.SpecimenStorageService? tubeStorage = null,
        BloodBankLIS.Application.PatientWorkspace.BloodOrderWorklistService? bloodOrders = null,
        IRepository<Issue>? issues = null,
        FacilityPolicyService? policy = null,
        IRepository<AntibodyIdentificationWorkup>? antibodyWorkups = null,
        IRepository<Hl7MessageLog>? hl7Messages = null,
        IRepository<DowntimeEpisode>? outages = null)
    {
        _hl7Messages = hl7Messages;
        _outages = outages;
        _antibodyWorkups = antibodyWorkups;
        _bloodOrders = bloodOrders;
        _issueRecords = issues;
        _policy = policy;
        _patientCharts = patients;
        _tubeStorage = tubeStorage;
        _hl7Errors = hl7Errors;
        _unmapped = unmapped;
        _rhig = rhig;
        _minimumStock = minimumStock;
        _instrumentMessages = instrumentMessages;
        _reagentQc = reagentQc;
        _deviations = deviations;
        _coolers = coolers;
        _inventory = inventory;
        _users = users;
        _mtpActivations = mtpActivations;
        _handoffs = handoffs;
        _issuing = issuing;
        _tests = tests;
        _specimens = specimens;
        _reactions = reactions;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<ShiftHandoffDto>> ListRecentAsync(int take = 20, CancellationToken ct = default)
    {
        var rows = await _handoffs.ListAsync(ct);
        return rows
            .OrderByDescending(h => h.RecordedUtc)
            .ThenByDescending(h => h.Id)
            .Take(Math.Clamp(take, 1, 100))
            .Select(ShiftHandoffDto.From)
            .ToList();
    }

    public async Task<ShiftHandoffDto?> GetLatestOpenAsync(CancellationToken ct = default)
    {
        var rows = await _handoffs.ListAsync(h => h.AcknowledgedUtc == null, ct);
        var latest = rows.OrderByDescending(h => h.RecordedUtc).ThenByDescending(h => h.Id).FirstOrDefault();
        return latest is null ? null : ShiftHandoffDto.From(latest);
    }

    public async Task<OperationResult<ShiftHandoff>> CreateAsync(
        CreateShiftHandoffRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var notesRule = ShiftHandoffRule.EvaluateNotes(request.Notes);
        if (notesRule.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<ShiftHandoff>.Fail(notesRule.Message);
        }

        if (!string.IsNullOrWhiteSpace(request.ToUser) && _users is not null)
        {
            var upper = request.ToUser.Trim().ToUpperInvariant();
            var active = await _users.AnyAsync(
                u => u.IsActive && !u.IsLocked && !u.IsServiceAccount && u.UserName.ToUpper() == upper, ct);
            var recipient = ShiftHandoffRule.EvaluateRecipient(request.ToUser, _currentUser.UserName, active);
            if (recipient.Severity == RuleSeverity.HardStop)
            {
                return OperationResult<ShiftHandoff>.Fail(recipient.Message);
            }
        }
        else
        {
            var self = ShiftHandoffRule.EvaluateRecipient(request.ToUser, _currentUser.UserName, isActiveUser: true);
            if (self.Severity == RuleSeverity.HardStop)
            {
                return OperationResult<ShiftHandoff>.Fail(self.Message);
            }
        }

        var row = await SnapshotAsync(ct);
        row.ToUser = string.IsNullOrWhiteSpace(request.ToUser) ? null : request.ToUser.Trim();
        row.Notes = request.Notes.Trim();

        await _handoffs.AddAsync(row, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        _audit?.Record(
            AuditEventType.ShiftHandoff,
            nameof(ShiftHandoff),
            row.Id,
            newValue: Snapshot(row),
            reason: "Shift handoff recorded.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<ShiftHandoff>.Ok(row);
    }

    /// <summary>
    /// What a handoff recorded now would carry, without recording it: the person handing over sees the counts while
    /// writing the note. Uses the same counting as <see cref="CreateAsync"/>.
    /// </summary>
    public async Task<OperationResult<ShiftHandoffDto>> PreviewAsync(CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(ct);
        if (denied is not null)
        {
            return OperationResult<ShiftHandoffDto>.Fail(denied.Error ?? "Shift handoff requires the shift.handoff permission.");
        }

        return OperationResult<ShiftHandoffDto>.Ok(ShiftHandoffDto.From(await SnapshotAsync(ct)));
    }

    /// <summary>The queue counts at this moment, the same ones the dashboard shows.</summary>
    private async Task<ShiftHandoff> SnapshotAsync(CancellationToken ct)
    {
        var retro = _issuing is null
            ? Array.Empty<RetrospectiveCrossmatchWorkItemDto>()
            : await _issuing.ListPendingRetrospectiveCrossmatchesAsync(ct);
        var outstanding = _issuing is null
            ? Array.Empty<OutstandingIssueWorkItemDto>()
            : await _issuing.ListOutstandingIssuedAsync(ct);
        var transit = _issuing is null
            ? Array.Empty<InTransitWorkItemDto>()
            : await _issuing.ListInTransitAsync(ct);
        var pendingTests = _tests is null
            ? Array.Empty<TestWorkItemDto>()
            : await _tests.ListPendingGlobalAsync(ct);
        var pendingSpecimens = _specimens is null
            ? Array.Empty<PendingSpecimenWorkItemDto>()
            : await _specimens.ListPendingReceiptAsync(ct);
        var reactions = _reactions is null
            ? Array.Empty<ReactionInvestigationDto>()
            : await _reactions.ListDtosAsync(ct);
        var ready = _issuing is null
            ? Array.Empty<ReadyToIssueWorkItemDto>()
            : await _issuing.ListReadyToIssueAllocationsAsync(ct);
        var nearExpiry = _inventory is null
            ? Array.Empty<NearExpiryWorkItemDto>()
            : await _inventory.ListNearExpiryAsync(ct);
        var coolers = _coolers is null
            ? Array.Empty<OpenCoolerDto>()
            : await _coolers.SummarizeOpenAsync(ct);
        var now = _clock.UtcNow;
        var openDeviations = _deviations is null
            ? []
            : await _deviations.ListAsync(d => d.Status != DeviationStatus.Closed, ct);
        // The next shift picks up failed instrument messages (held results wait on a review) and failed reagent lots.
        var instrumentFailures = _instrumentMessages is null
            ? []
            : await _instrumentMessages.ListAsync(m => m.Status == InstrumentMessageStatus.Failed && m.ResolvedUtc == null, ct);
        var failedLots = _reagentQc is null
            ? []
            : (await _reagentQc.ListWorkAsync(ct)).Where(w => w.Kind == ReagentQcWorklistRule.Failed).GroupBy(w => w.LotId).Select(g => g.First()).ToList();

        // RhIG evaluations still needing work: open, or a completed candidate with nothing given (Cycle 526–527).
        var bloodOrders = _bloodOrders is null ? [] : await _bloodOrders.ListAsync(ct);
        var rhigOpen = _rhig is null
            ? []
            : (await _rhig.ListAsync(e => e.CompletedUtc == null || (e.Decision == RhigDecision.Candidate && e.GivenUtc == null), ct)).ToList();
        // A workup half done at shift change: the next shift reads the panel, interprets, or reviews it.
        var antibodyWorkups = _antibodyWorkups is null
            ? []
            : await _antibodyWorkups.ListAsync(
                w => w.Status != AntibodyWorkupStatus.Completed && w.Status != AntibodyWorkupStatus.Voided, ct);

        return new ShiftHandoff
        {
            RecordedUtc = _clock.UtcNow,
            FromUser = _currentUser.UserName,
            RetrospectiveDue = retro.Count,
            RetrospectiveOverdue = RetrospectiveCrossmatchAttentionRule.CountOverdue(retro),
            OutstandingIssued = outstanding.Count,
            InTransit = transit.Count,
            InTransitOverdue = InTransitAttentionRule.CountOverdue(transit.Select(t => t.IsOverdue)),
            PendingTests = pendingTests.Count,
            PendingSpecimens = pendingSpecimens.Count,
            OpenReactions = OpenReactionInvestigationRule.Count(reactions.Select(r => r.Status)),
            FatalityFollowUpDue = OpenReactionInvestigationRule.CountFatalityFollowUpDue(reactions),
            ActiveMtp = _mtpActivations is null
                ? 0
                : (await _mtpActivations.ListAsync(a => a.Status == MtpActivationStatus.Active, ct)).Count,
            TransfusionsRunning = outstanding.Count(o => o.TransfusionStartedUtc is not null),
            TransfusionsCompletionOverdue = outstanding.Count(o => o.TransfusionCompletionOverdue),
            RetrospectiveIncompatible = retro.Count(r => r.CrossmatchStatus == CrossmatchClinicalStatus.Incompatible),
            PendingTestsVerifyBeforeExpiry = PendingTestAttentionRule.CountVerifyBeforeExpiry(pendingTests),
            ResultsAwaitingVerification = pendingTests.Count(t => t.AwaitsVerification),
            ReservationsLapsed = ReadyToIssueAttentionRule.CountReservationLapsed(ready, now),
            NearExpiryUnits = nearExpiry.Count,
            NearExpiryReserved = nearExpiry.Count(u => u.IsReserved),
            CoolersInTransit = coolers.Count,
            CoolersNotChecked = coolers.Count(c => c.LastCheckedUtc is null),
            CoolersWithDifferences = coolers.Count(c => c.LastCheckedUtc is not null && c.LastCheckReady != true),
            ExpiredOnShelf = _inventory is null ? 0 : await _inventory.CountExpiredOnShelfAsync(ct),
            OpenDeviations = openDeviations.Count,
            CriticalDeviations = openDeviations.Count(d => d.Severity == DeviationSeverity.Critical),
            ProductsBelowMinimum = _minimumStock is null ? 0 : (await _minimumStock.ListAsync(ct)).Count(r => r.Shortfall > 0),
            InstrumentFailuresOpen = instrumentFailures.Count,
            InstrumentResultsHeld = instrumentFailures.Count(m => InstrumentMessageFollowUpRule.IsHeld(m.Status, m.ResolvedUtc, m.ErrorDetail)),
            ReagentLotsFailedQc = failedLots.Count,
            ReagentResultsToReview = failedLots.Sum(l => l.ResultsToReview),
            Hl7ErrorsOpen = _hl7Errors is null ? 0 : (await _hl7Errors.ListAsync(e => !e.Resolved, ct)).Count,
            Hl7OutboundNotSent = await CountOutboundNotSentAsync(now, ct),
            OutagesOpen = _outages is null ? 0 : (await _outages.ListAsync(o => o.EndedUtc == null, ct)).Count,
            UnmappedInterfaceValues = _unmapped is null ? 0 : (await _unmapped.ListAsync(v => v.ResolvedUtc == null, ct)).Count,
            RhigOpen = rhigOpen.Count,
            RhigToGive = rhigOpen.Count(e => e.CompletedUtc != null),
            UnidentifiedPatients = _patientCharts is null
                ? 0
                : (await _patientCharts.ListAsync(p => p.Unidentified && p.Status != PatientStatus.Merged, ct)).Count,
            TubesDueForDiscard = _tubeStorage is null ? 0 : (await _tubeStorage.ListDueForDiscardAsync(ct)).Count,
            BloodOrdersToPrepare = bloodOrders.Count,
            BloodOrdersPastNeededBy = bloodOrders.Count(o => o.NeededByPassed),
            BloodOrdersNeededSoon = bloodOrders.Count(o => o.NeededBySoon),
            ActiveMtpIdle = await CountIdleMtpAsync(now, ct),
            ReactionsWithUnitsOut = ReactionsWithUnitsOut(outstanding, ready),
            AntibodyWorkupsOpen = antibodyWorkups.Count,
            AntibodyWorkupsToReview = antibodyWorkups.Count(w => w.InterpretedUtc != null && w.ReviewedUtc == null)
        };
    }

    public async Task<OperationResult<ShiftHandoff>> AcknowledgeAsync(long id, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var row = await _handoffs.GetByIdAsync(id, ct);
        if (row is null)
        {
            return OperationResult<ShiftHandoff>.Fail("Shift handoff not found.");
        }

        var ack = ShiftHandoffRule.EvaluateAcknowledge(row.FromUser, _currentUser.UserName, row.AcknowledgedUtc);
        if (ack.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<ShiftHandoff>.Fail(ack.Message);
        }

        var previous = new { row.AcknowledgedBy, row.AcknowledgedUtc };
        row.AcknowledgedBy = _currentUser.UserName;
        row.AcknowledgedUtc = _clock.UtcNow;
        _handoffs.Update(row);
        _audit?.Record(
            AuditEventType.ShiftHandoff,
            nameof(ShiftHandoff),
            row.Id,
            oldValue: previous,
            newValue: new { row.AcknowledgedBy, row.AcknowledgedUtc },
            reason: "Shift handoff acknowledged.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<ShiftHandoff>.Ok(row);
    }

    private static object Snapshot(ShiftHandoff row) => new
    {
        row.FromUser,
        row.ToUser,
        row.Notes,
        row.RetrospectiveDue,
        row.RetrospectiveOverdue,
        row.OutstandingIssued,
        row.InTransit,
        row.InTransitOverdue,
        row.PendingTests,
        row.PendingSpecimens,
        row.OpenReactions,
        row.FatalityFollowUpDue,
        row.ActiveMtp,
        row.TransfusionsRunning,
        row.TransfusionsCompletionOverdue,
        row.RetrospectiveIncompatible,
        row.PendingTestsVerifyBeforeExpiry,
        row.ResultsAwaitingVerification,
        row.ReservationsLapsed,
        row.NearExpiryUnits,
        row.NearExpiryReserved,
        row.CoolersInTransit,
        row.CoolersNotChecked,
        row.CoolersWithDifferences,
        row.ExpiredOnShelf,
        row.OpenDeviations,
        row.CriticalDeviations,
        row.ProductsBelowMinimum,
        row.InstrumentFailuresOpen,
        row.InstrumentResultsHeld,
        row.ReagentLotsFailedQc,
        row.ReagentResultsToReview,
        row.Hl7ErrorsOpen,
        row.UnmappedInterfaceValues,
        row.RhigOpen,
        row.RhigToGive,
        UnidentifiedPatients = row.UnidentifiedPatients,
        TubesDueForDiscard = row.TubesDueForDiscard,
        BloodOrdersToPrepare = row.BloodOrdersToPrepare,
        BloodOrdersPastNeededBy = row.BloodOrdersPastNeededBy,
        BloodOrdersNeededSoon = row.BloodOrdersNeededSoon,
        ActiveMtpIdle = row.ActiveMtpIdle,
        ReactionsWithUnitsOut = row.ReactionsWithUnitsOut,
        AntibodyWorkupsOpen = row.AntibodyWorkupsOpen,
        AntibodyWorkupsToReview = row.AntibodyWorkupsToReview,
        Hl7OutboundNotSent = row.Hl7OutboundNotSent,
        OutagesOpen = row.OutagesOpen
    };

    /// <summary>
    /// Outbound messages queued or failed past the stuck window, as the HL7 page lists them (a charge withheld because it
    /// was cancelled is not waiting); 0 without the message log.
    /// </summary>
    private async Task<int> CountOutboundNotSentAsync(DateTime now, CancellationToken ct)
    {
        if (_hl7Messages is null)
        {
            return 0;
        }

        var waiting = await _hl7Messages.ListAsync(m => m.Direction == Hl7Direction.Outbound
            && (m.Status == Hl7MessageStatus.Received || m.Status == Hl7MessageStatus.Errored), ct);
        return waiting.Count(m => m.ErrorDetail != BloodBankLIS.Application.Billing.BillingService.UnsentDftWithheldDetail
            && OutboundMessageWaitRule.IsStuck(m.ReceivedUtc, now));
    }

    private async Task<OperationResult<ShiftHandoff>?> RejectUnauthorizedAsync(CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(
            _currentUser.UserName, PermissionCodes.ShiftHandoff, ct);
        var auth = ShiftHandoffRule.EvaluatePermission(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? OperationResult<ShiftHandoff>.Fail(auth.Message)
            : null;
    }

    /// <summary>
    /// Reaction investigations still open or under review whose patient has a unit issued or reserved: both lists carry
    /// the patient's open investigation (Cycles 626–627), each counted once.
    /// </summary>
    public static int ReactionsWithUnitsOut(
        IEnumerable<OutstandingIssueWorkItemDto> outstanding, IEnumerable<ReadyToIssueWorkItemDto> ready) =>
        outstanding.Select(o => o.OpenReactionInvestigationId)
            .Concat(ready.Select(r => r.OpenReactionInvestigationId))
            .OfType<long>()
            .Distinct()
            .Count();

    /// <summary>Active protocols with no unit issued under them for the facility reminder minutes (OCD-076); 0 without the sources.</summary>
    private async Task<int> CountIdleMtpAsync(DateTime now, CancellationToken ct)
    {
        if (_mtpActivations is null || _issueRecords is null)
        {
            return 0;
        }

        var minutes = _policy is null ? MtpIdleRule.DefaultReminderMinutes : await _policy.GetMtpIdleReminderMinutesAsync(ct);
        var idle = 0;
        foreach (var activation in await _mtpActivations.ListAsync(a => a.Status == MtpActivationStatus.Active, ct))
        {
            var issued = await _issueRecords.ListAsync(i => i.MtpActivationId == activation.Id, ct);
            var last = MtpIdleRule.LastActivityUtc(activation.ActivatedUtc, issued.Count == 0 ? null : issued.Max(i => i.IssuedUtc));
            if (MtpIdleRule.IsIdle(last, now, minutes))
            {
                idle++;
            }
        }

        return idle;
    }
}
