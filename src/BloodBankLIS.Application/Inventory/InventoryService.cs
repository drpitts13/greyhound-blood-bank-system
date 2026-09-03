using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Isbt128;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Domain.Isbt128.Validation;
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
        UnitStatus.Quarantine, UnitStatus.Available, UnitStatus.Allocated, UnitStatus.Returned,
        UnitStatus.Received, UnitStatus.Selected, UnitStatus.Assigned, UnitStatus.Crossmatched,
        UnitStatus.Transferred, UnitStatus.CancelledAssignment, UnitStatus.OnHold
    };

    private readonly IInventoryRepository _repository;
    private readonly IRepository<UnitBloodAttribute> _unitAttributes;
    private readonly IRepository<BloodAttributeDefinition> _bloodAttributes;
    private readonly IsbtLookupCatalog _lookups;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IRepository<User>? _users;
    private readonly FacilityPolicyService? _policy;

    public InventoryService(
        IInventoryRepository repository,
        IRepository<UnitBloodAttribute> unitAttributes,
        IRepository<BloodAttributeDefinition> bloodAttributes,
        IsbtLookupCatalog lookups,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IRepository<User>? users = null,
        FacilityPolicyService? policy = null)
    {
        _repository = repository;
        _unitAttributes = unitAttributes;
        _bloodAttributes = bloodAttributes;
        _lookups = lookups;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _users = users;
        _policy = policy;
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

        var visual = await EvaluateReceiveVisualAsync(request.VisualInspectionAcceptable, ct);
        if (visual.Severity == RuleSeverity.HardStop)
        {
            return InventoryActionResult.Blocked(new RuleEvaluation([visual]));
        }

        if (string.IsNullOrWhiteSpace(request.UnitNumber))
        {
            return InventoryActionResult.Fail("Unit number is required.");
        }

        if (request.ExpiresUtc <= _clock.UtcNow)
        {
            return InventoryActionResult.Fail("Expiration date/time must be in the future.");
        }

        var productLookup = await _lookups.GetProductLookupAsync(ct);
        var productValidation = ProductCodeLookupValidator.Validate(
            request.Isbt128ProductCode,
            productLookup,
            DateOnly.FromDateTime(_clock.UtcNow));
        if (!productValidation.Success)
        {
            return InventoryActionResult.Fail(productValidation.Error!);
        }

        if (await _repository.UnitNumberExistsAsync(request.UnitNumber, ct))
        {
            return InventoryActionResult.Fail($"A unit with number '{request.UnitNumber}' already exists.");
        }

        var resolved = productValidation.Value!;
        var productType = await _repository.GetProductTypeAsync(request.ProductTypeId, ct);
        var initialStatus = productType?.RequiresRetype == true ? UnitStatus.Received : UnitStatus.Quarantine;
        var intakeReason = productType?.RequiresRetype == true
            ? "Initial intake — retype required"
            : "Initial intake";
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
            Isbt128ProductCode = resolved.ProductCodeData ?? resolved.ProductDescriptionCode,
            ProductDescriptionCode = resolved.ProductDescriptionCode,
            ProductCodeData = resolved.ProductCodeData,
            CollectionTypeCode = resolved.CollectionTypeCode,
            DivisionCode = resolved.DivisionCode,
            Isbt128DonationId = request.Isbt128DonationId,
            Volume = request.Volume,
            Status = initialStatus,
            ShipmentId = string.IsNullOrWhiteSpace(request.ShipmentId) ? null : request.ShipmentId.Trim(),
            ReceiveVisualAcceptable = request.VisualInspectionAcceptable,
            ReceiveVisualNotes = string.IsNullOrWhiteSpace(request.VisualInspectionNotes)
                ? null
                : request.VisualInspectionNotes.Trim()
        };

        await _repository.AddUnitAsync(unit, ct);

        // Link by navigation so the FK is fixed up after the unit's Id is generated,
        // keeping the unit and its first history row in one atomic save.
        _repository.AddStatusHistory(new InventoryStatusHistory
        {
            Unit = unit,
            FromStatus = null,
            ToStatus = initialStatus,
            ToLocationId = request.LocationId,
            Reason = intakeReason,
            ChangedBy = _currentUser.UserName,
            ChangedUtc = _clock.UtcNow
        });

        await _unitOfWork.SaveChangesAsync(ct);
        return InventoryActionResult.Ok(unit);
    }

    /// <summary>
    /// SoftBank/SafeTrace packing-list intake: records a unit that has been
    /// shipped but has not yet arrived. No visual inspection until arrival.
    /// </summary>
    public async Task<InventoryActionResult> ExpectUnitAsync(ReceiveUnitRequest request, CancellationToken ct = default)
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

        var productLookup = await _lookups.GetProductLookupAsync(ct);
        var productValidation = ProductCodeLookupValidator.Validate(
            request.Isbt128ProductCode,
            productLookup,
            DateOnly.FromDateTime(_clock.UtcNow));
        if (!productValidation.Success)
        {
            return InventoryActionResult.Fail(productValidation.Error!);
        }

        if (await _repository.UnitNumberExistsAsync(request.UnitNumber, ct))
        {
            return InventoryActionResult.Fail($"A unit with number '{request.UnitNumber}' already exists.");
        }

        var resolved = productValidation.Value!;
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
            ShipmentId = string.IsNullOrWhiteSpace(request.ShipmentId) ? null : request.ShipmentId.Trim(),
            Isbt128ProductCode = resolved.ProductCodeData ?? resolved.ProductDescriptionCode,
            ProductDescriptionCode = resolved.ProductDescriptionCode,
            ProductCodeData = resolved.ProductCodeData,
            CollectionTypeCode = resolved.CollectionTypeCode,
            DivisionCode = resolved.DivisionCode,
            Isbt128DonationId = request.Isbt128DonationId,
            Volume = request.Volume,
            Status = UnitStatus.Expected,
            ReceiveVisualAcceptable = true
        };

        await _repository.AddUnitAsync(unit, ct);
        _repository.AddStatusHistory(new InventoryStatusHistory
        {
            Unit = unit,
            FromStatus = null,
            ToStatus = UnitStatus.Expected,
            ToLocationId = request.LocationId,
            Reason = string.IsNullOrWhiteSpace(request.ShipmentId)
                ? "Expected inbound unit"
                : $"Expected inbound unit; shipment {request.ShipmentId.Trim()}",
            ChangedBy = _currentUser.UserName,
            ChangedUtc = _clock.UtcNow
        });

        await _unitOfWork.SaveChangesAsync(ct);
        return InventoryActionResult.Ok(unit);
    }

    /// <summary>
    /// Confirms physical arrival of an expected unit. Visual inspection is required.
    /// Lands in Received (retype) or Quarantine (same as walk-in receive).
    /// </summary>
    public async Task<InventoryActionResult> ReceiveExpectedUnitAsync(
        long unitId, ReceiveExpectedUnitRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
        {
            return InventoryActionResult.Fail("Unit not found.");
        }

        if (unit.Status != UnitStatus.Expected)
        {
            return InventoryActionResult.Fail("Only an expected inbound unit can be confirmed on arrival.");
        }

        var visual = await EvaluateReceiveVisualAsync(request.VisualInspectionAcceptable, ct);
        if (visual.Severity == RuleSeverity.HardStop)
        {
            return InventoryActionResult.Blocked(new RuleEvaluation([visual]));
        }

        var productType = await _repository.GetProductTypeAsync(unit.ProductTypeId, ct);
        var destination = productType?.RequiresRetype == true ? UnitStatus.Received : UnitStatus.Quarantine;
        unit.ReceiveVisualAcceptable = request.VisualInspectionAcceptable;
        unit.ReceiveVisualNotes = string.IsNullOrWhiteSpace(request.VisualInspectionNotes)
            ? null
            : request.VisualInspectionNotes.Trim();
        if (request.LocationId is long loc)
        {
            unit.CurrentLocationId = loc;
        }

        var reason = destination == UnitStatus.Received
            ? "Arrival confirmed — retype required"
            : "Arrival confirmed";
        return await ChangeStatusAsync(unit, destination, reason, ct);
    }

    public async Task<InventoryActionResult> CancelExpectedUnitAsync(
        long unitId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return InventoryActionResult.Fail("A reason is required to cancel an expected unit.");
        }

        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
        {
            return InventoryActionResult.Fail("Unit not found.");
        }

        if (unit.Status != UnitStatus.Expected)
        {
            return InventoryActionResult.Fail("Only an expected inbound unit can be cancelled.");
        }

        return await ChangeStatusAsync(unit, UnitStatus.CancelledAssignment, reason.Trim(), ct);
    }

    /// <summary>
    /// Receives a component from a normalized canonical draft (scanner session or manual entry).
    /// Does not re-parse raw barcodes — callers must supply the canonical draft.
    /// </summary>
    public async Task<InventoryActionResult> ReceiveNormalizedComponentAsync(
        CanonicalComponentDraft draft,
        long productTypeId,
        long? locationId,
        string? supplier,
        string? shipmentId,
        string? collectionFacility,
        decimal? volume,
        bool releaseToAvailable,
        bool visualInspectionAcceptable = true,
        string? visualInspectionNotes = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        draft.RebuildIdentity();

        var visual = await EvaluateReceiveVisualAsync(visualInspectionAcceptable, ct);
        if (visual.Severity == RuleSeverity.HardStop)
        {
            return InventoryActionResult.Blocked(new RuleEvaluation([visual]));
        }

        if (draft.Din is null || draft.Product is null || draft.AboRhd is null || draft.Expiration is null)
            return InventoryActionResult.Fail($"{IsbtErrorCodes.IncompleteScanSession}: Required quadrants missing.");

        var identityKey = ComponentIdentityBuilder.BuildUniquenessKey(
            draft.Din.Din, draft.Product.ProductCodeData, draft.Product.ExtendedDivisionCode);

        if (await _repository.ComponentIdentityKeyExistsAsync(identityKey, ct))
            return InventoryActionResult.Fail($"{IsbtErrorCodes.ComponentDuplicate}: {draft.ComponentIdentity}");

        var validation = ComponentCrossFieldValidator.Validate(draft, identityAlreadyExists: false, _clock.UtcNow);
        if (!validation.Valid)
            return InventoryActionResult.Fail(string.Join("; ", validation.Errors.Select(e => $"{e.Code}: {e.Message}")));

        var productType = await _repository.GetProductTypeAsync(productTypeId, ct);
        UnitStatus initialStatus;
        string intakeReason;
        if (productType?.RequiresRetype == true)
        {
            initialStatus = UnitStatus.Received;
            intakeReason = "ISBT receipt — retype required";
        }
        else if (releaseToAvailable)
        {
            initialStatus = UnitStatus.Available;
            intakeReason = "ISBT receipt — released to available";
        }
        else
        {
            initialStatus = UnitStatus.Quarantine;
            intakeReason = "ISBT receipt — quarantine pending disposition";
        }

        var unit = Application.Isbt128.CanonicalComponentMapper.ToBloodUnit(
            draft, productTypeId, locationId, supplier, shipmentId, collectionFacility, volume,
            initialStatus, _clock.UtcNow);
        unit.ReceiveVisualAcceptable = visualInspectionAcceptable;
        unit.ReceiveVisualNotes = string.IsNullOrWhiteSpace(visualInspectionNotes)
            ? null
            : visualInspectionNotes.Trim();

        await _repository.AddUnitAsync(unit, ct);

        _repository.AddStatusHistory(new InventoryStatusHistory
        {
            Unit = unit,
            FromStatus = null,
            ToStatus = initialStatus,
            ToLocationId = locationId,
            Reason = intakeReason,
            ChangedBy = _currentUser.UserName,
            ChangedUtc = _clock.UtcNow
        });

        await _unitOfWork.SaveChangesAsync(ct);
        return InventoryActionResult.Ok(unit);
    }

    public async Task<InventoryActionResult> RecallAsync(long unitId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return InventoryActionResult.Fail("A reason is required to recall a unit.");

        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
            return InventoryActionResult.Fail("Unit not found.");

        unit.RecallReason = reason;
        return await ChangeStatusAsync(unit, UnitStatus.Recalled, reason, ct);
    }

    public async Task<InventoryActionResult> QuarantineAsync(long unitId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return InventoryActionResult.Fail("A quarantine reason is required.");

        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
            return InventoryActionResult.Fail("Unit not found.");

        unit.QuarantineReason = reason;
        return await ChangeStatusAsync(unit, UnitStatus.Quarantine, reason, ct);
    }

    public async Task<InventoryActionResult> ReleaseFromQuarantineAsync(
        long unitId, string? secondVerifier = null, CancellationToken ct = default)
    {
        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
        {
            return InventoryActionResult.Fail("Unit not found.");
        }

        var requireSecond = _policy is null
            || await _policy.GetRequireQuarantineReleaseVerifierAsync(ct);
        var dual = QuarantineReleaseVerifierRule.Evaluate(_currentUser.UserName, secondVerifier, requireSecond);
        if (dual.Severity == RuleSeverity.HardStop)
        {
            return InventoryActionResult.Blocked(new RuleEvaluation([dual]));
        }

        var directory = await EvaluateSecondVerifierDirectoryAsync(secondVerifier, ct);
        if (directory.Severity == RuleSeverity.HardStop)
        {
            return InventoryActionResult.Blocked(new RuleEvaluation([directory]));
        }

        unit.QuarantineReason = null;
        var reason = string.IsNullOrWhiteSpace(secondVerifier)
            ? "Released from quarantine"
            : $"Released from quarantine; second verifier {secondVerifier.Trim()}";
        return await ChangeStatusAsync(unit, UnitStatus.Available, reason, ct);
    }

    private async Task<RuleResult> EvaluateSecondVerifierDirectoryAsync(string? secondVerifier, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(secondVerifier))
        {
            return SecondVerifierDirectoryRule.Evaluate(secondVerifier, isActiveUser: false);
        }

        if (_users is null)
        {
            return SecondVerifierDirectoryRule.Evaluate(secondVerifier, isActiveUser: true);
        }

        var upper = secondVerifier.Trim().ToUpperInvariant();
        var match = await _users.FirstOrDefaultAsync(
            u => u.IsActive && !u.IsLocked && !u.IsServiceAccount && u.UserName.ToUpper() == upper, ct);
        return SecondVerifierDirectoryRule.Evaluate(secondVerifier, match is not null);
    }

    private async Task<RuleResult> EvaluateReceiveVisualAsync(bool acceptable, CancellationToken ct)
    {
        var required = _policy is null || await _policy.GetRequireReceiveVisualInspectionAsync(ct);
        return ReceiveVisualInspectionRule.Evaluate(required, acceptable);
    }

    /// <summary>
    /// Places a unit on operational hold. Distinct from quarantine: hold is administrative
    /// (paperwork, pending review) and does not imply a product-quality disposition.
    /// </summary>
    public async Task<InventoryActionResult> HoldAsync(long unitId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return InventoryActionResult.Fail("A hold reason is required.");

        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
            return InventoryActionResult.Fail("Unit not found.");

        unit.HoldReason = reason.Trim();
        return await ChangeStatusAsync(unit, UnitStatus.OnHold, reason.Trim(), ct);
    }

    public async Task<InventoryActionResult> ReleaseFromHoldAsync(long unitId, CancellationToken ct = default)
    {
        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
            return InventoryActionResult.Fail("Unit not found.");

        if (unit.Status != UnitStatus.OnHold)
            return InventoryActionResult.Fail("Only a unit on operational hold can be released from hold.");

        unit.HoldReason = null;
        return await ChangeStatusAsync(unit, UnitStatus.Available, "Released from operational hold", ct);
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
