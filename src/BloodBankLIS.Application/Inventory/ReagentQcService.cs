using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Inventory;

public sealed record ReagentDto(long Id, string Code, string Name, bool IsActive);

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
    DateTime? ReviewedUtc);

public sealed record ReagentQcWorkItemDto(string Kind, long LotId, string LotNumber, long? QcId, string Message);

public sealed record SaveReagentRequest(string Code, string Name);

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

    public ReagentQcService(
        IRepository<Reagent> reagents,
        IRepository<ReagentLot> lots,
        IRepository<ReagentDailyQc> qc,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit)
    {
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
            .Select(r => new ReagentDto(r.Id, r.Code, r.Name, r.IsActive))
            .ToList();
    }

    public async Task<OperationResult<ReagentDto>> SaveReagentAsync(SaveReagentRequest request, CancellationToken ct = default)
    {
        var code = request.Code?.Trim() ?? string.Empty;
        var name = request.Name?.Trim() ?? string.Empty;
        if (code.Length == 0 || name.Length == 0)
        {
            return OperationResult<ReagentDto>.Fail("Reagent code and name are required.");
        }

        var reagent = new Reagent { Code = code, Name = name, IsActive = true };
        await _reagents.AddAsync(reagent, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        _audit.Record(AuditEventType.Create, nameof(Reagent), reagent.Id, null, new { reagent.Code, reagent.Name }, "Reagent recorded.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<ReagentDto>.Ok(new ReagentDto(reagent.Id, reagent.Code, reagent.Name, reagent.IsActive));
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

        if (lot.State == ReagentLotState.Closed)
        {
            return OperationResult<ReagentLotDto>.Fail("A closed lot cannot be opened.");
        }

        lot.State = ReagentLotState.Opened;
        lot.OpenedUtc ??= _clock.UtcNow;
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

        lot.State = ReagentLotState.Closed;
        lot.ClosedUtc = _clock.UtcNow;
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
        if (lot is null || lot.State == ReagentLotState.Closed)
        {
            return OperationResult<ReagentDailyQcDto>.Fail("Record QC on an open or received lot.");
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
        var facts = lots.Select(l => new ReagentQcLotFact(l.Id, l.LotNumber, l.State, l.ExpiresUtc)).ToList();
        var qcFacts = rows.Select(q => new ReagentQcFact(q.Id, q.ReagentLotId, q.QcDateUtc, q.Passed, q.ReviewedBy)).ToList();
        var lotNumbers = lots.ToDictionary(l => l.Id, l => l.LotNumber);
        return ReagentQcWorklistRule.Evaluate(facts, qcFacts, _clock.UtcNow)
            .Select(item => new ReagentQcWorkItemDto(
                item.Kind,
                item.LotId,
                lotNumbers.TryGetValue(item.LotId, out var number) ? number : string.Empty,
                item.QcId,
                item.Message))
            .ToList();
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

    private static ReagentDailyQcDto ToDto(ReagentDailyQc row, string lotNumber) =>
        new(row.Id, row.ReagentLotId, lotNumber, row.QcDateUtc, row.ExpectedResult, row.ObservedResult,
            row.Passed, row.PerformedBy, row.PerformedUtc, row.ReviewedBy, row.ReviewedUtc);
}
