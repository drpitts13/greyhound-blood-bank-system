using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Issuing;

/// <summary>The ward asks for a reserved unit to be issued, and where it should go.</summary>
public sealed record IssueRequestRequest(string? Destination);

public sealed record CancelIssueRequestRequest(string? Reason);

/// <summary>An open request to issue a reserved unit.</summary>
public sealed record IssueRequestDto(long AllocationId, DateTime RequestedUtc, string RequestedBy, string Destination);

/// <summary>
/// The ward's request to send a reserved unit now. It moves the reservation to the top of Ready to issue, fills in the
/// destination on the issue form, and closes when the unit is issued (the reservation is no longer reserved) or is
/// cancelled with a reason. Requesting checks nothing clinical: issue still runs every check.
/// </summary>
public sealed class IssueRequestService
{
    private readonly IRepository<Allocation> _allocations;
    private readonly IPermissionEvaluator _permissions;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;
    private readonly IUnitOfWork _unitOfWork;

    public IssueRequestService(
        IRepository<Allocation> allocations,
        IPermissionEvaluator permissions,
        ICurrentUser currentUser,
        IClock clock,
        IAuditWriter audit,
        IUnitOfWork unitOfWork)
    {
        _allocations = allocations;
        _permissions = permissions;
        _currentUser = currentUser;
        _clock = clock;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    // Ward staff who document transfusions, or the issue desk itself, may request.
    private async Task<bool> MayRequestAsync(CancellationToken ct) =>
        await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.TransfusionDocument, ct)
        || await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.IssueCreate, ct);

    public async Task<OperationResult<IssueRequestDto>> RequestAsync(long allocationId, IssueRequestRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await MayRequestAsync(ct))
        {
            return OperationResult<IssueRequestDto>.Fail("Requesting an issue needs the transfusion documentation or issue permission.");
        }

        if (string.IsNullOrWhiteSpace(request.Destination))
        {
            return OperationResult<IssueRequestDto>.Fail("Say where the unit should go.");
        }

        var allocation = await _allocations.GetByIdAsync(allocationId, ct);
        if (allocation is null)
        {
            return OperationResult<IssueRequestDto>.Fail("Reservation not found.");
        }

        if (allocation.Status != AllocationStatus.Reserved)
        {
            return OperationResult<IssueRequestDto>.Fail($"This unit is {allocation.Status}, not reserved; it cannot be requested.");
        }

        if (allocation.IssueRequestedUtc is DateTime already)
        {
            return OperationResult<IssueRequestDto>.Fail(
                $"Already requested at {already:yyyy-MM-dd HH:mm} UTC by {allocation.IssueRequestedBy} for {allocation.IssueRequestedTo}. Cancel that request to change it.");
        }

        allocation.IssueRequestedUtc = _clock.UtcNow;
        allocation.IssueRequestedBy = _currentUser.UserName;
        allocation.IssueRequestedTo = request.Destination.Trim();
        _allocations.Update(allocation);
        _audit.Record(
            AuditEventType.Assignment,
            nameof(Allocation),
            allocation.Id,
            newValue: new
            {
                allocation.PatientId,
                allocation.BloodProductId,
                allocation.IssueRequestedUtc,
                allocation.IssueRequestedBy,
                allocation.IssueRequestedTo
            },
            reason: $"Issue requested to {allocation.IssueRequestedTo}.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<IssueRequestDto>.Ok(new IssueRequestDto(
            allocation.Id, allocation.IssueRequestedUtc.Value, allocation.IssueRequestedBy, allocation.IssueRequestedTo));
    }

    public async Task<OperationResult<bool>> CancelAsync(long allocationId, CancelIssueRequestRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await MayRequestAsync(ct))
        {
            return OperationResult<bool>.Fail("Cancelling an issue request needs the transfusion documentation or issue permission.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return OperationResult<bool>.Fail("A reason is required to cancel an issue request.");
        }

        var allocation = await _allocations.GetByIdAsync(allocationId, ct);
        if (allocation?.IssueRequestedUtc is null)
        {
            return OperationResult<bool>.Fail("There is no open issue request for this reservation.");
        }

        var before = new { allocation.IssueRequestedUtc, allocation.IssueRequestedBy, allocation.IssueRequestedTo };
        allocation.IssueRequestedUtc = null;
        allocation.IssueRequestedBy = null;
        allocation.IssueRequestedTo = null;
        _allocations.Update(allocation);
        _audit.Record(
            AuditEventType.Assignment,
            nameof(Allocation),
            allocation.Id,
            oldValue: before,
            newValue: new { IssueRequestedUtc = (DateTime?)null },
            reason: $"Issue request cancelled: {request.Reason.Trim()}");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<bool>.Ok(true);
    }
}
