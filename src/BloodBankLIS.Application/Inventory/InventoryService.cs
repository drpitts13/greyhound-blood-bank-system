using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Inventory;

/// <summary>
/// Inventory workflows for Phase 2: intake, release, transfer, discard, expiration,
/// search, and status history. Every status/location change is guarded by
/// <see cref="InventoryStatusTransition"/>, recorded in append-only history, and
/// audited (automatic Create/Update audit via the persistence pipeline; named
/// audit events for dangerous actions such as discard).
/// </summary>
public sealed class InventoryService
{
    private static readonly UnitStatus[] TransferableStatuses =
    {
        UnitStatus.Quarantine, UnitStatus.Available, UnitStatus.Allocated, UnitStatus.Returned
    };

    private readonly IInventoryRepository _repository;
    private readonly IRepository<UnitBloodAttribute> _unitAttributes;
    private readonly IRepository<BloodAttributeDefinition> _bloodAttributes;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;

    public InventoryService(
        IInventoryRepository repository,
        IRepository<UnitBloodAttribute> unitAttributes,
        IRepository<BloodAttributeDefinition> bloodAttributes,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit)
    {
        _repository = repository;
        _unitAttributes = unitAttributes;
        _bloodAttributes = bloodAttributes;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
    }

    public Task<IReadOnlyList<BloodUnit>> SearchAsync(InventorySearchCriteria criteria, CancellationToken ct = default) =>
        _repository.SearchAsync(criteria, ct);

    public Task<BloodUnit?> GetAsync(long id, CancellationToken ct = default) =>
        _repository.GetUnitAsync(id, ct);

    public Task<IReadOnlyList<InventoryStatusHistory>> GetHistoryAsync(long unitId, CancellationToken ct = default) =>
        _repository.GetHistoryAsync(unitId, ct);

    public Task<IReadOnlyList<UnitBloodAttribute>> GetBloodAttributesAsync(long unitId, CancellationToken ct = default) =>
        _unitAttributes.ListAsync(a => a.BloodProductId == unitId, ct);

    public async Task<OperationResult<UnitBloodAttribute>> SaveBloodAttributeAsync(
        long unitId, SaveUnitBloodAttributeRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await _repository.GetUnitAsync(unitId, ct) is null)
        {
            return OperationResult<UnitBloodAttribute>.Fail("Unit not found.");
        }

        var definition = await _bloodAttributes.GetByIdAsync(request.BloodAttributeDefinitionId, ct);
        if (definition is null || !definition.IsActive)
        {
            return OperationResult<UnitBloodAttribute>.Fail("Blood attribute definition not found or inactive.");
        }

        var existing = await _unitAttributes.FirstOrDefaultAsync(
            a => a.BloodProductId == unitId
                 && a.BloodAttributeDefinitionId == request.BloodAttributeDefinitionId
                 && a.AttributeKind == request.AttributeKind, ct);

        if (existing is null)
        {
            existing = new UnitBloodAttribute
            {
                BloodProductId = unitId,
                BloodAttributeDefinitionId = request.BloodAttributeDefinitionId,
                AttributeKind = request.AttributeKind,
                Result = request.Result
            };
            await _unitAttributes.AddAsync(existing, ct);
        }
        else
        {
            existing.Result = request.Result;
            _unitAttributes.Update(existing);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<UnitBloodAttribute>.Ok(existing);
    }

