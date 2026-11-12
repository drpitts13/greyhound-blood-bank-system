using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Inventory;

public sealed record ReagentDto(long Id, string Code, string Name, bool IsActive, string? TestCodes = null);

/// <summary>A result recorded as tested with a reagent lot (lot lookback).</summary>
public sealed record ReagentLotResultDto(
    long ResultId,
    long PatientId,
    string? PatientMrn,
    string? PatientName,
    string? SpecimenAccession,
    string TestCode,
    int Version,
    ResultStatus Status,
    DateTime? EnteredUtc,
    string? EnteredBy,
    string? Value,
    // The lot's latest QC failed and this result was recorded with it after its last passed QC, not yet reviewed.
    bool ToReview = false,
    DateTime? ReviewedUtc = null,
    string? ReviewedBy = null,
    string? ReviewNote = null);

/// <summary>A reagent lot recorded against a result.</summary>
public sealed record ResultReagentLotDto(long ReagentLotId, string ReagentCode, string LotNumber, DateTime RecordedUtc);

public sealed record SetReagentTestsRequest(string? TestCodes);

public sealed record RenameReagentRequest(string Name, string? Reason);

public sealed record ReagentActiveRequest(bool Active, string? Reason);

/// <summary>The lots recorded against a result and anything the person entering it should know.</summary>
public sealed record LotUse(IReadOnlyList<TestResultReagentLot> Recorded, IReadOnlyList<RuleResult> Warnings)
{
    public static LotUse None { get; } = new([], []);
}

public sealed record ReagentLotDto(
    long Id,
    long ReagentId,
    string ReagentCode,
    string LotNumber,
    DateTime ReceivedUtc,
    DateTime? OpenedUtc,
    DateTime? ClosedUtc,
    DateTime ExpiresUtc,
    ReagentLotState State);

public sealed record ReagentDailyQcDto(
    long Id,
    long ReagentLotId,
    string LotNumber,
    DateTime QcDateUtc,
    string ExpectedResult,
    string ObservedResult,
    bool Passed,
    string PerformedBy,
    DateTime PerformedUtc,
    string? ReviewedBy,
    DateTime? ReviewedUtc,
    long? ReagentId = null,
    string? ReagentCode = null);

/// <summary>Helpers for the daily QC form.</summary>
public static class ReagentQcHistory
{
    /// <summary>
    /// The expected result most recently recorded for this reagent (any lot), to offer on the QC form. Only an offer:
    /// the package insert is the authority, so the form never fills it in by itself.
    /// </summary>
    public static string? LastExpected(long reagentId, IEnumerable<ReagentDailyQcDto> history) =>
        history.Where(q => q.ReagentId == reagentId && !string.IsNullOrWhiteSpace(q.ExpectedResult))
            .OrderByDescending(q => q.PerformedUtc)
            .ThenByDescending(q => q.Id)
            .Select(q => q.ExpectedResult.Trim())
            .FirstOrDefault();

    /// <summary>The one QC-able lot whose number is the scan, or null when none or several match.</summary>
    public static ReagentLotDto? LotForScan(string? scan, IEnumerable<ReagentLotDto> openLots)
    {
        var value = scan?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var matches = openLots.Where(l => string.Equals(l.LotNumber.Trim(), value, StringComparison.OrdinalIgnoreCase)).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }
}

public sealed record ReagentQcWorkItemDto(
    string Kind,
    long LotId,
    string LotNumber,
    long? QcId,
    string Message,
    // A failed lot: results recorded with it since its last passed QC, to review.
    int ResultsToReview = 0);

public sealed record SaveReagentRequest(string Code, string Name, string? TestCodes = null);

public sealed record ReceiveReagentLotRequest(long ReagentId, string LotNumber, DateTime ExpiresUtc);

public sealed record RecordReagentQcRequest(
    long ReagentLotId,
    string ExpectedResult,
    string ObservedResult,
    bool Passed);

