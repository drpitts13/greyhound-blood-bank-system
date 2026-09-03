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
    private readonly IRepository<Patient>? _patients;

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
        FacilityPolicyService? policy = null,
        IRepository<Patient>? patients = null)
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
        _patients = patients;
    }

    public Task<IReadOnlyList<BloodUnit>> SearchAsync(InventorySearchCriteria criteria, CancellationToken ct = default) =>
        _repository.SearchAsync(criteria, ct);

    /// <summary>
    /// SoftBank/SafeTrace packing-list worklist: units still in Expected status,
    /// flagged overdue after <see cref="FacilityPolicyKeys.ExpectedArrivalDueHours"/>.
    /// </summary>
    public async Task<IReadOnlyList<ExpectedInboundWorkItemDto>> ListExpectedAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var rows = await _repository.SearchAsync(new InventorySearchCriteria(Status: UnitStatus.Expected), ct);
        return rows
            .OrderBy(u => u.ExpectedArrivalDueUtc ?? u.CreatedUtc)
            .Select(u => ExpectedInboundWorkItemDto.From(u, now))
            .ToList();
    }

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

        var appearance = await EvaluateReceiveAppearanceAsync(
            request.VisualInspectionAcceptable, request.Appearance, ct);
        if (appearance is not null)
        {
            return appearance;
        }

        var temperature = await EvaluateReceiveTemperatureAsync(request.ReceiveTemperatureCelsius, ct);
        if (temperature is not null)
        {
            return temperature;
        }

        var restriction = await EvaluateDonationRestrictionAsync(
            request.DonationRestriction, request.ReservedPatientId, ct);
        if (restriction is not null)
        {
            return restriction;
        }

        var verifier = await EvaluateReceiveVerifierAsync(request.SecondVerifier, ct);
        if (verifier is not null)
        {
            return verifier;
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
        var intakeReason = AppendVerifier(
            productType?.RequiresRetype == true
                ? "Initial intake — retype required"
                : "Initial intake",
            request.SecondVerifier);
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
            ReceiveVisualAcceptable = request.VisualInspectionAcceptable
                && ReceiveAppearanceRule.IsAcceptable(request.Appearance),
            ReceiveVisualNotes = string.IsNullOrWhiteSpace(request.VisualInspectionNotes)
                ? null
                : request.VisualInspectionNotes.Trim(),
            ReceiveAppearance = request.Appearance,
            ReceiveTemperatureCelsius = request.ReceiveTemperatureCelsius,
            DonationRestriction = request.DonationRestriction,
            ReservedPatientId = AutologousDirectedRule.RequiresRecipient(request.DonationRestriction)
                ? request.ReservedPatientId
                : null
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

        var restriction = await EvaluateDonationRestrictionAsync(
            request.DonationRestriction, request.ReservedPatientId, ct);
        if (restriction is not null)
        {
            return restriction;
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
            ReceiveVisualAcceptable = true,
            DonationRestriction = request.DonationRestriction,
            ReservedPatientId = AutologousDirectedRule.RequiresRecipient(request.DonationRestriction)
                ? request.ReservedPatientId
                : null
        };

        var dueHours = _policy is null ? 24 : await _policy.GetExpectedArrivalDueHoursAsync(ct);
        unit.ExpectedArrivalDueUtc = _clock.UtcNow.AddHours(dueHours);

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

        var appearance = await EvaluateReceiveAppearanceAsync(
            request.VisualInspectionAcceptable, request.Appearance, ct);
        if (appearance is not null)
        {
            return appearance;
        }

        var temperature = await EvaluateReceiveTemperatureAsync(request.ReceiveTemperatureCelsius, ct);
        if (temperature is not null)
        {
            return temperature;
        }

        var verifier = await EvaluateReceiveVerifierAsync(request.SecondVerifier, ct);
        if (verifier is not null)
        {
            return verifier;
        }

        var productType = await _repository.GetProductTypeAsync(unit.ProductTypeId, ct);
        var destination = productType?.RequiresRetype == true ? UnitStatus.Received : UnitStatus.Quarantine;
        unit.ReceiveVisualAcceptable = request.VisualInspectionAcceptable
            && ReceiveAppearanceRule.IsAcceptable(request.Appearance);
        unit.ReceiveVisualNotes = string.IsNullOrWhiteSpace(request.VisualInspectionNotes)
            ? null
            : request.VisualInspectionNotes.Trim();
        unit.ReceiveAppearance = request.Appearance;
        unit.ReceiveTemperatureCelsius = request.ReceiveTemperatureCelsius;
        if (request.LocationId is long loc)
        {
            unit.CurrentLocationId = loc;
        }

        var reason = AppendVerifier(
            destination == UnitStatus.Received
                ? "Arrival confirmed — retype required"
                : "Arrival confirmed",
            request.SecondVerifier);
        var overdue = ExpectedArrivalPendingRule.EvaluateOverdue(unit.ExpectedArrivalDueUtc, _clock.UtcNow);
        if (overdue.Severity == RuleSeverity.Warning)
        {
            reason = $"{reason} (late arrival)";
        }

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
        UnitAppearance appearance = UnitAppearance.Acceptable,
        string? secondVerifier = null,
        decimal? receiveTemperatureCelsius = null,
        DonationRestriction donationRestriction = DonationRestriction.Allogeneic,
        long? reservedPatientId = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        draft.RebuildIdentity();

        var appear = await EvaluateReceiveAppearanceAsync(visualInspectionAcceptable, appearance, ct);
        if (appear is not null)
        {
            return appear;
        }

        var temperature = await EvaluateReceiveTemperatureAsync(receiveTemperatureCelsius, ct);
        if (temperature is not null)
        {
            return temperature;
        }

        var restriction = await EvaluateDonationRestrictionAsync(donationRestriction, reservedPatientId, ct);
        if (restriction is not null)
        {
            return restriction;
        }

        var verifier = await EvaluateReceiveVerifierAsync(secondVerifier, ct);
        if (verifier is not null)
        {
            return verifier;
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
            intakeReason = AppendVerifier("ISBT receipt — retype required", secondVerifier);
        }
        else if (releaseToAvailable)
        {
            initialStatus = UnitStatus.Available;
            intakeReason = AppendVerifier("ISBT receipt — released to available", secondVerifier);
        }
        else
        {
            initialStatus = UnitStatus.Quarantine;
            intakeReason = AppendVerifier("ISBT receipt — quarantine pending disposition", secondVerifier);
        }

        var unit = Application.Isbt128.CanonicalComponentMapper.ToBloodUnit(
            draft, productTypeId, locationId, supplier, shipmentId, collectionFacility, volume,
            initialStatus, _clock.UtcNow);
        unit.ReceiveVisualAcceptable = visualInspectionAcceptable
            && ReceiveAppearanceRule.IsAcceptable(appearance);
        unit.ReceiveVisualNotes = string.IsNullOrWhiteSpace(visualInspectionNotes)
            ? null
            : visualInspectionNotes.Trim();
        unit.ReceiveAppearance = appearance;
        unit.ReceiveTemperatureCelsius = receiveTemperatureCelsius;
        unit.DonationRestriction = donationRestriction;
        unit.ReservedPatientId = AutologousDirectedRule.RequiresRecipient(donationRestriction)
            ? reservedPatientId
            : null;

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

    private async Task<InventoryActionResult?> EvaluateReceiveAppearanceAsync(
        bool visualAcceptable, UnitAppearance appearance, CancellationToken ct)
    {
        var required = _policy is null || await _policy.GetRequireReceiveVisualInspectionAsync(ct);
        var coded = ReceiveAppearanceRule.Evaluate(required, appearance);
        if (coded.Severity == RuleSeverity.HardStop)
        {
            return InventoryActionResult.Blocked(new RuleEvaluation([coded]));
        }

        var acceptable = visualAcceptable && ReceiveAppearanceRule.IsAcceptable(appearance);
        var visual = ReceiveVisualInspectionRule.Evaluate(required, acceptable);
        return visual.Severity == RuleSeverity.HardStop
            ? InventoryActionResult.Blocked(new RuleEvaluation([visual]))
            : null;
    }

    private async Task<InventoryActionResult?> EvaluateReceiveTemperatureAsync(decimal? celsius, CancellationToken ct)
    {
        var required = _policy is null || await _policy.GetRequireReceiveTemperatureAsync(ct);
        var result = ReceiveTemperatureRule.Evaluate(required, celsius);
        return result.Severity == RuleSeverity.HardStop
            ? InventoryActionResult.Blocked(new RuleEvaluation([result]))
            : null;
    }

    private async Task<InventoryActionResult?> EvaluateDonationRestrictionAsync(
        DonationRestriction restriction, long? reservedPatientId, CancellationToken ct)
    {
        var designated = AutologousDirectedRule.EvaluateReceive(restriction, reservedPatientId);
        if (designated.Severity == RuleSeverity.HardStop)
        {
            return InventoryActionResult.Blocked(new RuleEvaluation([designated]));
        }

        if (AutologousDirectedRule.RequiresRecipient(restriction)
            && reservedPatientId is long pid
            && _patients is not null
            && await _patients.GetByIdAsync(pid, ct) is null)
        {
            return InventoryActionResult.Fail("Intended recipient was not found in the patient directory.");
        }

        return null;
    }

    private async Task<InventoryActionResult?> EvaluateReceiveVerifierAsync(string? secondVerifier, CancellationToken ct)
    {
        var required = _policy is null || await _policy.GetRequireReceiveVerifierAsync(ct);
        var dual = ReceiveVerifierRule.Evaluate(_currentUser.UserName, secondVerifier, required);
        if (dual.Severity == RuleSeverity.HardStop)
        {
            return InventoryActionResult.Blocked(new RuleEvaluation([dual]));
        }

        var directory = await EvaluateSecondVerifierDirectoryAsync(secondVerifier, ct);
        if (directory.Severity == RuleSeverity.HardStop)
        {
            return InventoryActionResult.Blocked(new RuleEvaluation([directory]));
        }

        return null;
    }

    private static string AppendVerifier(string reason, string? secondVerifier) =>
        string.IsNullOrWhiteSpace(secondVerifier)
            ? reason
            : $"{reason}; second verifier {secondVerifier.Trim()}";

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

    /// <summary>
    /// SoftBank/SafeTrace inventory discrepancy: the unit cannot be located.
    /// Missing units are not issuable (21 CFR 606.165 chain of custody).
    /// </summary>
    public async Task<InventoryActionResult> MarkMissingAsync(long unitId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return InventoryActionResult.Fail("A reason is required to mark a unit missing.");

        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
            return InventoryActionResult.Fail("Unit not found.");

        unit.MissingReason = reason.Trim();
        return await ChangeStatusAsync(unit, UnitStatus.Missing, reason.Trim(), ct);
    }

    /// <summary>
    /// Recovered units enter quality quarantine for inspection before they can return to Available.
    /// </summary>
    public async Task<InventoryActionResult> LocateMissingAsync(long unitId, CancellationToken ct = default)
    {
        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
            return InventoryActionResult.Fail("Unit not found.");

        if (unit.Status != UnitStatus.Missing)
            return InventoryActionResult.Fail("Only a missing unit can be located back into inventory.");

        var prior = unit.MissingReason;
        unit.MissingReason = null;
        unit.QuarantineReason = string.IsNullOrWhiteSpace(prior)
            ? "Located after missing; pending inspection"
            : $"Located after missing ({prior}); pending inspection";
        return await ChangeStatusAsync(unit, UnitStatus.Quarantine, unit.QuarantineReason, ct);
    }

    /// <summary>
    /// SoftBank/SafeTrace container damage found in storage or handling.
    /// Distinct from receive-time appearance reject: the unit is already in inventory.
    /// Damaged units are not issuable (AABB product integrity).
    /// </summary>
    public async Task<InventoryActionResult> MarkDamagedAsync(long unitId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return InventoryActionResult.Fail("A reason is required to mark a unit damaged.");

        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
            return InventoryActionResult.Fail("Unit not found.");

        unit.DamagedReason = reason.Trim();
        return await ChangeStatusAsync(unit, UnitStatus.Damaged, reason.Trim(), ct);
    }

    /// <summary>
    /// Inspected damaged units enter quality quarantine; they cannot return directly to Available.
    /// </summary>
    public async Task<InventoryActionResult> InspectDamagedAsync(long unitId, CancellationToken ct = default)
    {
        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
            return InventoryActionResult.Fail("Unit not found.");

        if (unit.Status != UnitStatus.Damaged)
            return InventoryActionResult.Fail("Only a damaged unit can be moved to quarantine for inspection.");

        var prior = unit.DamagedReason;
        unit.DamagedReason = null;
        unit.QuarantineReason = string.IsNullOrWhiteSpace(prior)
            ? "Inspected after damage; pending quality review"
            : $"Inspected after damage ({prior}); pending quality review";
        return await ChangeStatusAsync(unit, UnitStatus.Quarantine, unit.QuarantineReason, ct);
    }

    /// <summary>
    /// SoftBank/SafeTrace consignee reject / unused-stock return to the supplier.
    /// Distinct from ward <see cref="UnitStatus.Returned"/> and from packing-list
    /// <see cref="UnitStatus.CancelledAssignment"/>. Terminal; not issuable.
    /// </summary>
    public async Task<InventoryActionResult> ReturnToSupplierAsync(long unitId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return InventoryActionResult.Fail("A reason is required to return a unit to the supplier.");

        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
            return InventoryActionResult.Fail("Unit not found.");

        unit.SupplierReturnReason = reason.Trim();
        return await ChangeStatusAsync(unit, UnitStatus.ReturnedToSupplier, reason.Trim(), ct);
    }

    /// <summary>
    /// SoftBank/SafeTrace unused-directed conversion: drop the recipient lock so
    /// the unit may enter volunteer inventory. Autologous units cannot convert.
    /// Requires a distinct second verifier when policy demands it.
    /// </summary>
    public async Task<InventoryActionResult> ConvertDirectedToAllogeneicAsync(
        long unitId, string reason, string? secondVerifier = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return InventoryActionResult.Fail("A reason is required to convert a directed unit to allogeneic inventory.");
        }

        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
        {
            return InventoryActionResult.Fail("Unit not found.");
        }

        var convert = AutologousDirectedRule.EvaluateConvert(unit.DonationRestriction, unit.Status);
        if (convert.Severity == RuleSeverity.HardStop)
        {
            return InventoryActionResult.Blocked(new RuleEvaluation([convert]));
        }

        var requireSecond = _policy is null
            || await _policy.GetRequireDirectedConversionVerifierAsync(ct);
        var dual = DirectedConversionVerifierRule.Evaluate(_currentUser.UserName, secondVerifier, requireSecond);
        if (dual.Severity == RuleSeverity.HardStop)
        {
            return InventoryActionResult.Blocked(new RuleEvaluation([dual]));
        }

        var directory = await EvaluateSecondVerifierDirectoryAsync(secondVerifier, ct);
        if (directory.Severity == RuleSeverity.HardStop)
        {
            return InventoryActionResult.Blocked(new RuleEvaluation([directory]));
        }

        var priorPatientId = unit.ReservedPatientId;
        unit.DonationRestriction = DonationRestriction.Allogeneic;
        unit.ReservedPatientId = null;
        unit.DirectedConversionReason = reason.Trim();
        unit.DirectedConvertedUtc = _clock.UtcNow;
        unit.DirectedConvertedBy = _currentUser.UserName;

        var historyReason = string.IsNullOrWhiteSpace(secondVerifier)
            ? reason.Trim()
            : $"{reason.Trim()}; second verifier {secondVerifier.Trim()}";

        _repository.AddStatusHistory(new InventoryStatusHistory
        {
            BloodProductId = unit.Id,
            FromStatus = unit.Status,
            ToStatus = unit.Status,
            FromLocationId = unit.CurrentLocationId,
            ToLocationId = unit.CurrentLocationId,
            Reason = $"Converted directed to allogeneic: {historyReason}",
            ChangedBy = _currentUser.UserName,
            ChangedUtc = _clock.UtcNow
        });

        _audit.Record(
            AuditEventType.Update,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { DonationRestriction = DonationRestriction.Directed, ReservedPatientId = priorPatientId },
            newValue: new
            {
                DonationRestriction = DonationRestriction.Allogeneic,
                DirectedConversionReason = unit.DirectedConversionReason,
                SecondVerifier = secondVerifier
            },
            reason: historyReason);

        await _unitOfWork.SaveChangesAsync(ct);
        return InventoryActionResult.Ok(unit);
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
    /// Dangerous action: requires a reason, a distinct second verifier when policy
    /// demands it, and is recorded as a named Discard audit event in addition to the
    /// automatic Update audit (see docs/safety-rules.md 5).
    /// </summary>
    public async Task<InventoryActionResult> DiscardAsync(
        long unitId, string reason, string? secondVerifier = null, CancellationToken ct = default)
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

        var requireSecond = _policy is null
            || await _policy.GetRequireDiscardVerifierAsync(ct);
        var dual = DiscardVerifierRule.Evaluate(_currentUser.UserName, secondVerifier, requireSecond);
        if (dual.Severity == RuleSeverity.HardStop)
        {
            return InventoryActionResult.Blocked(new RuleEvaluation([dual]));
        }

        var directory = await EvaluateSecondVerifierDirectoryAsync(secondVerifier, ct);
        if (directory.Severity == RuleSeverity.HardStop)
        {
            return InventoryActionResult.Blocked(new RuleEvaluation([directory]));
        }

        var fromStatus = unit.Status;
        unit.Status = UnitStatus.Discarded;
        unit.DiscardReason = reason;
        var historyReason = string.IsNullOrWhiteSpace(secondVerifier)
            ? reason
            : $"{reason}; second verifier {secondVerifier.Trim()}";

        _repository.AddStatusHistory(new InventoryStatusHistory
        {
            BloodProductId = unit.Id,
            FromStatus = fromStatus,
            ToStatus = UnitStatus.Discarded,
            FromLocationId = unit.CurrentLocationId,
            ToLocationId = unit.CurrentLocationId,
            Reason = historyReason,
            ChangedBy = _currentUser.UserName,
            ChangedUtc = _clock.UtcNow
        });

        _audit.Record(
            AuditEventType.Discard,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = fromStatus },
            newValue: new { Status = UnitStatus.Discarded, DiscardReason = reason, SecondVerifier = secondVerifier },
            reason: historyReason);

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