    /// <summary>Receives a new unit into Quarantine and records the initial status history.</summary>
    public async Task<InventoryActionResult> ReceiveUnitAsync(ReceiveUnitRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.UnitNumber))
        {
            return InventoryActionResult.Fail("Unit number is required.");
        }

        if (request.ExpiresUtc <= _clock.UtcNow)
        {
            return InventoryActionResult.Fail("Expiration date/time must be in the future.");
        }

        if (await _repository.UnitNumberExistsAsync(request.UnitNumber, ct))
        {
            return InventoryActionResult.Fail($"A unit with number '{request.UnitNumber}' already exists.");
        }

        var unit = new BloodUnit
        {
            UnitNumber = request.UnitNumber,
            ProductTypeId = request.ProductTypeId,
            Abo = request.Abo,
            RhD = request.RhD,
            ExpiresUtc = request.ExpiresUtc,
            CurrentLocationId = request.LocationId,
            CollectionFacility = request.CollectionFacility,
            Supplier = request.Supplier,
            Isbt128ProductCode = request.Isbt128ProductCode,
            Isbt128DonationId = request.Isbt128DonationId,
            Volume = request.Volume,
            Status = UnitStatus.Quarantine
        };

        await _repository.AddUnitAsync(unit, ct);

        // Link by navigation so the FK is fixed up after the unit's Id is generated,
        // keeping the unit and its first history row in one atomic save.
        _repository.AddStatusHistory(new InventoryStatusHistory
        {
            Unit = unit,
            FromStatus = null,
            ToStatus = UnitStatus.Quarantine,
            ToLocationId = request.LocationId,
            Reason = "Initial intake",
            ChangedBy = _currentUser.UserName,
            ChangedUtc = _clock.UtcNow
        });

        await _unitOfWork.SaveChangesAsync(ct);
        return InventoryActionResult.Ok(unit);
    }

    public async Task<InventoryActionResult> ReleaseFromQuarantineAsync(long unitId, CancellationToken ct = default)
    {
        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
        {
            return InventoryActionResult.Fail("Unit not found.");
        }

        return await ChangeStatusAsync(unit, UnitStatus.Available, "Released from quarantine", ct);
    }

    public async Task<InventoryActionResult> TransferAsync(long unitId, long toLocationId, string? reason, CancellationToken ct = default)
    {
        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
        {
            return InventoryActionResult.Fail("Unit not found.");
        }

        if (!TransferableStatuses.Contains(unit.Status))
        {
            return InventoryActionResult.Fail($"A unit with status {unit.Status} cannot be transferred.");
        }

        var fromLocationId = unit.CurrentLocationId;
        unit.CurrentLocationId = toLocationId;

        _repository.AddStatusHistory(new InventoryStatusHistory
        {
            BloodProductId = unit.Id,
            FromStatus = unit.Status,
            ToStatus = unit.Status,
            FromLocationId = fromLocationId,
            ToLocationId = toLocationId,
            Reason = reason ?? "Location transfer",
            ChangedBy = _currentUser.UserName,
            ChangedUtc = _clock.UtcNow
        });

        await _unitOfWork.SaveChangesAsync(ct);
        return InventoryActionResult.Ok(unit);
    }

    /// <summary>
    /// Dangerous action: requires a reason and is recorded as a named Discard audit
    /// event in addition to the automatic Update audit (see docs/safety-rules.md 5).
    /// </summary>
    public async Task<InventoryActionResult> DiscardAsync(long unitId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return InventoryActionResult.Fail("A reason is required to discard a unit.");
        }

        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
        {
            return InventoryActionResult.Fail("Unit not found.");
        }

        var transition = InventoryStatusTransition.Evaluate(unit.Status, UnitStatus.Discarded);
        if (transition.Severity == RuleSeverity.HardStop)
        {
            return InventoryActionResult.Blocked(new RuleEvaluation(new[] { transition }));
        }

        var fromStatus = unit.Status;
        unit.Status = UnitStatus.Discarded;
        unit.DiscardReason = reason;

        _repository.AddStatusHistory(new InventoryStatusHistory
        {
            BloodProductId = unit.Id,
            FromStatus = fromStatus,
            ToStatus = UnitStatus.Discarded,
            FromLocationId = unit.CurrentLocationId,
            ToLocationId = unit.CurrentLocationId,
            Reason = reason,
            ChangedBy = _currentUser.UserName,
            ChangedUtc = _clock.UtcNow
        });

        _audit.Record(
            AuditEventType.Discard,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = fromStatus },
            newValue: new { Status = UnitStatus.Discarded, DiscardReason = reason },
            reason: reason);

        await _unitOfWork.SaveChangesAsync(ct);
        return InventoryActionResult.Ok(unit);
    }

    /// <summary>Sweeps units that are at/past expiration into the Expired status.</summary>
    public async Task<int> ExpireDueUnitsAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var due = await _repository.GetExpirableUnitsAsync(now, ct);
        if (due.Count == 0)
        {
            return 0;
        }

        foreach (var unit in due)
        {
            if (!InventoryStatusTransition.IsAllowed(unit.Status, UnitStatus.Expired))
            {
                continue;
            }

            var fromStatus = unit.Status;
            unit.Status = UnitStatus.Expired;

            _repository.AddStatusHistory(new InventoryStatusHistory
            {
                BloodProductId = unit.Id,
                FromStatus = fromStatus,
                ToStatus = UnitStatus.Expired,
                FromLocationId = unit.CurrentLocationId,
                ToLocationId = unit.CurrentLocationId,
                Reason = $"Expired at {unit.ExpiresUtc:u}",
                ChangedBy = _currentUser.UserName,
                ChangedUtc = now
            });
        }

        return await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<InventoryActionResult> ChangeStatusAsync(BloodUnit unit, UnitStatus toStatus, string reason, CancellationToken ct)
    {
        var transition = InventoryStatusTransition.Evaluate(unit.Status, toStatus);
        if (transition.Severity == RuleSeverity.HardStop)
        {
            return InventoryActionResult.Blocked(new RuleEvaluation(new[] { transition }));
        }

        var fromStatus = unit.Status;
        unit.Status = toStatus;

        _repository.AddStatusHistory(new InventoryStatusHistory
        {
            BloodProductId = unit.Id,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            FromLocationId = unit.CurrentLocationId,
            ToLocationId = unit.CurrentLocationId,
            Reason = reason,
            ChangedBy = _currentUser.UserName,
            ChangedUtc = _clock.UtcNow
        });

        await _unitOfWork.SaveChangesAsync(ct);
        return InventoryActionResult.Ok(unit);
    }
}
