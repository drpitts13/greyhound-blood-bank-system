using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

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
    private readonly IPermissionEvaluator? _permissions;

    public DeviationService(
        IRepository<Deviation> deviations,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IPermissionEvaluator? permissions = null)
    {
        _deviations = deviations;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _permissions = permissions;
    }

    public Task<IReadOnlyList<Deviation>> ListAsync(CancellationToken ct = default) =>
        _deviations.ListAsync(ct);

    public async Task<OperationResult<Deviation>> CreateAsync(CreateDeviationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

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
        var denied = await RejectUnauthorizedAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

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

    private async Task<OperationResult<Deviation>?> RejectUnauthorizedAsync(CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(
            _currentUser.UserName, PermissionCodes.DeviationManage, ct);
        var auth = DeviationAuthorizationRule.EvaluateManage(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? OperationResult<Deviation>.Fail(auth.Message)
            : null;
    }
}