public sealed class ReagentQcService
{
    private readonly IRepository<Reagent> _reagents;
    private readonly IRepository<ReagentLot> _lots;
    private readonly IRepository<ReagentDailyQc> _qc;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IRepository<TestResultReagentLot>? _resultLots;
    private readonly IRepository<TestResult>? _results;
    private readonly IRepository<Specimen>? _specimens;
    private readonly IRepository<Patient>? _patients;
    private readonly IRepository<TestDefinition>? _tests;

    public ReagentQcService(
        IRepository<Reagent> reagents,
        IRepository<ReagentLot> lots,
        IRepository<ReagentDailyQc> qc,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IRepository<TestResultReagentLot>? resultLots = null,
        IRepository<TestResult>? results = null,
        IRepository<Specimen>? specimens = null,
        IRepository<Patient>? patients = null,
        IRepository<TestDefinition>? tests = null)
    {
        _resultLots = resultLots;
        _results = results;
        _specimens = specimens;
        _patients = patients;
        _tests = tests;
        _reagents = reagents;
        _lots = lots;
        _qc = qc;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<IReadOnlyList<ReagentDto>> ListReagentsAsync(CancellationToken ct = default)
    {
        var rows = await _reagents.ListAsync(ct);
        return rows
            .OrderBy(r => r.Code)
            .Select(r => new ReagentDto(r.Id, r.Code, r.Name, r.IsActive, r.TestCodes))
            .ToList();
    }

    /// <summary>Sets the tests a reagent is used for (audited). Unknown test codes are refused when the catalog is known.</summary>
    public async Task<OperationResult<ReagentDto>> SetReagentTestsAsync(long reagentId, SetReagentTestsRequest request, CancellationToken ct = default)
    {
        var reagent = await _reagents.GetByIdAsync(reagentId, ct);
        if (reagent is null)
        {
            return OperationResult<ReagentDto>.Fail("Reagent not found.");
        }

        var unknown = await UnknownTestCodesAsync(request.TestCodes, ct);
        if (unknown is not null)
        {
            return OperationResult<ReagentDto>.Fail(unknown);
        }

        var before = reagent.TestCodes;
        reagent.TestCodes = ReagentLotUseRule.NormalizeTestCodes(request.TestCodes);
        _reagents.Update(reagent);
        _audit.Record(AuditEventType.Update, nameof(Reagent), reagent.Id, new { TestCodes = before }, new { reagent.TestCodes },
            "Tests the reagent is used for changed.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<ReagentDto>.Ok(new ReagentDto(reagent.Id, reagent.Code, reagent.Name, reagent.IsActive, reagent.TestCodes));
    }

    public async Task<OperationResult<ReagentDto>> RenameReagentAsync(long reagentId, RenameReagentRequest request, CancellationToken ct = default)
    {
        var reagent = await _reagents.GetByIdAsync(reagentId, ct);
        if (reagent is null)
        {
            return OperationResult<ReagentDto>.Fail("Reagent not found.");
        }

        var rule = ReagentRegisterRule.EvaluateRename(request.Name, request.Reason);
        if (rule.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<ReagentDto>.Fail(rule.Message);
        }

        var before = reagent.Name;
        reagent.Name = request.Name.Trim();
        _reagents.Update(reagent);
        _audit.Record(AuditEventType.Update, nameof(Reagent), reagent.Id, new { Name = before }, new { reagent.Name }, request.Reason!.Trim());
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<ReagentDto>.Ok(new ReagentDto(reagent.Id, reagent.Code, reagent.Name, reagent.IsActive, reagent.TestCodes));
    }

    /// <summary>
    /// Retire a reagent no longer used (refused while any lot is received or open), or restore it; a reason is required
    /// to retire. A retired reagent takes no new lots and records no lots on results; its lots and QC are kept.
    /// </summary>
    public async Task<OperationResult<ReagentDto>> SetReagentActiveAsync(long reagentId, ReagentActiveRequest request, CancellationToken ct = default)
    {
        var reagent = await _reagents.GetByIdAsync(reagentId, ct);
        if (reagent is null)
        {
            return OperationResult<ReagentDto>.Fail("Reagent not found.");
        }

        if (reagent.IsActive == request.Active)
        {
            return OperationResult<ReagentDto>.Fail(request.Active ? "This reagent is already in use." : "This reagent is already retired.");
        }

        if (!request.Active)
        {
            var onHand = (await _lots.ListAsync(l => l.ReagentId == reagentId && l.State != ReagentLotState.Closed, ct)).Count;
            var rule = ReagentRegisterRule.EvaluateRetire(request.Reason, onHand);
            if (rule.Severity == RuleSeverity.HardStop)
            {
                return OperationResult<ReagentDto>.Fail(rule.Message);
            }
        }

        reagent.IsActive = request.Active;
        _reagents.Update(reagent);
        _audit.Record(request.Active ? AuditEventType.Activate : AuditEventType.Deactivate, nameof(Reagent), reagent.Id,
            new { IsActive = !request.Active }, new { reagent.IsActive },
            string.IsNullOrWhiteSpace(request.Reason) ? (request.Active ? "Reagent restored." : "Reagent retired.") : request.Reason.Trim());
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<ReagentDto>.Ok(new ReagentDto(reagent.Id, reagent.Code, reagent.Name, reagent.IsActive, reagent.TestCodes));
    }

    private async Task<string?> UnknownTestCodesAsync(string? codes, CancellationToken ct)
    {
        var parsed = ReagentLotUseRule.ParseTestCodes(codes);
        if (_tests is null || parsed.Count == 0)
        {
            return null;
        }

        var known = (await _tests.ListAsync(t => t.IsActive && !t.IsDraft, ct)).Select(t => t.Code.ToUpperInvariant()).ToHashSet();
        var missing = parsed.Where(c => !known.Contains(c)).ToList();
        return missing.Count == 0 ? null : $"Not an active test: {string.Join(", ", missing)}.";
    }

    /// <summary>
    /// Records the open, unexpired lots of the reagents used for a manual result's test against it (saved with the
    /// caller's next save). Returns what was recorded; nothing when no reagent lists the test.
    /// </summary>
    public async Task<LotUse> RecordLotsInUseAsync(TestResult result, CancellationToken ct = default)
    {
        if (_resultLots is null || result.Id == 0)
        {
            return LotUse.None;
        }

        var reagents = await _reagents.ListAsync(r => r.IsActive && r.TestCodes != null, ct);
        if (!reagents.Any(r => ReagentLotUseRule.UsedFor(r, result.TestCode)))
        {
            return LotUse.None;
        }

        var lots = await _lots.ListAsync(l => l.State == ReagentLotState.Opened, ct);
        var codes = reagents.ToDictionary(r => r.Id, r => r.Code);
        var now = _clock.UtcNow;
        var inUse = ReagentLotUseRule.LotsInUse(result.TestCode, reagents, lots, now);
        var inUseIds = inUse.Select(l => l.Id).ToList();
        var today = now.Date;
        var passedToday = (await _qc.ListAsync(q => inUseIds.Contains(q.ReagentLotId) && q.Passed, ct))
            .Where(q => q.QcDateUtc.Date == today)
            .Select(q => q.ReagentLotId)
            .ToHashSet();
        var recorded = inUse
            .Select(l => new TestResultReagentLot
            {
                TestResultId = result.Id,
                ReagentLotId = l.Id,
                ReagentCode = codes.GetValueOrDefault(l.ReagentId, string.Empty),
                LotNumber = l.LotNumber,
                RecordedUtc = now
            })
            .ToList();
        foreach (var row in recorded)
        {
            await _resultLots.AddAsync(row, ct);
        }

        var expiredOpen = lots.Where(l => l.ExpiresUtc <= now).ToList();
        return new LotUse(recorded, ReagentLotUseRule.EntryWarnings(result.TestCode, reagents, inUse, passedToday, expiredOpen));
    }

    /// <summary>The recorded lots of several results, as "AHG AHG-24033", for result lists.</summary>
    public async Task<IReadOnlyDictionary<long, IReadOnlyList<string>>> LotLabelsForResultsAsync(IEnumerable<long> resultIds, CancellationToken ct = default)
    {
        var ids = resultIds.Distinct().ToList();
        if (_resultLots is null || ids.Count == 0)
        {
            return new Dictionary<long, IReadOnlyList<string>>();
        }

        return (await _resultLots.ListAsync(r => ids.Contains(r.TestResultId), ct))
            .GroupBy(r => r.TestResultId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.OrderBy(r => r.ReagentCode).ThenBy(r => r.LotNumber).Select(r => $"{r.ReagentCode} {r.LotNumber}").ToList());
    }

    /// <summary>
    /// Records that a result tested with a lot whose QC failed has been reviewed (what was found or done), audited.
    /// The result itself is not changed: correct or invalidate it through the result workflow when needed.
    /// </summary>
    public async Task<OperationResult<ReagentLotResultDto>> ReviewLotResultAsync(long lotId, long resultId, string? note, CancellationToken ct = default)
    {
        if (_resultLots is null)
        {
            return OperationResult<ReagentLotResultDto>.Fail("Lot traceability is not available.");
        }

        var use = (await _resultLots.ListAsync(r => r.ReagentLotId == lotId && r.TestResultId == resultId, ct)).FirstOrDefault();
        if (use is null)
        {
            return OperationResult<ReagentLotResultDto>.Fail("That result is not recorded as tested with this lot.");
        }

        var since = ReagentLotUseRule.ReviewSinceUtc(await _qc.ListAsync(q => q.ReagentLotId == lotId, ct), out _);
        var check = ReagentLotUseRule.EvaluateReview(since is not null && use.RecordedUtc > since.Value, use.ReviewedUtc is not null, note);
        if (check.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<ReagentLotResultDto>.Fail($"{check.Code}: {check.Message}");
        }

        var tracked = await _resultLots.GetByIdAsync(use.Id, ct) ?? use;
        tracked.ReviewedUtc = _clock.UtcNow;
        tracked.ReviewedBy = _currentUser.UserName;
        tracked.ReviewNote = note!.Trim().Length <= 500 ? note.Trim() : note.Trim()[..500];
        _resultLots.Update(tracked);
        _audit.Record(AuditEventType.Update, nameof(TestResult), resultId,
            new { ReagentLot = $"{use.ReagentCode} {use.LotNumber}", Reviewed = false },
            new { ReagentLot = $"{use.ReagentCode} {use.LotNumber}", Reviewed = true, tracked.ReviewedBy },
            $"Reviewed after reagent lot {use.LotNumber} failed QC: {tracked.ReviewNote}");
        await _unitOfWork.SaveChangesAsync(ct);

        var rows = await ResultsForLotAsync(lotId, ct);
        var row = rows.Value?.FirstOrDefault(r => r.ResultId == resultId);
        return row is null
            ? OperationResult<ReagentLotResultDto>.Fail("Result not found.")
            : OperationResult<ReagentLotResultDto>.Ok(row);
    }

    /// <summary>The reagent lots recorded against a result.</summary>
    public async Task<IReadOnlyList<ResultReagentLotDto>> LotsForResultAsync(long resultId, CancellationToken ct = default) =>
        _resultLots is null
            ? []
            : (await _resultLots.ListAsync(r => r.TestResultId == resultId, ct))
                .OrderBy(r => r.ReagentCode).ThenBy(r => r.LotNumber)
                .Select(r => new ResultReagentLotDto(r.ReagentLotId, r.ReagentCode, r.LotNumber, r.RecordedUtc))
                .ToList();

    /// <summary>Lot lookback: every result recorded as tested with the lot, newest first, with patient and specimen.</summary>
    public async Task<OperationResult<IReadOnlyList<ReagentLotResultDto>>> ResultsForLotAsync(long lotId, CancellationToken ct = default)
    {
        if (await _lots.GetByIdAsync(lotId, ct) is null)
        {
            return OperationResult<IReadOnlyList<ReagentLotResultDto>>.Fail("Reagent lot not found.");
        }

        if (_resultLots is null || _results is null)
        {
            return OperationResult<IReadOnlyList<ReagentLotResultDto>>.Ok([]);
        }

        var uses = await _resultLots.ListAsync(r => r.ReagentLotId == lotId, ct);
        var resultIds = uses.Select(r => r.TestResultId).Distinct().ToList();
        var since = ReagentLotUseRule.ReviewSinceUtc(await _qc.ListAsync(q => q.ReagentLotId == lotId, ct), out _);
        var toReview = since is null
            ? new HashSet<long>()
            : uses.Where(u => u.RecordedUtc > since.Value && u.ReviewedUtc == null).Select(u => u.TestResultId).ToHashSet();
        var reviews = uses.Where(u => u.ReviewedUtc is not null).GroupBy(u => u.TestResultId).ToDictionary(g => g.Key, g => g.OrderByDescending(u => u.ReviewedUtc).First());
        var results = resultIds.Count == 0 ? [] : await _results.ListAsync(r => resultIds.Contains(r.Id), ct);
        var specimenIds = results.Select(r => r.SpecimenId).Distinct().ToList();
        var patientIds = results.Select(r => r.PatientId).Distinct().ToList();
        var specimens = _specimens is null || specimenIds.Count == 0
            ? new Dictionary<long, string>()
            : (await _specimens.ListAsync(s => specimenIds.Contains(s.Id), ct)).ToDictionary(s => s.Id, s => s.AccessionNumber);
        var patients = _patients is null || patientIds.Count == 0
            ? new Dictionary<long, Patient>()
            : (await _patients.ListAsync(p => patientIds.Contains(p.Id), ct)).ToDictionary(p => p.Id);
        IReadOnlyList<ReagentLotResultDto> rows = results
            .OrderBy(r => toReview.Contains(r.Id) ? 0 : 1)
            .ThenByDescending(r => r.EnteredUtc).ThenByDescending(r => r.Id)
            .Select(r =>
            {
                var patient = patients.GetValueOrDefault(r.PatientId);
                return new ReagentLotResultDto(
                    r.Id, r.PatientId, patient?.MedicalRecordNumber, patient is null ? null : $"{patient.LastName}, {patient.FirstName}",
                    specimens.GetValueOrDefault(r.SpecimenId), r.TestCode, r.Version, r.Status, r.EnteredUtc, r.EnteredBy, r.Value,
                    toReview.Contains(r.Id),
                    reviews.GetValueOrDefault(r.Id)?.ReviewedUtc,
                    reviews.GetValueOrDefault(r.Id)?.ReviewedBy,
                    reviews.GetValueOrDefault(r.Id)?.ReviewNote);
            })
            .ToList();
        return OperationResult<IReadOnlyList<ReagentLotResultDto>>.Ok(rows);
    }

    public async Task<OperationResult<ReagentDto>> SaveReagentAsync(SaveReagentRequest request, CancellationToken ct = default)
    {
        var code = request.Code?.Trim() ?? string.Empty;
        var name = request.Name?.Trim() ?? string.Empty;
        // A code already in the register, retired or not, is refused (Cycle 713): two reagents with one code would
        // split their lots and QC.
        var inUse = code.Length > 0
            && (await _reagents.ListAsync(ct)).Any(r => string.Equals(r.Code.Trim(), code, StringComparison.OrdinalIgnoreCase));
        var create = ReagentRegisterRule.EvaluateCreate(code, name, inUse);
        if (create.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<ReagentDto>.Fail(create.Message);
        }

        var unknown = await UnknownTestCodesAsync(request.TestCodes, ct);
        if (unknown is not null)
        {
            return OperationResult<ReagentDto>.Fail(unknown);
        }

        var reagent = new Reagent { Code = code, Name = name, IsActive = true, TestCodes = ReagentLotUseRule.NormalizeTestCodes(request.TestCodes) };
        await _reagents.AddAsync(reagent, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        _audit.Record(AuditEventType.Create, nameof(Reagent), reagent.Id, null, new { reagent.Code, reagent.Name, reagent.TestCodes }, "Reagent recorded.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<ReagentDto>.Ok(new ReagentDto(reagent.Id, reagent.Code, reagent.Name, reagent.IsActive, reagent.TestCodes));
    }

    public async Task<IReadOnlyList<ReagentLotDto>> ListLotsAsync(CancellationToken ct = default)
    {
        var lots = await _lots.ListAsync(ct);
        var reagents = (await _reagents.ListAsync(ct)).ToDictionary(r => r.Id);
        return lots
            .OrderBy(l => l.ExpiresUtc)
            .Select(l =>
            {
                reagents.TryGetValue(l.ReagentId, out var reagent);
                return new ReagentLotDto(
                    l.Id, l.ReagentId, reagent?.Code ?? string.Empty, l.LotNumber,
                    l.ReceivedUtc, l.OpenedUtc, l.ClosedUtc, l.ExpiresUtc, l.State);
            })
            .ToList();
    }

    public async Task<OperationResult<ReagentLotDto>> ReceiveLotAsync(ReceiveReagentLotRequest request, CancellationToken ct = default)
    {
        var lotNumber = request.LotNumber?.Trim() ?? string.Empty;
        if (request.ReagentId <= 0 || lotNumber.Length == 0)
        {
            return OperationResult<ReagentLotDto>.Fail("Reagent and lot number are required.");
        }

        var reagent = await _reagents.GetByIdAsync(request.ReagentId, ct);
        if (reagent is null || !reagent.IsActive)
        {
            return OperationResult<ReagentLotDto>.Fail("Reagent not found.");
        }

        var numberInUse = (await _lots.ListAsync(l => l.ReagentId == reagent.Id && l.State != ReagentLotState.Closed, ct))
            .Any(l => string.Equals(l.LotNumber.Trim(), lotNumber, StringComparison.OrdinalIgnoreCase));
        var receive = ReagentLotRule.EvaluateReceive(request.ExpiresUtc, _clock.UtcNow, numberInUse);
        if (receive.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<ReagentLotDto>.Fail(receive.Message);
        }

        var lot = new ReagentLot
        {
            ReagentId = reagent.Id,
            LotNumber = lotNumber,
            ReceivedUtc = _clock.UtcNow,
            ExpiresUtc = request.ExpiresUtc,
            State = ReagentLotState.Received
        };
        await _lots.AddAsync(lot, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        _audit.Record(
            AuditEventType.Create,
            nameof(ReagentLot),
            lot.Id,
            null,
            new { lot.LotNumber, lot.State, lot.ExpiresUtc },
            "Reagent lot received.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<ReagentLotDto>.Ok(new ReagentLotDto(
            lot.Id, lot.ReagentId, reagent.Code, lot.LotNumber, lot.ReceivedUtc, lot.OpenedUtc, lot.ClosedUtc, lot.ExpiresUtc, lot.State));
    }

    public async Task<OperationResult<ReagentLotDto>> OpenLotAsync(long lotId, CancellationToken ct = default)
    {
        var lot = await _lots.GetByIdAsync(lotId, ct);
        if (lot is null)
        {
            return OperationResult<ReagentLotDto>.Fail("Reagent lot not found.");
        }

        var open = ReagentLotRule.EvaluateOpen(lot.State, lot.ExpiresUtc, _clock.UtcNow);
        if (open.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<ReagentLotDto>.Fail(open.Message);
        }

        var before = new { lot.State, lot.OpenedUtc };
        lot.State = ReagentLotState.Opened;
        lot.OpenedUtc ??= _clock.UtcNow;
        _lots.Update(lot);
        _audit.Record(AuditEventType.Update, nameof(ReagentLot), lot.Id, before, new { lot.State, lot.OpenedUtc, lot.LotNumber }, "Reagent lot opened.");
        await _unitOfWork.SaveChangesAsync(ct);
        return await LotResultAsync(lot, ct);
    }

    public async Task<OperationResult<ReagentLotDto>> CloseLotAsync(long lotId, CancellationToken ct = default)
    {
        var lot = await _lots.GetByIdAsync(lotId, ct);
        if (lot is null)
        {
            return OperationResult<ReagentLotDto>.Fail("Reagent lot not found.");
        }

        if (lot.State == ReagentLotState.Closed)
        {
            return OperationResult<ReagentLotDto>.Fail("This lot is already closed.");
        }

        var before = new { lot.State, lot.ClosedUtc };
        lot.State = ReagentLotState.Closed;
        lot.ClosedUtc = _clock.UtcNow;
        _lots.Update(lot);
        _audit.Record(AuditEventType.Update, nameof(ReagentLot), lot.Id, before, new { lot.State, lot.ClosedUtc, lot.LotNumber }, "Reagent lot closed.");
        await _unitOfWork.SaveChangesAsync(ct);
        return await LotResultAsync(lot, ct);
    }

    public async Task<OperationResult<ReagentDailyQcDto>> RecordQcAsync(RecordReagentQcRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ExpectedResult) || string.IsNullOrWhiteSpace(request.ObservedResult))
        {
            return OperationResult<ReagentDailyQcDto>.Fail("Expected and observed results are required.");
        }

        var lot = await _lots.GetByIdAsync(request.ReagentLotId, ct);
        if (lot is null)
        {
            return OperationResult<ReagentDailyQcDto>.Fail("Record QC on an open or received lot.");
        }

        var qcCheck = ReagentLotRule.EvaluateQc(lot.State, lot.ExpiresUtc, _clock.UtcNow);
        if (qcCheck.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<ReagentDailyQcDto>.Fail(qcCheck.Message);
        }

        var now = _clock.UtcNow;
        var row = new ReagentDailyQc
        {
            ReagentLotId = lot.Id,
            QcDateUtc = now.Date,
            ExpectedResult = request.ExpectedResult.Trim(),
            ObservedResult = request.ObservedResult.Trim(),
            Passed = request.Passed,
            PerformedBy = _currentUser.UserName,
            PerformedUtc = now
        };
        await _qc.AddAsync(row, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        _audit.Record(
            AuditEventType.Create,
            nameof(ReagentDailyQc),
            row.Id,
            null,
            new { row.ExpectedResult, row.ObservedResult, row.Passed, row.PerformedBy },
            "Reagent QC recorded.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<ReagentDailyQcDto>.Ok(ToDto(row, lot.LotNumber));
    }

    public async Task<OperationResult<ReagentDailyQcDto>> ReviewAsync(long qcId, CancellationToken ct = default)
    {
        var row = await _qc.GetByIdAsync(qcId, ct);
        if (row is null)
        {
            return OperationResult<ReagentDailyQcDto>.Fail("QC row not found.");
        }

        var review = ReagentQcReviewRule.Evaluate(row.PerformedBy, _currentUser.UserName);
        if (review.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<ReagentDailyQcDto>.Fail(review.Message);
        }

        row.ReviewedBy = _currentUser.UserName;
        row.ReviewedUtc = _clock.UtcNow;
        _audit.Record(
            AuditEventType.Verify,
            nameof(ReagentDailyQc),
            row.Id,
            new { row.PerformedBy },
            new { row.ReviewedBy },
            "Reagent QC reviewed.");
        await _unitOfWork.SaveChangesAsync(ct);
        var lot = await _lots.GetByIdAsync(row.ReagentLotId, ct);
        return OperationResult<ReagentDailyQcDto>.Ok(ToDto(row, lot?.LotNumber ?? string.Empty));
    }

    public async Task<IReadOnlyList<ReagentQcWorkItemDto>> ListWorkAsync(CancellationToken ct = default)
    {
        var lots = await _lots.ListAsync(ct);
        var rows = await _qc.ListAsync(ct);
        var reagentCodes = (await _reagents.ListAsync(ct)).ToDictionary(r => r.Id, r => r.Code);
        var facts = lots
            .Select(l => new ReagentQcLotFact(l.Id, l.LotNumber, l.State, l.ExpiresUtc, l.ReagentId, reagentCodes.GetValueOrDefault(l.ReagentId)))
            .ToList();
        var qcFacts = rows.Select(q => new ReagentQcFact(q.Id, q.ReagentLotId, q.QcDateUtc, q.Passed, q.ReviewedBy)).ToList();
        var lotNumbers = lots.ToDictionary(l => l.Id, l => l.LotNumber);
        var items = ReagentQcWorklistRule.Evaluate(facts, qcFacts, _clock.UtcNow)
            .Select(item => new ReagentQcWorkItemDto(
                item.Kind,
                item.LotId,
                lotNumbers.TryGetValue(item.LotId, out var number) ? number : string.Empty,
                item.QcId,
                item.Message))
            .ToList();
        if (_resultLots is null || !items.Any(i => i.Kind == ReagentQcWorklistRule.Failed))
        {
            return items;
        }

        // A failed lot names how many results were tested with it since it last passed.
        var result = new List<ReagentQcWorkItemDto>();
        foreach (var item in items)
        {
            if (item.Kind != ReagentQcWorklistRule.Failed)
            {
                result.Add(item);
                continue;
            }

            var since = ReagentLotUseRule.ReviewSinceUtc(rows.Where(q => q.ReagentLotId == item.LotId), out _);
            var count = since is null
                ? 0
                : (await _resultLots.ListAsync(r => r.ReagentLotId == item.LotId && r.RecordedUtc > since.Value && r.ReviewedUtc == null, ct))
                    .Select(r => r.TestResultId).Distinct().Count();
            result.Add(item with { ResultsToReview = count });
        }

        return result;
    }

    public async Task<bool> HasOpenWorkAsync(CancellationToken ct = default) =>
        (await ListWorkAsync(ct)).Count > 0;

    private async Task<OperationResult<ReagentLotDto>> LotResultAsync(ReagentLot lot, CancellationToken ct)
    {
        var reagent = await _reagents.GetByIdAsync(lot.ReagentId, ct);
        return OperationResult<ReagentLotDto>.Ok(new ReagentLotDto(
            lot.Id, lot.ReagentId, reagent?.Code ?? string.Empty, lot.LotNumber,
            lot.ReceivedUtc, lot.OpenedUtc, lot.ClosedUtc, lot.ExpiresUtc, lot.State));
    }

    /// <summary>QC recorded in the last <paramref name="days"/> days, newest first, with its lot and reagent.</summary>
    public async Task<IReadOnlyList<ReagentDailyQcDto>> ListRecentQcAsync(int days = 14, CancellationToken ct = default)
    {
        var since = _clock.UtcNow.AddDays(-Math.Clamp(days, 1, 90));
        var rows = await _qc.ListAsync(q => q.PerformedUtc >= since, ct);
        if (rows.Count == 0)
        {
            return [];
        }

        var lotIds = rows.Select(q => q.ReagentLotId).Distinct().ToList();
        var lots = (await _lots.ListAsync(l => lotIds.Contains(l.Id), ct)).ToDictionary(l => l.Id);
        var reagentIds = lots.Values.Select(l => l.ReagentId).Distinct().ToList();
        var reagents = (await _reagents.ListAsync(r => reagentIds.Contains(r.Id), ct)).ToDictionary(r => r.Id);
        return rows
            .OrderByDescending(q => q.PerformedUtc)
            .ThenByDescending(q => q.Id)
            .Select(q =>
            {
                lots.TryGetValue(q.ReagentLotId, out var lot);
                var reagent = lot is not null && reagents.TryGetValue(lot.ReagentId, out var r) ? r : null;
                return ToDto(q, lot?.LotNumber ?? string.Empty) with { ReagentId = lot?.ReagentId, ReagentCode = reagent?.Code };
            })
            .ToList();
    }

    private static ReagentDailyQcDto ToDto(ReagentDailyQc row, string lotNumber) =>
        new(row.Id, row.ReagentLotId, lotNumber, row.QcDateUtc, row.ExpectedResult, row.ObservedResult,
            row.Passed, row.PerformedBy, row.PerformedUtc, row.ReviewedBy, row.ReviewedUtc);
}
