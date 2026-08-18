using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Compliance;

public sealed record CreateDeviationRequest(
    string Title,
    string Description,
    DeviationSeverity Severity,
    string? ContextType = null,
    long? ContextId = null);

public sealed record DeviationDto(
    long Id,
    string Title,
    string Description,
    DeviationSeverity Severity,
    DeviationStatus Status,
    string? ContextType,
    long? ContextId,
    string? CorrectiveAction,
    string ReportedBy,
    DateTime ReportedUtc)
{
    public static DeviationDto From(Deviation d) => new(
        d.Id, d.Title, d.Description, d.Severity, d.Status, d.ContextType, d.ContextId,
        d.CorrectiveAction, d.ReportedBy, d.ReportedUtc);
}

public sealed class DeviationService
{
    private readonly IRepository<Deviation> _deviations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;

    public DeviationService(
        IRepository<Deviation> deviations,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit)
    {
        _deviations = deviations;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
    }

    public Task<IReadOnlyList<Deviation>> ListAsync(CancellationToken ct = default) =>
        _deviations.ListAsync(ct);

    public async Task<OperationResult<Deviation>> CreateAsync(CreateDeviationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
        {
            return OperationResult<Deviation>.Fail("Title and description are required.");
        }

        var row = new Deviation
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Severity = request.Severity,
            ContextType = request.ContextType,
            ContextId = request.ContextId,
            ReportedBy = _currentUser.UserName,
            ReportedUtc = _clock.UtcNow
        };
        await _deviations.AddAsync(row, ct);
        _audit.Record(AuditEventType.Deviation, nameof(Deviation), null, newValue: new { request.Title, request.Severity });
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<Deviation>.Ok(row);
    }

    public async Task<OperationResult<Deviation>> UpdateStatusAsync(
        long id, DeviationStatus status, string? correctiveAction, CancellationToken ct = default)
    {
        var row = await _deviations.GetByIdAsync(id, ct);
        if (row is null)
        {
            return OperationResult<Deviation>.Fail("Deviation not found.");
        }

        row.Status = status;
        if (correctiveAction is not null)
        {
            row.CorrectiveAction = correctiveAction;
        }

        if (status == DeviationStatus.Closed)
        {
            row.ClosedBy = _currentUser.UserName;
            row.ClosedUtc = _clock.UtcNow;
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<Deviation>.Ok(row);
    }
}
