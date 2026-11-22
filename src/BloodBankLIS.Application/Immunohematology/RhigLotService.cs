using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Immunohematology;

public sealed record RhigLotDto(
    long Id,
    string LotNumber,
    string ProductName,
    DateOnly ExpiresOn,
    int VialsReceived,
    int VialsGiven,
    DateTime ReceivedUtc,
    string ReceivedBy,
    bool IsActive,
    string? RetiredReason)
{
    public int VialsLeft => Math.Max(0, VialsReceived - VialsGiven);
}

public sealed record ReceiveRhigLotRequest(string LotNumber, string ProductName, DateOnly ExpiresOn, int VialsReceived);

public sealed record RetireRhigLotRequest(string? Reason);

/// <summary>
/// The RhIG lot register (end-user testing, 2026-11-12): lots received, vials given from each, and retiring a lot
/// (used up, recalled, expired). Receiving and retiring are issue work (issue.create), audited; nothing is deleted.
/// </summary>
public sealed class RhigLotService
{
    private readonly IRepository<RhigLot> _lots;
    private readonly IRepository<RhigEvaluation> _evaluations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;

    // The facility's calendar day for "today" (end-user testing pass 69: the UTC day turned over at 19:00 Eastern).
    private readonly IFacilityTimeZone? _facilityTimeZone;

    public RhigLotService(
        IRepository<RhigLot> lots,
        IRepository<RhigEvaluation> evaluations,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IFacilityTimeZone? facilityTimeZone = null)
    {
        _facilityTimeZone = facilityTimeZone;
        _lots = lots;
        _evaluations = evaluations;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<IReadOnlyList<RhigLotDto>> ListAsync(bool includeRetired = false, CancellationToken ct = default)
    {
        var lots = includeRetired ? await _lots.ListAsync(ct) : await _lots.ListAsync(l => l.IsActive, ct);
        if (lots.Count == 0)
        {
            return [];
        }

        var given = (await _evaluations.ListAsync(e => e.GivenLot != null, ct))
            .GroupBy(e => e.GivenLot!.Trim().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.Sum(e => e.GivenVials ?? 0));
        return lots
            .OrderBy(l => l.IsActive ? 0 : 1)
            .ThenBy(l => l.ExpiresOn)
            .ThenBy(l => l.LotNumber)
            .Select(l => new RhigLotDto(l.Id, l.LotNumber, l.ProductName, l.ExpiresOn, l.VialsReceived,
                given.GetValueOrDefault(l.LotNumber.Trim().ToUpperInvariant()), l.ReceivedUtc, l.ReceivedBy, l.IsActive, l.RetiredReason))
            .ToList();
    }

    public async Task<OperationResult<RhigLotDto>> ReceiveAsync(ReceiveRhigLotRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var lot = request.LotNumber?.Trim() ?? "";
        var upper = lot.ToUpperInvariant();
        var inUse = lot.Length > 0 && (await _lots.ListAsync(ct)).Any(l => l.LotNumber.Trim().ToUpperInvariant() == upper);
        var rule = RhigLotRule.EvaluateReceive(lot, request.ProductName, request.ExpiresOn, request.VialsReceived,
            FacilityDay.Of(_clock.UtcNow, _facilityTimeZone), inUse);
        if (rule.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<RhigLotDto>.Fail(rule.Message);
        }

        var row = new RhigLot
        {
            LotNumber = lot,
            ProductName = request.ProductName.Trim(),
            ExpiresOn = request.ExpiresOn,
            VialsReceived = request.VialsReceived,
            ReceivedUtc = _clock.UtcNow,
            ReceivedBy = _currentUser.UserName,
            IsActive = true
        };
        await _lots.AddAsync(row, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        _audit.Record(AuditEventType.Create, nameof(RhigLot), row.Id, null,
            new { row.LotNumber, row.ProductName, row.ExpiresOn, row.VialsReceived }, "RhIG lot received.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<RhigLotDto>.Ok((await ListAsync(includeRetired: true, ct)).Single(l => l.Id == row.Id));
    }

    public async Task<OperationResult<RhigLotDto>> RetireAsync(long id, RetireRhigLotRequest request, CancellationToken ct = default)
    {
        var row = await _lots.GetByIdAsync(id, ct);
        if (row is null || !row.IsActive)
        {
            return OperationResult<RhigLotDto>.Fail("Active RhIG lot not found.");
        }

        if (string.IsNullOrWhiteSpace(request?.Reason))
        {
            return OperationResult<RhigLotDto>.Fail("Say why the lot is retired (for example, used up, recalled, expired).");
        }

        row.IsActive = false;
        row.RetiredReason = request.Reason.Trim();
        _lots.Update(row);
        _audit.Record(AuditEventType.Deactivate, nameof(RhigLot), row.Id, new { IsActive = true }, new { IsActive = false, row.RetiredReason },
            row.RetiredReason);
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<RhigLotDto>.Ok((await ListAsync(includeRetired: true, ct)).Single(l => l.Id == row.Id));
    }
}
