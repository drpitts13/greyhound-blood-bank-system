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
    int UnmappedInterfaceValues = 0)
{
    public bool IsAcknowledged => AcknowledgedUtc is not null;

    /// <summary>One line for the handoff list: safety items first, then each queue with what needs attention.</summary>
    public string QueueSummary =>
        (ActiveMtp > 0 ? $"MASSIVE TRANSFUSION ACTIVE: {ActiveMtp}; " : "") +
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
        (FatalityFollowUpDue > 0 ? $", {FatalityFollowUpDue} fatality follow-up" : "") +
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
        (UnmappedInterfaceValues > 0 ? $"; unmapped interface values {UnmappedInterfaceValues}" : "");

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
        h.Hl7ErrorsOpen, h.UnmappedInterfaceValues);
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
    private readonly InventoryService? _inventory;
    private readonly CoolerCheckService? _coolers;
    // Read directly, like the other counts: whoever hands off sees open deviations without deviation.manage.
    private readonly IRepository<Deviation>? _deviations;
    // Read without admin.config.edit: minimum stock levels are configuration, the shortage is shift work.
    private readonly MinimumStockService? _minimumStock;
    private readonly IRepository<InstrumentMessageLog>? _instrumentMessages;
    private readonly ReagentQcService? _reagentQc;
    private readonly IRepository<InterfaceErrorQueueItem>? _hl7Errors;
    private readonly IRepository<InterfaceUnmappedValue>? _unmapped;

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
        IRepository<InterfaceUnmappedValue>? unmapped = null)
    {
        _hl7Errors = hl7Errors;
        _unmapped = unmapped;
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

        var row = new ShiftHandoff
        {
            RecordedUtc = _clock.UtcNow,
            FromUser = _currentUser.UserName,
            ToUser = string.IsNullOrWhiteSpace(request.ToUser) ? null : request.ToUser.Trim(),
            Notes = request.Notes.Trim(),
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
            UnmappedInterfaceValues = _unmapped is null ? 0 : (await _unmapped.ListAsync(v => v.ResolvedUtc == null, ct)).Count
        };

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
        row.UnmappedInterfaceValues
    };

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
}
