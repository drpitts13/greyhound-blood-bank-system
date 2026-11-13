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

    private static readonly UnitStatus[] NearExpiryStatuses =
    {
        UnitStatus.Quarantine, UnitStatus.Available, UnitStatus.Allocated, UnitStatus.Returned,
        UnitStatus.Received, UnitStatus.Selected, UnitStatus.Assigned, UnitStatus.Crossmatched,
        UnitStatus.Transferred, UnitStatus.OnHold
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
    private readonly IRepository<InventoryLocation>? _locations;
    private readonly IRepository<ProductType>? _productTypes;
    private readonly IPermissionEvaluator? _permissions;
    private readonly ProductRetypeService? _retype;
    private readonly IRepository<ProductRetypeResult>? _retypeResults;
    private readonly DeliveryService? _deliveries;
    private readonly IRepository<UnitModificationUnit>? _modificationUnits;
    private readonly IRepository<UnitDiscardReason>? _discardReasons;
    private readonly IRepository<Allocation>? _allocations;
    private readonly IAuditQuery? _auditQuery;

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
        IRepository<Patient>? patients = null,
        IRepository<InventoryLocation>? locations = null,
        IRepository<ProductType>? productTypes = null,
        IPermissionEvaluator? permissions = null,
        ProductRetypeService? retype = null,
        IRepository<ProductRetypeResult>? retypeResults = null,
        DeliveryService? deliveries = null,
        IRepository<UnitModificationUnit>? modificationUnits = null,
        IRepository<UnitDiscardReason>? discardReasons = null,
        IRepository<Allocation>? allocations = null,
        IRepository<ReactionInvestigation>? reactions = null,
        IAuditQuery? auditQuery = null,
        IRepository<StorageTemperatureReading>? temperatureReadings = null)
    {
        _temperatureReadings = temperatureReadings;
        _auditQuery = auditQuery;
        _reactions = reactions;
        _repository = repository;
        _unitAttributes = unitAttributes;
        _bloodAttributes = bloodAttributes;
        _lookups = lookups;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _deliveries = deliveries;
        _users = users;
        _policy = policy;
        _patients = patients;
        _locations = locations;
        _productTypes = productTypes;
        _permissions = permissions;
        _retype = retype;
        _retypeResults = retypeResults;
        _modificationUnits = modificationUnits;
        _discardReasons = discardReasons;
        _allocations = allocations;
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
        var supplierIds = await SupplierIdsByDeliveryAsync(rows, ct);
        return rows
            .OrderBy(u => u.ExpectedArrivalDueUtc ?? u.CreatedUtc)
            .Select(u => ExpectedInboundWorkItemDto.From(u, now) with
            {
                BloodSupplierId = u.DeliveryId is long deliveryId && supplierIds.TryGetValue(deliveryId, out var supplierId)
                    ? supplierId
                    : null
            })
            .ToList();
    }

    private async Task<Dictionary<long, long>> SupplierIdsByDeliveryAsync(
        IEnumerable<BloodUnit> units,
        CancellationToken ct)
    {
        var map = new Dictionary<long, long>();
        if (_deliveries is null)
        {
            return map;
        }

        foreach (var deliveryId in units.Select(u => u.DeliveryId).OfType<long>().Distinct())
        {
            var supplierId = await _deliveries.GetSupplierIdAsync(deliveryId, ct);
            if (supplierId is long id)
            {
                map[deliveryId] = id;
            }
        }

        return map;
    }

    /// <summary>
    /// SoftBank/SafeTrace FIFO / outdate worklist: on-hand units that expire within
    /// <see cref="FacilityPolicyKeys.NearExpiryWarningHours"/> (default 24) but are
    /// not yet expired. The issue gate already warns <c>UNIT-NEAR-EXPIRY</c>.
    /// </summary>
    public async Task<IReadOnlyList<NearExpiryWorkItemDto>> ListNearExpiryAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var hours = _policy is null ? 24 : await _policy.GetNearExpiryWarningHoursAsync(ct);
        if (hours < 1)
        {
            hours = 1;
        }

        var window = TimeSpan.FromHours(hours);
        var rows = await _repository.SearchAsync(
            new InventorySearchCriteria(ExpiringBeforeUtc: now.Add(window).AddSeconds(1)), ct);
        var near = rows
            .Where(u => NearExpiryStatuses.Contains(u.Status)
                        && BloodUnitExpirationRule.IsNearExpiry(u.ExpiresUtc, now, window))
            .OrderBy(u => u.ExpiresUtc)
            .ToList();
        var locationNames = await LoadLocationNamesAsync(near.Select(u => u.CurrentLocationId).OfType<long>(), ct);
        var reservedFor = await LoadReservedPatientsAsync(near.Select(u => u.Id).ToList(), ct);

        return near
            .Select(u =>
            {
                string? locationName = null;
                if (u.CurrentLocationId is long locationId)
                {
                    locationNames.TryGetValue(locationId, out locationName);
                }

                var row = NearExpiryWorkItemDto.From(u, locationName);
                return reservedFor.TryGetValue(u.Id, out var patient)
                    ? row with { ReservedPatientId = patient.Id, ReservedPatientMrn = patient.Mrn, ReservedPatientName = patient.Name }
                    : row;
            })
            .ToList();
    }

    /// <summary>The patient on each unit's Reserved allocation; units without one are left out.</summary>
    private async Task<Dictionary<long, (long Id, string? Mrn, string? Name)>> LoadReservedPatientsAsync(
        IReadOnlyList<long> unitIds, CancellationToken ct)
    {
        var result = new Dictionary<long, (long Id, string? Mrn, string? Name)>();
        if (_allocations is null || unitIds.Count == 0)
        {
            return result;
        }

        var reserved = await _allocations.ListAsync(
            a => unitIds.Contains(a.BloodProductId) && a.Status == AllocationStatus.Reserved, ct);
        if (reserved.Count == 0)
        {
            return result;
        }

        var patientIds = reserved.Select(a => a.PatientId).Distinct().ToList();
        var patients = _patients is null
            ? new Dictionary<long, Patient>()
            : (await _patients.ListAsync(p => patientIds.Contains(p.Id), ct)).ToDictionary(p => p.Id);
        foreach (var allocation in reserved.OrderByDescending(a => a.AllocatedUtc))
        {
            if (result.ContainsKey(allocation.BloodProductId))
            {
                continue;
            }

            patients.TryGetValue(allocation.PatientId, out var patient);
            var name = patient is null ? null : $"{patient.LastName}, {patient.FirstName}".Trim(' ', ',');
            result[allocation.BloodProductId] = (
                allocation.PatientId,
                string.IsNullOrWhiteSpace(patient?.MedicalRecordNumber) ? null : patient.MedicalRecordNumber.Trim(),
                string.IsNullOrWhiteSpace(name) ? null : name);
        }

        return result;
    }

    /// <summary>
    /// SoftBank/SafeTrace quality-quarantine worklist: units in
    /// <see cref="UnitStatus.Quarantine"/> with a coded disposition.
    /// </summary>
    private readonly IRepository<ReactionInvestigation>? _reactions;

    /// <summary>Units whose reaction investigation is still open (not closed), among those given.</summary>
    private async Task<HashSet<long>> OpenReactionUnitIdsAsync(IReadOnlyCollection<long> unitIds, CancellationToken ct) =>
        _reactions is null || unitIds.Count == 0
            ? []
            : (await _reactions.ListAsync(r => unitIds.Contains(r.BloodProductId) && r.Status != ReactionInvestigationStatus.Closed, ct))
                .Select(r => r.BloodProductId)
                .ToHashSet();

    private readonly IRepository<StorageTemperatureReading>? _temperatureReadings;

    /// <summary>
    /// Locations, among those given, whose latest readings are an excursion still going on (no in-range reading since),
    /// named for the person: code (or name) and the latest reading.
    /// </summary>
    private async Task<Dictionary<long, string>> LocationsStillOutOfRangeAsync(IEnumerable<long> locationIds, CancellationToken ct)
    {
        var ids = locationIds.Distinct().ToList();
        var result = new Dictionary<long, string>();
        if (_temperatureReadings is null || ids.Count == 0)
        {
            return result;
        }

        var readings = await _temperatureReadings.ListAsync(r => ids.Contains(r.InventoryLocationId), ct);
        var names = await LoadLocationNamesAsync(ids, ct);
        foreach (var group in readings.GroupBy(r => r.InventoryLocationId))
        {
            if (StorageTemperatureRule.OngoingExcursionStart(group.Select(r => (r.ReadingUtc, r.Status))) is null)
            {
                continue;
            }

            var latest = group.OrderByDescending(r => r.ReadingUtc).ThenByDescending(r => r.Id).First();
            var name = names.TryGetValue(group.Key, out var n) && !string.IsNullOrWhiteSpace(n) ? n : $"Location {group.Key}";
            result[group.Key] = $"{name} ({latest.ReadingC.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} °C)";
        }

        return result;
    }

    private async Task<QuarantineReleaseRule.Decision> EvaluateQuarantineReleaseAsync(BloodUnit unit, CancellationToken ct) =>
        QuarantineReleaseRule.Evaluate(
            unit.QuarantineReasonCode,
            (await OpenReactionUnitIdsAsync([unit.Id], ct)).Contains(unit.Id),
            _retypeResults is not null && await ProductRetypeConfirmation.HasVerifiedMismatchAsync(_retypeResults, unit.Id, ct),
            unit.ExpiresUtc <= _clock.UtcNow,
            unit.CurrentLocationId is long locationId
                ? (await LocationsStillOutOfRangeAsync([locationId], ct)).GetValueOrDefault(locationId)
                : null);

    public async Task<IReadOnlyList<QuarantineWorkItemDto>> ListQuarantineAsync(CancellationToken ct = default)
    {
        // OCD-083: a unit reported transfused is not released; it is reviewed on the supervisor queue.
        var rows = (await _repository.SearchAsync(new InventorySearchCriteria(Status: UnitStatus.Quarantine), ct))
            .Where(u => u.TransfusionReportedUtc is null)
            .ToList();
        var productIds = rows.Select(u => u.ProductTypeId).Distinct().ToList();
        var products = _productTypes is null || productIds.Count == 0
            ? new Dictionary<long, ProductType>()
            : (await _productTypes.ListAsync(p => productIds.Contains(p.Id), ct)).ToDictionary(p => p.Id);
        var unitIds = rows.Select(u => u.Id).ToList();
        var retypes = _retypeResults is null || unitIds.Count == 0
            ? new Dictionary<long, ProductRetypeResult>()
            : (await _retypeResults.ListAsync(r => unitIds.Contains(r.BloodProductId), ct))
                .GroupBy(r => r.BloodProductId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Id).First());
        var locationIds = rows.Select(u => u.CurrentLocationId).OfType<long>().Distinct().ToList();
        var locationNames = await LoadLocationNamesAsync(locationIds, ct);
        var modificationResultIds = await ModificationResultUnitIdsAsync(unitIds, ct);
        var openReactions = await OpenReactionUnitIdsAsync(unitIds, ct);
        var outOfRange = await LocationsStillOutOfRangeAsync(locationIds, ct);
        var verifiedMismatches = new HashSet<long>();
        if (_retypeResults is not null)
        {
            foreach (var id in unitIds)
            {
                if (await ProductRetypeConfirmation.HasVerifiedMismatchAsync(_retypeResults, id, ct))
                {
                    verifiedMismatches.Add(id);
                }
            }
        }

        var now = _clock.UtcNow;
        return rows
            .OrderBy(u => u.CreatedUtc)
            .Select(u =>
            {
                products.TryGetValue(u.ProductTypeId, out var product);
                retypes.TryGetValue(u.Id, out var latest);
                string? locationName = null;
                if (u.CurrentLocationId is long locationId)
                {
                    locationNames.TryGetValue(locationId, out locationName);
                }

                var release = QuarantineReleaseRule.Evaluate(
                    u.QuarantineReasonCode, openReactions.Contains(u.Id), verifiedMismatches.Contains(u.Id), u.ExpiresUtc <= now,
                    u.CurrentLocationId is long at ? outOfRange.GetValueOrDefault(at) : null);
                return QuarantineWorkItemDto.From(
                    u, product?.ProductCode, latest, locationName, modificationResultIds.Contains(u.Id))
                    with { NextStep = release.NextStep, CanRelease = release.CanRelease };
            })
            .ToList();
    }

    /// <summary>
    /// SoftBank/SafeTrace physical-inventory discrepancy worklist: missing and
    /// damaged units awaiting locate or inspect (21 CFR 606.165).
    /// </summary>
    public async Task<IReadOnlyList<DiscrepancyWorkItemDto>> ListDiscrepancyAsync(CancellationToken ct = default)
    {
        var missing = await _repository.SearchAsync(new InventorySearchCriteria(Status: UnitStatus.Missing), ct);
        var damaged = await _repository.SearchAsync(new InventorySearchCriteria(Status: UnitStatus.Damaged), ct);
        // OCD-083: a unit reported transfused is not located or inspected; it is reviewed on the supervisor queue.
        var rows = missing.Concat(damaged).Where(u => u.TransfusionReportedUtc is null).ToList();
        var products = await LoadProductsAsync(rows, ct);
        var locationNames = await LoadLocationNamesAsync(rows.Select(u => u.CurrentLocationId).OfType<long>(), ct);
        return rows
            .OrderBy(u => u.Status)
            .ThenBy(u => u.UnitNumber)
            .Select(u =>
            {
                products.TryGetValue(u.ProductTypeId, out var product);
                string? locationName = null;
                if (u.CurrentLocationId is long locationId)
                {
                    locationNames.TryGetValue(locationId, out locationName);
                }

                return DiscrepancyWorkItemDto.From(u, product?.ProductCode, locationName);
            })
            .ToList();
    }

    /// <summary>
    /// Operational hold worklist: units that cannot be issued until hold is released.
    /// </summary>
    public async Task<IReadOnlyList<OnHoldWorkItemDto>> ListOnHoldAsync(CancellationToken ct = default)
    {
        var rows = await _repository.SearchAsync(new InventorySearchCriteria(Status: UnitStatus.OnHold), ct);
        var products = await LoadProductsAsync(rows, ct);
        var locationNames = await LoadLocationNamesAsync(rows.Select(u => u.CurrentLocationId).OfType<long>(), ct);
        return rows
            .OrderBy(u => u.UnitNumber)
            .Select(u =>
            {
                products.TryGetValue(u.ProductTypeId, out var product);
                string? locationName = null;
                if (u.CurrentLocationId is long locationId)
                {
                    locationNames.TryGetValue(locationId, out locationName);
                }

                return OnHoldWorkItemDto.From(u, product?.ProductCode, locationName);
            })
            .ToList();
    }

    /// <summary>
    /// Cycle 688: recalled units not yet pulled (still Recalled), oldest recall first, with the recall's time, who and
    /// what the unit was before it, from the status row the recall wrote.
    /// </summary>
    public async Task<IReadOnlyList<RecalledWorkItemDto>> ListRecalledAsync(CancellationToken ct = default)
    {
        // OCD-083: a recalled unit the EHR reported transfused is not here to pull; it is reviewed on the supervisor queue.
        var rows = (await _repository.SearchAsync(new InventorySearchCriteria(Status: UnitStatus.Recalled), ct))
            .Where(u => u.TransfusionReportedUtc is null)
            .ToList();
        var products = await LoadProductsAsync(rows, ct);
        var locationNames = await LoadLocationNamesAsync(rows.Select(u => u.CurrentLocationId).OfType<long>(), ct);
        var items = new List<RecalledWorkItemDto>(rows.Count);
        foreach (var u in rows)
        {
            var recall = (await _repository.GetHistoryAsync(u.Id, ct))
                .Where(h => h.ToStatus == UnitStatus.Recalled)
                .OrderByDescending(h => h.ChangedUtc).ThenByDescending(h => h.Id)
                .FirstOrDefault();
            products.TryGetValue(u.ProductTypeId, out var product);
            string? locationName = null;
            if (u.CurrentLocationId is long locationId)
            {
                locationNames.TryGetValue(locationId, out locationName);
            }

            items.Add(new RecalledWorkItemDto(
                u.Id, u.UnitNumber, u.Abo, u.RhD, u.ExpiresUtc, product?.ProductCode, u.CurrentLocationId,
                string.IsNullOrWhiteSpace(locationName) ? null : locationName.Trim(),
                u.RecallReason ?? recall?.Reason, recall?.ChangedUtc, recall?.ChangedBy, recall?.FromStatus));
        }

        return items
            .OrderBy(i => i.RecalledUtc ?? DateTime.MinValue)
            .ThenBy(i => i.UnitNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<Dictionary<long, ProductType>> LoadProductsAsync(
        IReadOnlyCollection<BloodUnit> rows,
        CancellationToken ct)
    {
        var productIds = rows.Select(u => u.ProductTypeId).Distinct().ToList();
        if (_productTypes is null || productIds.Count == 0)
        {
            return new Dictionary<long, ProductType>();
        }

        return (await _productTypes.ListAsync(p => productIds.Contains(p.Id), ct)).ToDictionary(p => p.Id);
    }

    private async Task<Dictionary<long, string?>> LoadLocationNamesAsync(
        IEnumerable<long> locationIds,
        CancellationToken ct)
    {
        var ids = locationIds.Distinct().ToList();
        var names = new Dictionary<long, string?>();
        if (_locations is null || ids.Count == 0)
        {
            return names;
        }

        var locations = await _locations.ListAsync(location => ids.Contains(location.Id), ct);
        foreach (var location in locations)
        {
            names[location.Id] = location.Name;
        }

        return names;
    }

    public Task<BloodUnit?> GetAsync(long id, CancellationToken ct = default) =>
        _repository.GetUnitAsync(id, ct);

    public async Task<BloodUnitDto?> GetChartAsync(long id, CancellationToken ct = default)
    {
        var unit = await GetAsync(id, ct);
        return unit is null ? null : await ToChartDtoAsync(unit, ct);
    }

    private async Task<BloodUnitDto> ToChartDtoAsync(BloodUnit unit, CancellationToken ct)
    {
        long? supplierId = null;
        if (unit.DeliveryId is long deliveryId && _deliveries is not null)
        {
            var delivery = await _deliveries.GetAsync(deliveryId, ct);
            if (delivery is not null)
            {
                supplierId = delivery.BloodSupplierId;
            }
        }

        return BloodUnitDto.From(unit) with
        {
            BloodSupplierId = supplierId,
            RequiresModificationLabelScan = unit.Status == UnitStatus.Quarantine && await IsModificationResultAsync(unit.Id, ct)
        };
    }

    private async Task<bool> IsModificationResultAsync(long unitId, CancellationToken ct) =>
        _modificationUnits is not null
        && await _modificationUnits.AnyAsync(
            link => link.BloodProductId == unitId && link.Role == ModificationUnitRole.Result, ct);

    private async Task<HashSet<long>> ModificationResultUnitIdsAsync(IReadOnlyList<long> unitIds, CancellationToken ct)
    {
        if (_modificationUnits is null || unitIds.Count == 0)
        {
            return [];
        }

        var links = await _modificationUnits.ListAsync(
            link => unitIds.Contains(link.BloodProductId) && link.Role == ModificationUnitRole.Result, ct);
        return links.Select(link => link.BloodProductId).ToHashSet();
    }

    public Task<IReadOnlyList<InventoryStatusHistory>> GetHistoryAsync(long unitId, CancellationToken ct = default) =>
        _repository.GetHistoryAsync(unitId, ct);

    public Task<IReadOnlyList<UnitBloodAttribute>> GetBloodAttributesAsync(long unitId, CancellationToken ct = default) =>
        _unitAttributes.ListAsync(a => a.BloodProductId == unitId, ct);

    public async Task<OperationResult<UnitBloodAttribute>> SaveBloodAttributeAsync(
        long unitId, SaveUnitBloodAttributeRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_permissions is not null)
        {
            var allowed = await _permissions.HasPermissionAsync(
                _currentUser.UserName, PermissionCodes.InventoryReceive, ct);
            var auth = InventoryAuthorizationRule.EvaluateSaveAttribute(allowed);
            if (auth.Severity == RuleSeverity.HardStop)
            {
                return OperationResult<UnitBloodAttribute>.Fail(auth.Message);
            }
        }

        if (request.TypingSource == UnitAttributeTypingSource.TestResult)
        {
            return OperationResult<UnitBloodAttribute>.Fail(
                "Test-result typings are posted from contributing tests, not entered here.");
        }

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
                Result = request.Result,
                TypingSource = request.TypingSource
            };
            await _unitAttributes.AddAsync(existing, ct);
        }
        else
        {
            existing.Result = request.Result;
            existing.TypingSource = request.TypingSource;
            _unitAttributes.Update(existing);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<UnitBloodAttribute>.Ok(existing);
    }

    /// <summary>Receives a new unit into Quarantine and records the initial status history.</summary>
    public async Task<InventoryActionResult> ReceiveUnitAsync(ReceiveUnitRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedReceiveAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var (appearanceBlocked, receivedAppearance) = await EvaluateReceiveAppearanceAsync(
            request.VisualInspectionAcceptable, request.Appearance, ct);
        if (appearanceBlocked is not null)
        {
            return appearanceBlocked;
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

        var deliveryGate = await ResolveDeliveryAsync(request.DeliveryId, request.ProductTypeId, ct);
        if (deliveryGate.Error is not null)
        {
            return deliveryGate.Error;
        }

        if (string.IsNullOrWhiteSpace(request.UnitNumber))
        {
            return InventoryActionResult.Fail("Unit number is required.");
        }

        // A scanned ISBT 128 DIN ("=" + 13 characters + 2 flag characters) is stored as its 13-character DIN, as the
        // issue desk and bedside read it (end-user testing, 2026-11-12: a delivery's scanned unit was stored with the
        // "=" and the flags, so later scans and searches would not find it).
        if (request.UnitNumber.TrimStart().StartsWith('='))
        {
            var din = BloodBankLIS.Domain.Isbt128.Parsing.DinParser.Parse(request.UnitNumber);
            if (!din.Success || din.Value is null)
            {
                return InventoryActionResult.Fail("The scanned unit number could not be read as an ISBT 128 DIN. Scan the donation identification number barcode, or type the DIN.");
            }

            request = request with { UnitNumber = din.Value.Din };
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
        var retypeGate = RejectIncompleteRetypeAssignment(productType, request.RhD);
        if (retypeGate is not null)
        {
            return retypeGate;
        }

        if (await RejectStorageAsync(request.LocationId, productType, ct) is { } storageGate)
        {
            return storageGate;
        }

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
            CollectionFacility = deliveryGate.Context?.SupplierName ?? request.CollectionFacility,
            Supplier = deliveryGate.Context?.SupplierName ?? request.Supplier,
            DeliveryId = deliveryGate.Context?.Id,
            Isbt128ProductCode = resolved.ProductCodeData ?? resolved.ProductDescriptionCode,
            ProductDescriptionCode = resolved.ProductDescriptionCode,
            ProductCodeData = resolved.ProductCodeData,
            CollectionTypeCode = resolved.CollectionTypeCode,
            DivisionCode = resolved.DivisionCode,
            Isbt128DonationId = request.Isbt128DonationId,
            Volume = request.Volume,
            Status = initialStatus,
            ShipmentId = deliveryGate.Context is not null
                ? deliveryGate.Context.PackingListNumber
                : string.IsNullOrWhiteSpace(request.ShipmentId) ? null : request.ShipmentId.Trim(),
            ReceiveVisualAcceptable = request.VisualInspectionAcceptable
                && ReceiveAppearanceRule.IsAcceptable(receivedAppearance),
            ReceiveVisualNotes = string.IsNullOrWhiteSpace(request.VisualInspectionNotes)
                ? null
                : request.VisualInspectionNotes.Trim(),
            ReceiveAppearance = receivedAppearance,
            ReceiveTemperatureCelsius = request.ReceiveTemperatureCelsius,
            DonationRestriction = request.DonationRestriction,
            ReservedPatientId = AutologousDirectedRule.RequiresRecipient(request.DonationRestriction)
                ? request.ReservedPatientId
                : null,
            QuarantineReasonCode = initialStatus == UnitStatus.Quarantine
                ? UnitQuarantineReason.PendingRelease
                : UnitQuarantineReason.Unspecified
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
        RecordReceiveAudit(unit, path: "WalkIn");
        await _unitOfWork.SaveChangesAsync(ct);
        var attached = await AttachPendingRetypeAsync(unit, productType, ct);
        return attached ?? InventoryActionResult.Ok(unit);
    }

    /// <summary>
    /// SoftBank/SafeTrace packing-list intake: records a unit that has been
    /// shipped but has not yet arrived. No visual inspection until arrival.
    /// </summary>
    public async Task<InventoryActionResult> ExpectUnitAsync(ReceiveUnitRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedReceiveAsync(
            InventoryAuthorizationRule.EvaluateExpect, ct);
        if (denied is not null)
        {
            return denied;
        }

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

        // A scanned ISBT 128 DIN ("=" + 13 characters + 2 flag characters) is stored as its 13-character DIN, as the
        // issue desk and bedside read it (end-user testing, 2026-11-12: a delivery's scanned unit was stored with the
        // "=" and the flags, so later scans and searches would not find it).
        if (request.UnitNumber.TrimStart().StartsWith('='))
        {
            var din = BloodBankLIS.Domain.Isbt128.Parsing.DinParser.Parse(request.UnitNumber);
            if (!din.Success || din.Value is null)
            {
                return InventoryActionResult.Fail("The scanned unit number could not be read as an ISBT 128 DIN. Scan the donation identification number barcode, or type the DIN.");
            }

            request = request with { UnitNumber = din.Value.Din };
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
        _audit.Record(
            AuditEventType.ProductStatus,
            nameof(BloodUnit),
            unit.Id,
            newValue: new
            {
                unit.UnitNumber,
                unit.Isbt128DonationId,
                unit.Status,
                unit.ShipmentId,
                unit.ExpectedArrivalDueUtc,
                Path = "Expected"
            },
            reason: "Expected inbound unit recorded.");
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

        var denied = await RejectUnauthorizedReceiveAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
        {
            return InventoryActionResult.Fail("Unit not found.");
        }

        if (unit.Status != UnitStatus.Expected)
        {
            return InventoryActionResult.Fail("Only an expected inbound unit can be confirmed on arrival.");
        }

        if (BloodUnitExpirationRule.IsExpired(unit.ExpiresUtc, _clock.UtcNow))
        {
            return InventoryActionResult.Fail(
                "This unit is outdated and cannot be received. Return it to the supplier.");
        }

        var (appearanceBlocked, receivedAppearance) = await EvaluateReceiveAppearanceAsync(
            request.VisualInspectionAcceptable, request.Appearance, ct);
        if (appearanceBlocked is not null)
        {
            return appearanceBlocked;
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
        var retypeGate = RejectIncompleteRetypeAssignment(productType, unit.RhD);
        if (retypeGate is not null)
        {
            return retypeGate;
        }

        if (await RejectStorageAsync(request.LocationId, productType, ct) is { } storageGate)
        {
            return storageGate;
        }

        var destination = productType?.RequiresRetype == true ? UnitStatus.Received : UnitStatus.Quarantine;
        unit.ReceiveVisualAcceptable = request.VisualInspectionAcceptable
            && ReceiveAppearanceRule.IsAcceptable(receivedAppearance);
        unit.ReceiveVisualNotes = string.IsNullOrWhiteSpace(request.VisualInspectionNotes)
            ? null
            : request.VisualInspectionNotes.Trim();
        unit.ReceiveAppearance = receivedAppearance;
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

        if (destination == UnitStatus.Quarantine)
        {
            unit.QuarantineReasonCode = UnitQuarantineReason.PendingRelease;
        }

        var changed = await ChangeStatusAsync(unit, destination, reason, ct);
        if (!changed.Succeeded)
        {
            return changed;
        }

        _audit.Record(
            AuditEventType.ProductStatus,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = UnitStatus.Expected },
            newValue: new
            {
                unit.UnitNumber,
                unit.Isbt128DonationId,
                unit.Status,
                unit.DonationRestriction,
                Path = "ExpectedArrival",
                LateArrival = overdue.Severity == RuleSeverity.Warning
            },
            reason: "Expected inbound unit received.");
        await _unitOfWork.SaveChangesAsync(ct);
        if (destination == UnitStatus.Received)
        {
            var attached = await AttachPendingRetypeAsync(unit, productType, ct);
            if (attached is not null)
            {
                return attached;
            }
        }

        return changed;
    }

    public async Task<InventoryActionResult> CancelExpectedUnitAsync(
        long unitId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return InventoryActionResult.Fail("A reason is required to cancel an expected unit.");
        }

        var denied = await RejectUnauthorizedReceiveAsync(
            InventoryAuthorizationRule.EvaluateCancelExpected, ct);
        if (denied is not null)
        {
            return denied;
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

        var changed = await ChangeStatusAsync(unit, UnitStatus.CancelledAssignment, reason.Trim(), ct);
        if (!changed.Succeeded)
        {
            return changed;
        }

        _audit.Record(
            AuditEventType.ProductStatus,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = UnitStatus.Expected },
            newValue: new
            {
                unit.UnitNumber,
                unit.Status,
                Path = "ExpectedCancel",
                CancelReason = reason.Trim()
            },
            reason: "Expected inbound unit cancelled.");
        await _unitOfWork.SaveChangesAsync(ct);
        return changed;
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
        UnitAppearance? recordedAppearance = null,
        string? secondVerifier = null,
        decimal? receiveTemperatureCelsius = null,
        DonationRestriction donationRestriction = DonationRestriction.Allogeneic,
        long? reservedPatientId = null,
        long? deliveryId = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var denied = await RejectUnauthorizedReceiveAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var deliveryGate = await ResolveDeliveryAsync(deliveryId, productTypeId, ct);
        if (deliveryGate.Error is not null)
        {
            return deliveryGate.Error;
        }

        draft.RebuildIdentity();

        var (appear, appearance) = await EvaluateReceiveAppearanceAsync(visualInspectionAcceptable, recordedAppearance, ct);
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
        var labeledRh = draft.AboRhd?.RhD ?? RhType.Unknown;
        var retypeGate = RejectIncompleteRetypeAssignment(productType, labeledRh);
        if (retypeGate is not null)
        {
            return retypeGate;
        }

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
            draft,
            productTypeId,
            locationId,
            deliveryGate.Context?.SupplierName ?? supplier,
            deliveryGate.Context is not null ? deliveryGate.Context.PackingListNumber : shipmentId,
            deliveryGate.Context?.SupplierName ?? collectionFacility,
            volume,
            initialStatus,
            _clock.UtcNow);
        unit.DeliveryId = deliveryGate.Context?.Id;
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
        if (initialStatus == UnitStatus.Quarantine)
        {
            unit.QuarantineReasonCode = UnitQuarantineReason.PendingRelease;
        }

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
        RecordReceiveAudit(unit, path: "IsbtNormalized");
        await _unitOfWork.SaveChangesAsync(ct);
        var attachedIsbt = await AttachPendingRetypeAsync(unit, productType, ct);
        return attachedIsbt ?? InventoryActionResult.Ok(unit);
    }

    public async Task<InventoryActionResult> RecallAsync(long unitId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return InventoryActionResult.Fail("A reason is required to recall a unit.");

        var denied = await RejectUnauthorizedRecallAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var existing = await _repository.GetUnitAsync(unitId, ct);
        if (existing is null)
        {
            return InventoryActionResult.Fail("Unit not found.");
        }

        var fromStatus = existing.Status;
        var recalled = await ApplyRecallAsync(unitId, reason, ct);
        if (!recalled.Succeeded)
        {
            return recalled;
        }

        _audit.Record(
            AuditEventType.ProductStatus,
            nameof(BloodUnit),
            unitId,
            oldValue: new { Status = fromStatus },
            newValue: new
            {
                existing.UnitNumber,
                Status = UnitStatus.Recalled,
                Path = "DirectRecall",
                RecallReason = reason.Trim()
            },
            reason: "Unit recalled.");
        await _unitOfWork.SaveChangesAsync(ct);
        return recalled;
    }

    /// <summary>
    /// Lookback DIN recall already authorized <c>lookback.manage</c> (OCD-014).
    /// Does not also require <c>inventory.recall</c>.
    /// </summary>
    public Task<InventoryActionResult> RecallForLookbackAsync(long unitId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Task.FromResult(InventoryActionResult.Fail("A reason is required to recall a unit."));

        return ApplyRecallAsync(unitId, reason, ct);
    }

    private async Task<InventoryActionResult> ApplyRecallAsync(long unitId, string reason, CancellationToken ct)
    {
        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
            return InventoryActionResult.Fail("Unit not found.");

        if (RefuseRepeat(unit, UnitStatus.Recalled, "recalled") is { } repeat)
            return repeat;

        unit.RecallReason = reason;
        return await ChangeStatusAsync(unit, UnitStatus.Recalled, reason, ct);
    }

    public Task<InventoryActionResult> QuarantineAsync(long unitId, string reason, CancellationToken ct = default) =>
        QuarantineAsync(unitId, UnitQuarantineReason.Other, reason, ct);

    public async Task<InventoryActionResult> QuarantineAsync(
        long unitId, UnitQuarantineReason reasonCode, string? notes = null, CancellationToken ct = default)
    {
        var coded = QuarantineReasonRule.Evaluate(reasonCode, notes);
        if (coded.Severity == RuleSeverity.HardStop)
        {
            return InventoryActionResult.Blocked(new RuleEvaluation([coded]));
        }

        var denied = await RejectUnauthorizedAsync(InventoryAuthorizationRule.EvaluateQuarantine, ct);
        if (denied is not null)
        {
            return denied;
        }

        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
            return InventoryActionResult.Fail("Unit not found.");

        // A unit held only by the routine receipt hold may be quarantined for a specific reason; one already quarantined
        // for a specific reason keeps it.
        if (unit.Status == UnitStatus.Quarantine
            && unit.QuarantineReasonCode is not (UnitQuarantineReason.Unspecified or UnitQuarantineReason.PendingRelease))
            return InventoryActionResult.Fail(
                $"{unit.UnitNumber} is already in quarantine ({unit.QuarantineReason ?? unit.QuarantineReasonCode.ToString()}); the reason recorded then stays.");

        var note = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        unit.QuarantineReasonCode = reasonCode;
        unit.QuarantineReason = note;
        var historyReason = note ?? reasonCode.ToString();
        var fromStatus = unit.Status;
        var quarantined = await ChangeStatusAsync(unit, UnitStatus.Quarantine, historyReason, ct);
        if (!quarantined.Succeeded)
        {
            return quarantined;
        }

        _audit.Record(
            AuditEventType.ProductStatus,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = fromStatus },
            newValue: new
            {
                unit.UnitNumber,
                Status = UnitStatus.Quarantine,
                Path = "PlaceQuarantine",
                ReasonCode = reasonCode,
                Notes = note
            },
            reason: "Unit placed in quarantine.");
        await _unitOfWork.SaveChangesAsync(ct);
        return quarantined;
    }

    public async Task<InventoryActionResult> ReleaseFromQuarantineAsync(
        long unitId, string? secondVerifier = null, string? scannedUnitNumber = null, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(
            InventoryAuthorizationRule.EvaluateQuarantineRelease, ct);
        if (denied is not null)
        {
            return denied;
        }

        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
        {
            return InventoryActionResult.Fail("Unit not found.");
        }

        // End-user testing pass 28: a release of a unit that was not quarantined was accepted and recorded as a release.
        if (unit.Status != UnitStatus.Quarantine)
        {
            return InventoryActionResult.Fail($"{unit.UnitNumber} is not in quarantine ({unit.Status}); there is nothing to release.");
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

        // Why the unit is held decides whether it may go back to stock at all.
        var releasable = await EvaluateQuarantineReleaseAsync(unit, ct);
        if (!releasable.CanRelease)
        {
            return InventoryActionResult.Blocked(new RuleEvaluation([releasable.Result]));
        }

        var isModificationResult = await IsModificationResultAsync(unit.Id, ct);
        var label = ModificationLabelVerificationRule.Evaluate(isModificationResult, unit.UnitNumber, scannedUnitNumber);
        if (label.Severity == RuleSeverity.HardStop)
        {
            return InventoryActionResult.Blocked(new RuleEvaluation([label]));
        }

        var stock = await StockStatusAsync(unit, ct);
        unit.QuarantineReason = null;
        unit.QuarantineReasonCode = UnitQuarantineReason.Unspecified;
        var reason = string.IsNullOrWhiteSpace(secondVerifier)
            ? "Released from quarantine"
            : $"Released from quarantine; second verifier {secondVerifier.Trim()}";
        if (stock == UnitStatus.Received)
        {
            reason = $"{reason} ({RetypeStockStatus.PendingReason})";
        }
        else if (await ReservedStatusBeforeQuarantineAsync(unit, ct) is UnitStatus reservedStatus)
        {
            stock = reservedStatus;
            reason = $"{reason}; still reserved for its patient";
        }

        var changed = await ChangeStatusAsync(unit, stock, reason, ct);
        if (!changed.Succeeded)
        {
            return changed;
        }

        _audit.Record(
            AuditEventType.ProductStatus,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = UnitStatus.Quarantine },
            newValue: new
            {
                unit.UnitNumber,
                unit.Status,
                Path = "QuarantineRelease",
                RetypePending = stock == UnitStatus.Received,
                SecondVerifier = string.IsNullOrWhiteSpace(secondVerifier) ? null : secondVerifier.Trim(),
                ModificationLabelScan = isModificationResult ? unit.UnitNumber : null
            },
            reason: "Unit released from quarantine.");
        await _unitOfWork.SaveChangesAsync(ct);
        return changed;
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

    /// <summary>
    /// Resolves the recorded appearance (OCD-040) and applies the receipt inspection rules. When receipt inspection
    /// is required, an unrecorded appearance is refused unless the facility default treats it as acceptable.
    /// </summary>
    private async Task<(InventoryActionResult? Blocked, UnitAppearance Appearance)> EvaluateReceiveAppearanceAsync(
        bool visualAcceptable, UnitAppearance? recorded, CancellationToken ct)
    {
        var required = _policy is null || await _policy.GetRequireReceiveVisualInspectionAsync(ct);
        var facilityDefault = _policy is not null && await _policy.GetInspectionDefaultAcceptableAsync(ct);
        var attested = AppearanceAttestationRule.Resolve(recorded, facilityDefault, "receiving the unit", inspectionRequired: required);
        if (attested.Result.Severity == RuleSeverity.HardStop)
        {
            return (InventoryActionResult.Blocked(new RuleEvaluation([attested.Result])), attested.Appearance);
        }

        var appearance = attested.Appearance;
        var coded = ReceiveAppearanceRule.Evaluate(required, appearance);
        if (coded.Severity == RuleSeverity.HardStop)
        {
            return (InventoryActionResult.Blocked(new RuleEvaluation([coded])), appearance);
        }

        var acceptable = visualAcceptable && ReceiveAppearanceRule.IsAcceptable(appearance);
        var visual = ReceiveVisualInspectionRule.Evaluate(required, acceptable);
        return visual.Severity == RuleSeverity.HardStop
            ? (InventoryActionResult.Blocked(new RuleEvaluation([visual])), appearance)
            : (null, appearance);
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

    private async Task<(DeliveryReceiveContext? Context, InventoryActionResult? Error)> ResolveDeliveryAsync(
        long? deliveryId,
        long productTypeId,
        CancellationToken ct)
    {
        if (_deliveries is null)
        {
            return (null, null);
        }

        var resolved = await _deliveries.ResolveForReceiveAsync(deliveryId, productTypeId, ct);
        if (!resolved.Succeeded || resolved.Value is null)
        {
            return resolved.Evaluation is { IsHardStopped: true } evaluation
                ? (null, InventoryActionResult.Blocked(evaluation))
                : (null, InventoryActionResult.Fail(resolved.Error ?? "Delivery is not valid for this unit."));
        }

        return (resolved.Value, null);
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

        var denied = await RejectUnauthorizedAsync(InventoryAuthorizationRule.EvaluatePlaceHold, ct);
        if (denied is not null)
        {
            return denied;
        }

        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
            return InventoryActionResult.Fail("Unit not found.");

        if (RefuseRepeat(unit, UnitStatus.OnHold, "on hold") is { } repeat)
            return repeat;

        unit.HoldReason = reason.Trim();
        var fromStatus = unit.Status;
        var held = await ChangeStatusAsync(unit, UnitStatus.OnHold, reason.Trim(), ct);
        if (!held.Succeeded)
        {
            return held;
        }

        _audit.Record(
            AuditEventType.ProductStatus,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = fromStatus },
            newValue: new
            {
                unit.UnitNumber,
                Status = UnitStatus.OnHold,
                Path = "PlaceHold",
                HoldReason = reason.Trim()
            },
            reason: "Unit placed on hold.");
        await _unitOfWork.SaveChangesAsync(ct);
        return held;
    }

    public async Task<InventoryActionResult> ReleaseFromHoldAsync(long unitId, string? reason = null, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(
            InventoryAuthorizationRule.EvaluateHoldRelease, ct);
        if (denied is not null)
        {
            return denied;
        }

        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
            return InventoryActionResult.Fail("Unit not found.");

        if (unit.Status != UnitStatus.OnHold)
            return InventoryActionResult.Fail("Only a unit on operational hold can be released from hold.");

        var check = HoldReleaseRule.Evaluate(reason, unit.ExpiresUtc <= _clock.UtcNow);
        if (check.Severity == RuleSeverity.HardStop)
        {
            return InventoryActionResult.Blocked(new RuleEvaluation([check]));
        }

        // Keep why it was held alongside why it was released; the hold reason is cleared from the unit.
        var heldFor = unit.HoldReason;
        var releaseReason = reason!.Trim();
        unit.HoldReason = null;
        var stock = await StockStatusAsync(unit, ct);
        var changed = await ChangeStatusAsync(unit, stock,
            stock == UnitStatus.Received
                ? $"Released from operational hold: {releaseReason} ({RetypeStockStatus.PendingReason})"
                : $"Released from operational hold: {releaseReason}", ct);
        if (!changed.Succeeded)
        {
            return changed;
        }

        _audit.Record(
            AuditEventType.ProductStatus,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = UnitStatus.OnHold, HoldReason = heldFor },
            newValue: new { unit.UnitNumber, unit.Status, Path = "HoldRelease", ReleaseReason = releaseReason },
            reason: $"Unit released from hold: {releaseReason}");
        await _unitOfWork.SaveChangesAsync(ct);
        return changed;
    }

    /// <summary>
    /// SoftBank/SafeTrace inventory discrepancy: the unit cannot be located.
    /// Missing units are not issuable (21 CFR 606.165 chain of custody).
    /// </summary>
    public async Task<InventoryActionResult> MarkMissingAsync(long unitId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return InventoryActionResult.Fail("A reason is required to mark a unit missing.");

        var denied = await RejectUnauthorizedAsync(InventoryAuthorizationRule.EvaluateMarkMissing, ct);
        if (denied is not null)
        {
            return denied;
        }

        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
            return InventoryActionResult.Fail("Unit not found.");

        if (RefuseRepeat(unit, UnitStatus.Missing, "marked missing") is { } repeat)
            return repeat;

        unit.MissingReason = reason.Trim();
        var fromStatus = unit.Status;
        var missing = await ChangeStatusAsync(unit, UnitStatus.Missing, reason.Trim(), ct);
        if (!missing.Succeeded)
        {
            return missing;
        }

        _audit.Record(
            AuditEventType.ProductStatus,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = fromStatus },
            newValue: new
            {
                unit.UnitNumber,
                Status = UnitStatus.Missing,
                Path = "MarkMissing",
                MissingReason = reason.Trim()
            },
            reason: "Unit marked missing.");
        await _unitOfWork.SaveChangesAsync(ct);
        return missing;
    }

    /// <summary>
    /// Recovered units enter quality quarantine for inspection before they can return to Available.
    /// </summary>
    public async Task<InventoryActionResult> LocateMissingAsync(long unitId, string? foundNote = null, CancellationToken ct = default)
    {
        // Where and how the unit turned up is the custody record for the time it was missing.
        if (string.IsNullOrWhiteSpace(foundNote))
            return InventoryActionResult.Fail("Say where and how the unit was found.");

        var denied = await RejectUnauthorizedAsync(
            InventoryAuthorizationRule.EvaluateLocateMissing, ct);
        if (denied is not null)
        {
            return denied;
        }

        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
            return InventoryActionResult.Fail("Unit not found.");

        if (unit.Status != UnitStatus.Missing)
            return InventoryActionResult.Fail("Only a missing unit can be located back into inventory.");

        var prior = unit.MissingReason;
        unit.MissingReason = null;
        unit.QuarantineReasonCode = UnitQuarantineReason.LocatedAfterMissing;
        var found = foundNote.Trim();
        unit.QuarantineReason = string.IsNullOrWhiteSpace(prior)
            ? $"Located after missing: {found}; pending inspection"
            : $"Located after missing ({prior}): {found}; pending inspection";
        var fromStatus = unit.Status;
        var located = await ChangeStatusAsync(unit, UnitStatus.Quarantine, unit.QuarantineReason, ct);
        if (!located.Succeeded)
        {
            return located;
        }

        _audit.Record(
            AuditEventType.ProductStatus,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = fromStatus },
            newValue: new
            {
                unit.UnitNumber,
                Status = UnitStatus.Quarantine,
                Path = "LocateMissing",
                ReasonCode = UnitQuarantineReason.LocatedAfterMissing,
                PriorReason = prior,
                Found = found
            },
            reason: $"Unit located after missing: {found}");
        await _unitOfWork.SaveChangesAsync(ct);
        return located;
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

        var denied = await RejectUnauthorizedAsync(InventoryAuthorizationRule.EvaluateMarkDamaged, ct);
        if (denied is not null)
        {
            return denied;
        }

        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
            return InventoryActionResult.Fail("Unit not found.");

        if (RefuseRepeat(unit, UnitStatus.Damaged, "marked damaged") is { } repeat)
            return repeat;

        unit.DamagedReason = reason.Trim();
        var fromStatus = unit.Status;
        var damaged = await ChangeStatusAsync(unit, UnitStatus.Damaged, reason.Trim(), ct);
        if (!damaged.Succeeded)
        {
            return damaged;
        }

        _audit.Record(
            AuditEventType.ProductStatus,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = fromStatus },
            newValue: new
            {
                unit.UnitNumber,
                Status = UnitStatus.Damaged,
                Path = "MarkDamaged",
                DamagedReason = reason.Trim()
            },
            reason: "Unit marked damaged.");
        await _unitOfWork.SaveChangesAsync(ct);
        return damaged;
    }

    /// <summary>
    /// Inspected damaged units enter quality quarantine; they cannot return directly to Available.
    /// </summary>
    public async Task<InventoryActionResult> InspectDamagedAsync(long unitId, string? findings = null, CancellationToken ct = default)
    {
        // What the inspection showed is what quality review decides on.
        if (string.IsNullOrWhiteSpace(findings))
            return InventoryActionResult.Fail("Say what the inspection showed.");

        var denied = await RejectUnauthorizedAsync(
            InventoryAuthorizationRule.EvaluateInspectDamaged, ct);
        if (denied is not null)
        {
            return denied;
        }

        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
            return InventoryActionResult.Fail("Unit not found.");

        if (unit.Status != UnitStatus.Damaged)
            return InventoryActionResult.Fail("Only a damaged unit can be moved to quarantine for inspection.");

        var prior = unit.DamagedReason;
        unit.DamagedReason = null;
        unit.QuarantineReasonCode = UnitQuarantineReason.InspectedAfterDamage;
        var seen = findings.Trim();
        unit.QuarantineReason = string.IsNullOrWhiteSpace(prior)
            ? $"Inspected after damage: {seen}; pending quality review"
            : $"Inspected after damage ({prior}): {seen}; pending quality review";
        var fromStatus = unit.Status;
        var inspected = await ChangeStatusAsync(unit, UnitStatus.Quarantine, unit.QuarantineReason, ct);
        if (!inspected.Succeeded)
        {
            return inspected;
        }

        _audit.Record(
            AuditEventType.ProductStatus,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = fromStatus },
            newValue: new
            {
                unit.UnitNumber,
                Status = UnitStatus.Quarantine,
                Path = "InspectDamaged",
                ReasonCode = UnitQuarantineReason.InspectedAfterDamage,
                PriorReason = prior,
                Findings = seen
            },
            reason: $"Unit inspected after damage: {seen}");
        await _unitOfWork.SaveChangesAsync(ct);
        return inspected;
    }

    /// <summary>
    /// Returns the unit in hand to its supplier by its scan (a return shipment: one reason for all), with the same checks
    /// and audit as <see cref="ReturnToSupplierAsync"/>. A divided number matching several units returns none; a unit whose
    /// status does not allow a return (reserved, issued, already returned...) is refused with its status.
    /// </summary>
    public async Task<InventoryActionResult> ReturnToSupplierByScanAsync(ReturnToSupplierByScanRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return InventoryActionResult.Fail("A reason is required to return a unit to the supplier.");
        }

        var (key, candidates, error) = await ResolveScanAsync(request.Scan, ct);
        if (error is not null)
        {
            return InventoryActionResult.Fail(error);
        }

        // A unit already returned counts as "allowed" by the transition table (same status), so it is excluded first.
        var returnable = candidates
            .Where(u => u.Status != UnitStatus.ReturnedToSupplier && InventoryStatusTransition.IsAllowed(u.Status, UnitStatus.ReturnedToSupplier))
            .ToList();
        if (returnable.Count == 0)
        {
            var unit = candidates[0];
            return InventoryActionResult.Fail(unit.Status == UnitStatus.ReturnedToSupplier
                ? $"{unit.UnitNumber} is already returned to the supplier."
                : $"{unit.UnitNumber} is {unit.Status}; it cannot be returned to the supplier. Release its reservation or resolve it first.");
        }

        if (returnable.Count > 1)
        {
            return InventoryActionResult.Fail(
                $"{key} matches {returnable.Count} units ({string.Join(", ", returnable.Select(u => u.UnitNumber + (string.IsNullOrWhiteSpace(u.DivisionCode) ? "" : $" division {u.DivisionCode}")))}). Return each from its unit page.");
        }

        return await ReturnToSupplierAsync(returnable[0].Id, request.Reason, ct);
    }

    /// <summary>
    /// Cycle 690: a recalled unit found and scanned is quarantined for the recall (coded Lookback recall, noting the
    /// recall's reason), with the same checks and audit as <see cref="QuarantineAsync(long, UnitQuarantineReason, string?, CancellationToken)"/>.
    /// The scan is the proof it was found. A unit that is not Recalled is refused with its status; a divided number
    /// matching several recalled units pulls none.
    /// </summary>
    public async Task<InventoryActionResult> PullRecalledByScanAsync(PullRecalledByScanRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (key, candidates, error) = await ResolveScanAsync(request.Scan, ct);
        if (error is not null)
        {
            return InventoryActionResult.Fail(error);
        }

        var recalled = candidates.Where(u => u.Status == UnitStatus.Recalled).ToList();
        if (recalled.Count == 0)
        {
            var unit = candidates[0];
            return InventoryActionResult.Fail($"{unit.UnitNumber} is {unit.Status}, not a recalled unit waiting to be pulled.");
        }

        if (recalled.Count > 1)
        {
            return InventoryActionResult.Fail(
                $"{key} matches {recalled.Count} recalled units ({string.Join(", ", recalled.Select(u => u.UnitNumber + (string.IsNullOrWhiteSpace(u.DivisionCode) ? "" : $" division {u.DivisionCode}")))}). Quarantine each from its unit page.");
        }

        var found = recalled[0];
        if (found.TransfusionReportedUtc is not null)
        {
            return InventoryActionResult.Fail(
                $"{found.UnitNumber} was reported transfused by the EHR after it was recalled; it is reviewed on the supervisor queue, not pulled.");
        }

        // Cycle 693: a unit recalled while it was issued is on the ward with its issue open; it is returned on Issue &
        // Transfuse, which closes the issue and quarantines it, rather than quarantined here behind an open issue.
        var recallRow = (await _repository.GetHistoryAsync(found.Id, ct))
            .Where(h => h.ToStatus == UnitStatus.Recalled)
            .OrderByDescending(h => h.ChangedUtc).ThenByDescending(h => h.Id)
            .FirstOrDefault();
        if (recallRow?.FromStatus is UnitStatus.Issued or UnitStatus.TransfusionStarted)
        {
            return InventoryActionResult.Fail(
                $"{found.UnitNumber} was issued when it was recalled. Return it on Issue & Transfuse; the return quarantines it for the recall.");
        }

        var note = string.IsNullOrWhiteSpace(found.RecallReason)
            ? "Pulled after recall"
            : $"Pulled after recall: {found.RecallReason.Trim()}";
        return await QuarantineAsync(found.Id, UnitQuarantineReason.LookbackRecall, note, ct);
    }

    /// <summary>
    /// Shipments to other facilities in the last <paramref name="days"/> days, newest first, each with its units, so what
    /// was sent where can be answered and its packing list printed again.
    /// </summary>
    public async Task<IReadOnlyList<OutboundShipmentDto>> ListShipmentsAsync(int days = 30, CancellationToken ct = default)
    {
        var since = _clock.UtcNow.AddDays(-Math.Clamp(days, 1, 366));
        var shipped = await _repository.ListShippedOutAsync(since, ct);
        return shipped
            .GroupBy(u => ShipOutRule.ShipmentKey(u.ShippedToFacility, u.OutboundShipmentReference, u.ShippedUtc))
            .Select(g =>
            {
                var ordered = g.OrderBy(u => u.ShippedUtc).ThenBy(u => u.UnitNumber, StringComparer.OrdinalIgnoreCase).ToList();
                return new OutboundShipmentDto(
                    ordered[0].ShippedToFacility ?? string.Empty,
                    ordered[0].OutboundShipmentReference,
                    ordered[0].ShippedUtc!.Value,
                    ordered[^1].ShippedUtc!.Value,
                    ordered.Select(BloodUnitDto.From).ToList(),
                    ordered[0].ShipContainer,
                    ordered[0].ShipPackingTemperatureC,
                    ordered[0].ShipPackedBy,
                    ordered[0].ShipReceivingContact);
            })
            .OrderByDescending(s => s.LastShippedUtc)
            .ToList();
    }

    /// <summary>Facilities units have been shipped to, most recent first, one spelling each (for the facility field).</summary>
    public async Task<IReadOnlyList<string>> KnownShipFacilitiesAsync(CancellationToken ct = default)
    {
        var shipped = await _repository.ListShippedOutAsync(null, ct);
        return shipped
            .OrderByDescending(u => u.ShippedUtc)
            .Select(u => u.ShippedToFacility!.Trim())
            .Where(f => f.Length > 0)
            .DistinctBy(f => ShipOutRule.ShipmentKey(f, "x", null))
            .ToList();
    }

    /// <summary>
    /// Ships the unit in hand to another facility by its scan: the receiving facility (required) and the shipment reference
    /// are kept on the unit so a recall or lookback notifies that facility. Only an unreserved, unexpired unit in stock
    /// ships (<see cref="ShipOutRule"/>); a divided number matching several units ships none. Audited.
    /// </summary>
    public async Task<InventoryActionResult> ShipOutByScanAsync(ShipOutByScanRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = await RejectUnauthorizedReturnToSupplierAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        if (string.IsNullOrWhiteSpace(request.Facility))
        {
            return InventoryActionResult.Fail("Name the facility the units are shipped to.");
        }

        var details = ShipOutRule.EvaluateDetails(request.Container, request.PackingTemperatureC, request.PackedBy, request.ReceivingContact);
        if (details.Severity == RuleSeverity.HardStop)
        {
            return InventoryActionResult.Fail(details.Message);
        }

        var (key, candidates, error) = await ResolveScanAsync(request.Scan, ct);
        if (error is not null)
        {
            return InventoryActionResult.Fail(error);
        }

        if (candidates.Count > 1)
        {
            return InventoryActionResult.Fail(
                $"{key} matches {candidates.Count} units ({string.Join(", ", candidates.Select(u => u.UnitNumber + (string.IsNullOrWhiteSpace(u.DivisionCode) ? "" : $" division {u.DivisionCode}")))}). Scan the division's own label.");
        }

        var unit = await _repository.GetUnitAsync(candidates[0].Id, ct);
        if (unit is null)
        {
            return InventoryActionResult.Fail("Unit not found.");
        }

        var now = _clock.UtcNow;
        var check = ShipOutRule.Evaluate(unit.Status, unit.ExpiresUtc, now, request.Facility, request.Reference);
        if (check.Severity == RuleSeverity.HardStop)
        {
            return InventoryActionResult.Fail($"{unit.UnitNumber}: {check.Message}");
        }

        var facility = request.Facility.Trim();
        var reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim();
        var reason = $"Shipped to {facility}" + (reference is null ? "" : $" ({reference})")
            + (string.IsNullOrWhiteSpace(request.Note) ? "" : $": {request.Note.Trim()}");
        var fromStatus = unit.Status;
        unit.ShippedToFacility = facility;
        unit.ShippedUtc = now;
        unit.OutboundShipmentReference = reference;
        unit.ShipContainer = request.Container!.Trim();
        unit.ShipPackingTemperatureC = request.PackingTemperatureC;
        unit.ShipPackedBy = request.PackedBy!.Trim();
        unit.ShipReceivingContact = request.ReceivingContact!.Trim();
        var shipped = await ChangeStatusAsync(unit, UnitStatus.ShippedOut, reason, ct);
        if (!shipped.Succeeded)
        {
            return shipped;
        }

        _audit.Record(
            AuditEventType.ProductStatus,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = fromStatus },
            newValue: new
            {
                unit.UnitNumber, Status = UnitStatus.ShippedOut, Path = "ShipOut", ShippedToFacility = facility, OutboundShipmentReference = reference,
                unit.ShipContainer, unit.ShipPackingTemperatureC, unit.ShipPackedBy, unit.ShipReceivingContact
            },
            reason: reason);
        await _unitOfWork.SaveChangesAsync(ct);
        return shipped;
    }

    /// <summary>
    /// SoftBank/SafeTrace consignee reject / unused-stock return to the supplier.
    /// Distinct from ward <see cref="UnitStatus.Returned"/> and from packing-list
    /// <see cref="UnitStatus.CancelledAssignment"/>. Terminal; not issuable.
    /// </summary>
    /// <summary>
    /// Cycle 686: returns to the supplier in the last <paramref name="days"/> days, newest first, each with its units, so
    /// what went back can be answered and its return list printed again.
    /// </summary>
    public async Task<IReadOnlyList<SupplierReturnDto>> ListSupplierReturnsAsync(int days = 30, CancellationToken ct = default)
    {
        var since = _clock.UtcNow.AddDays(-Math.Clamp(days, 1, 366));
        var returned = await _repository.ListReturnedToSupplierAsync(since, ct);
        return returned
            .GroupBy(r => SupplierReturnListRule.ReturnKey(r.Unit.SupplierReturnReason ?? r.Returned.Reason, r.Returned.ChangedUtc))
            .Select(g =>
            {
                var ordered = g.OrderBy(r => r.Returned.ChangedUtc).ThenBy(r => r.Unit.UnitNumber, StringComparer.OrdinalIgnoreCase).ToList();
                return new SupplierReturnDto(
                    g.Key,
                    ordered[0].Unit.SupplierReturnReason ?? ordered[0].Returned.Reason ?? string.Empty,
                    ordered[0].Returned.ChangedUtc,
                    ordered[^1].Returned.ChangedUtc,
                    ordered.Select(r => r.Returned.ChangedBy).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    ordered.Select(r => r.Unit.Supplier).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!)
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    ordered.Select(r => BloodUnitDto.From(r.Unit)).ToList());
            })
            .OrderByDescending(r => r.LastReturnedUtc)
            .ToList();
    }

    public async Task<InventoryActionResult> ReturnToSupplierAsync(long unitId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return InventoryActionResult.Fail("A reason is required to return a unit to the supplier.");

        var denied = await RejectUnauthorizedReturnToSupplierAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
            return InventoryActionResult.Fail("Unit not found.");

        if (RefuseRepeat(unit, UnitStatus.ReturnedToSupplier, "returned to the supplier") is { } repeat)
            return repeat;

        unit.SupplierReturnReason = reason.Trim();
        var fromStatus = unit.Status;
        var returned = await ChangeStatusAsync(unit, UnitStatus.ReturnedToSupplier, reason.Trim(), ct);
        if (!returned.Succeeded)
        {
            return returned;
        }

        _audit.Record(
            AuditEventType.ProductStatus,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = fromStatus },
            newValue: new
            {
                unit.UnitNumber,
                Status = UnitStatus.ReturnedToSupplier,
                Path = "ReturnToSupplier",
                SupplierReturnReason = reason.Trim()
            },
            reason: "Unit returned to supplier.");
        await _unitOfWork.SaveChangesAsync(ct);
        return returned;
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

        var denied = await RejectUnauthorizedAsync(
            InventoryAuthorizationRule.EvaluateDirectedConversion, ct);
        if (denied is not null)
        {
            return denied;
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
            AuditEventType.ProductStatus,
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

    /// <summary>
    /// Moves the unit in hand to a location by its scanned unit number (DIN, with or without the scanner's '=') or
    /// component label, with the same checks and audit as <see cref="TransferAsync"/>. A scan matching several units
    /// (a divided DIN) moves none; each is moved from its unit page. A unit already in the location is not "moved".
    /// </summary>
    public async Task<InventoryActionResult> TransferByScanAsync(TransferByScanRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (key, candidates, error) = await ResolveScanAsync(request.Scan, ct);
        if (error is not null)
        {
            return InventoryActionResult.Fail(error);
        }

        var movable = candidates.Where(u => TransferableStatuses.Contains(u.Status)).ToList();
        if (movable.Count == 0)
        {
            return InventoryActionResult.Fail($"{candidates[0].UnitNumber} is {candidates[0].Status}; it cannot be moved.");
        }

        if (movable.Count > 1)
        {
            return InventoryActionResult.Fail(
                $"{key} matches {movable.Count} units ({string.Join(", ", movable.Select(u => u.UnitNumber + (string.IsNullOrWhiteSpace(u.DivisionCode) ? "" : $" division {u.DivisionCode}")))}). Scan its product code label next to pick the division.");
        }

        var unit = movable[0];
        if (unit.CurrentLocationId == request.ToLocationId)
        {
            return InventoryActionResult.Fail($"{unit.UnitNumber} is already in that location.");
        }

        return await TransferAsync(unit.Id, request.ToLocationId,
            string.IsNullOrWhiteSpace(request.Reason) ? "Moved by scan" : request.Reason.Trim(), ct);
    }

    public const string ExpiredDiscardReasonCode = "EXPIRED";

    // Expired by the sweep, or past its expiry on a shelf before the next sweep runs.
    private static bool IsExpiredOnShelf(BloodUnit u, DateTime now) =>
        u.Status == UnitStatus.Expired || (InventoryUnitScopes.OnHand.Contains(u.Status) && u.ExpiresUtc <= now);

    /// <summary>
    /// Expired units still on a shelf (marked Expired, or past their expiry before the sweep runs), oldest expiry first,
    /// with where each is recorded, so they can be pulled and discarded. Needs the discard permission.
    /// </summary>
    public async Task<OperationResult<IReadOnlyList<ExpiredUnitDto>>> ListExpiredOnShelfAsync(CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedDiscardAsync(ct);
        if (denied is not null)
        {
            return OperationResult<IReadOnlyList<ExpiredUnitDto>>.Fail(denied.Error ?? "Not permitted.");
        }

        var now = _clock.UtcNow;
        var rows = (await _repository.SearchAsync(new InventorySearchCriteria(Statuses: InventoryUnitScopes.OnShelf), ct))
            .Where(u => IsExpiredOnShelf(u, now))
            .ToList();
        var products = await LoadProductsAsync(rows, ct);
        var locations = await LoadLocationNamesAsync(rows.Select(u => u.CurrentLocationId).OfType<long>(), ct);
        return OperationResult<IReadOnlyList<ExpiredUnitDto>>.Ok(rows
            .OrderBy(u => u.ExpiresUtc)
            .ThenBy(u => u.UnitNumber, StringComparer.Ordinal)
            .Select(u =>
            {
                products.TryGetValue(u.ProductTypeId, out var product);
                string? location = null;
                if (u.CurrentLocationId is long id)
                {
                    locations.TryGetValue(id, out location);
                }

                return new ExpiredUnitDto(u.Id, u.UnitNumber, product?.ProductCode, product?.Name, u.BloodType.ToString(), u.Status, u.ExpiresUtc, location);
            })
            .ToList());
    }

    /// <summary>
    /// How many expired units are still on a shelf, without the discard permission check, for summaries (shift handoff)
    /// whose caller is authorized for its own action. Read-only.
    /// </summary>
    public async Task<int> CountExpiredOnShelfAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        return (await _repository.SearchAsync(new InventorySearchCriteria(Statuses: InventoryUnitScopes.OnShelf), ct))
            .Count(u => IsExpiredOnShelf(u, now));
    }

    /// <summary>
    /// Discards the expired unit in hand by its scan, with the EXPIRED discard reason and the same checks and audit as a
    /// discard from the unit page (permission, second verifier when required). A unit that is not expired, or a divided
    /// number matching several units, is refused; those are discarded from the unit page with their own reason.
    /// </summary>
    public async Task<InventoryActionResult> DiscardExpiredByScanAsync(DiscardExpiredByScanRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (key, candidates, error) = await ResolveScanAsync(request.Scan, ct);
        if (error is not null)
        {
            return InventoryActionResult.Fail(error);
        }

        var now = _clock.UtcNow;
        var expired = candidates.Where(u => IsExpiredOnShelf(u, now)).ToList();
        if (expired.Count == 0)
        {
            var unit = candidates[0];
            return InventoryActionResult.Fail(unit.Status == UnitStatus.Discarded
                ? $"{unit.UnitNumber} is already discarded."
                : $"{unit.UnitNumber} is not expired ({unit.Status}, expires {ClinicalTimeText.Local(unit.ExpiresUtc)}). Discard it from its unit page with its own reason.");
        }

        if (expired.Count > 1)
        {
            return InventoryActionResult.Fail(
                $"{key} matches {expired.Count} expired units ({string.Join(", ", expired.Select(u => u.UnitNumber + (string.IsNullOrWhiteSpace(u.DivisionCode) ? "" : $" division {u.DivisionCode}")))}). Discard each from its unit page.");
        }

        return await DiscardAsync(expired[0].Id,
            new DiscardUnitCommand(ExpiredDiscardReasonCode, "Discarded by scan from the expired list", request.SecondVerifier), ct);
    }

    /// <summary>
    /// The units a scanned unit number (DIN, with or without the scanner's '='), or a component label, names: one, or
    /// several for a divided DIN. An empty, unreadable, or unknown scan gives the reason instead.
    /// </summary>
    private Task<(string Key, List<BloodUnit> Units, string? Error)> ResolveScanAsync(string? scan, CancellationToken ct) =>
        InventoryScanResolver.ResolveAsync(_repository, scan, ct);

    /// <summary>
    /// A stock count at one location: what is recorded there (on hand or expired, not yet discarded) against what was
    /// scanned. Found, Missing (recorded here, not scanned), Elsewhere (scanned here, recorded in another place), Not on
    /// hand (scanned here but issued, transfused, discarded...), and scans that name no unit or a divided number. Moves
    /// nothing. With <see cref="InventoryCountRequest.Finish"/> the outcome is recorded in the audit trail.
    /// </summary>
    public async Task<OperationResult<InventoryCountDto>> ReconcileCountAsync(InventoryCountRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = await RejectUnauthorizedTransferAsync(ct);
        if (denied is not null)
        {
            return OperationResult<InventoryCountDto>.Fail(denied.Error ?? "Not permitted.");
        }

        var location = _locations is null ? null : await _locations.GetByIdAsync(request.LocationId, ct);
        if (_locations is not null && location is null)
        {
            return OperationResult<InventoryCountDto>.Fail("Location not found.");
        }

        var locationName = location is null ? $"location {request.LocationId}" : $"{location.Code} {location.Name}".Trim();
        var expected = await _repository.SearchAsync(
            new InventorySearchCriteria(LocationId: request.LocationId, Statuses: InventoryUnitScopes.OnShelf), ct);
        var expectedIds = expected.Select(u => u.Id).ToHashSet();
        var products = _productTypes is null
            ? new Dictionary<long, ProductType>()
            : (await _productTypes.ListAsync(ct)).ToDictionary(p => p.Id);
        var locationNames = _locations is null
            ? new Dictionary<long, string>()
            : (await _locations.ListAsync(ct)).ToDictionary(l => l.Id, l => $"{l.Code} {l.Name}".Trim());
        var now = _clock.UtcNow;

        InventoryCountLineDto Line(BloodUnit u, InventoryCountOutcome outcome)
        {
            var product = products.GetValueOrDefault(u.ProductTypeId);
            var wrongStorage = location is not null && product is not null
                && outcome is InventoryCountOutcome.Found or InventoryCountOutcome.Missing
                && !InventoryLocationPolicyRule.AllowsComponent(location, product.ComponentClass);
            return new(
                u.Id, u.UnitNumber, product?.Name, u.BloodType.ToString(), u.Status, u.ExpiresUtc, outcome,
                u.CurrentLocationId is long l ? locationNames.GetValueOrDefault(l) ?? $"location {l}" : null)
            {
                Expired = u.Status == UnitStatus.Expired || u.ExpiresUtc <= now,
                // A unit in a place that does not take its component, such as platelets in a red-cell refrigerator.
                WrongStorage = wrongStorage ? $"{location!.Code} does not take {ComponentText(product!.ComponentClass)}" : null
            };
        }

        var foundIds = new HashSet<long>();
        var elsewhere = new Dictionary<long, InventoryCountLineDto>();
        var notOnHand = new Dictionary<long, InventoryCountLineDto>();
        var problems = new List<InventoryCountProblemDto>();
        foreach (var scan in request.Scans.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            var (key, units, error) = await ResolveScanAsync(scan, ct);
            if (error is not null)
            {
                problems.Add(new InventoryCountProblemDto(scan.Trim(), error));
                continue;
            }

            var here = units.Where(u => expectedIds.Contains(u.Id)).ToList();
            if (units.Count > 1 && here.Count != 1)
            {
                problems.Add(new InventoryCountProblemDto(scan.Trim(),
                    $"{key} matches {units.Count} units ({string.Join(", ", units.Select(u => u.UnitNumber + (string.IsNullOrWhiteSpace(u.DivisionCode) ? "" : $" division {u.DivisionCode}")))}); scan its product code label next to pick the division."));
                continue;
            }

            var unit = here.Count == 1 ? here[0] : units[0];
            if (expectedIds.Contains(unit.Id))
            {
                foundIds.Add(unit.Id);
            }
            else if (InventoryUnitScopes.OnShelf.Contains(unit.Status))
            {
                elsewhere[unit.Id] = Line(unit, InventoryCountOutcome.Elsewhere);
            }
            else
            {
                notOnHand[unit.Id] = Line(unit, InventoryCountOutcome.NotOnHand);
            }
        }

        var dto = new InventoryCountDto(
            request.LocationId,
            locationName,
            expected.Count,
            request.Scans.Count(s => !string.IsNullOrWhiteSpace(s)),
            expected.Where(u => foundIds.Contains(u.Id)).OrderBy(u => u.UnitNumber).Select(u => Line(u, InventoryCountOutcome.Found)).ToList(),
            expected.Where(u => !foundIds.Contains(u.Id)).OrderBy(u => u.UnitNumber).Select(u => Line(u, InventoryCountOutcome.Missing)).ToList(),
            elsewhere.Values.OrderBy(l => l.UnitNumber).ToList(),
            notOnHand.Values.OrderBy(l => l.UnitNumber).ToList(),
            problems,
            request.Finish);

        if (request.Finish)
        {
            _audit.Record(
                AuditEventType.ProductStatus,
                nameof(InventoryLocation),
                request.LocationId,
                newValue: new
                {
                    Document = "Stock count",
                    Location = locationName,
                    dto.Expected,
                    dto.Scanned,
                    Found = dto.Found.Count,
                    Missing = dto.Missing.Select(l => l.UnitNumber).ToList(),
                    Elsewhere = dto.Elsewhere.Select(l => l.UnitNumber).ToList(),
                    NotOnHand = dto.NotOnHand.Select(l => $"{l.UnitNumber} ({l.Status})").ToList(),
                    Unresolved = dto.Problems.Select(p => p.Scan).ToList()
                },
                reason: dto.Reconciled
                    ? $"Stock count at {locationName}: all {dto.Expected} units found."
                    : $"Stock count at {locationName}: {dto.Found.Count} of {dto.Expected} found, {dto.Missing.Count} missing, {dto.Elsewhere.Count} recorded elsewhere, {dto.NotOnHand.Count} not on hand, {dto.Problems.Count} unresolved scans.");
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return OperationResult<InventoryCountDto>.Ok(dto);
    }

    private const string StockCountReasonPrefix = "Stock count at ";

    private static string ComponentText(ComponentClass component) => component switch
    {
        ComponentClass.RedBloodCells => "red cells",
        ComponentClass.WholeBlood => "whole blood",
        ComponentClass.Plasma => "plasma",
        ComponentClass.Platelets => "platelets",
        ComponentClass.Cryoprecipitate => "cryoprecipitate",
        ComponentClass.Granulocytes => "granulocytes",
        _ => "this component"
    };

    /// <summary>
    /// Finished stock counts, newest first, read from the audit events each count writes against its location.
    /// A location id narrows to one location.
    /// </summary>
    public async Task<OperationResult<IReadOnlyList<InventoryCountHistoryDto>>> ListCountHistoryAsync(
        long? locationId = null, int take = 20, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedTransferAsync(ct);
        if (denied is not null)
        {
            return OperationResult<IReadOnlyList<InventoryCountHistoryDto>>.Fail(denied.Error ?? "Not permitted.");
        }

        if (_auditQuery is null || _locations is null)
        {
            return OperationResult<IReadOnlyList<InventoryCountHistoryDto>>.Ok([]);
        }

        var locations = (await _locations.ListAsync(ct)).Where(l => locationId is null || l.Id == locationId).ToList();
        var history = new List<InventoryCountHistoryDto>();
        foreach (var location in locations)
        {
            var events = await _auditQuery.ListForEntityAsync(nameof(InventoryLocation), location.Id, ct);
            history.AddRange(events
                .Where(e => e.Reason is not null && e.Reason.StartsWith(StockCountReasonPrefix, StringComparison.Ordinal))
                .Select(e => new InventoryCountHistoryDto(
                    location.Id,
                    $"{location.Code} {location.Name}".Trim(),
                    e.OccurredUtc,
                    e.UserName,
                    e.Reason!,
                    e.Reason!.Contains(": all ", StringComparison.Ordinal))));
        }

        return OperationResult<IReadOnlyList<InventoryCountHistoryDto>>.Ok(
            history.OrderByDescending(h => h.CountedUtc).Take(Math.Clamp(take, 1, 200)).ToList());
    }

    /// <summary>
    /// The storage check a transfer makes, for a unit received into a location (end-user testing, 2026-11-12: units were
    /// received into a location that does not take their component, such as platelets into a red-cell refrigerator).
    /// Null when no location is given or it may hold the component.
    /// </summary>
    private async Task<InventoryActionResult?> RejectStorageAsync(long? locationId, ProductType? productType, CancellationToken ct)
    {
        if (locationId is not long id || _locations is null)
        {
            return null;
        }

        var location = await _locations.GetByIdAsync(id, ct);
        var component = productType?.ComponentClass ?? ComponentClass.Other;
        var evaluation = new RuleEvaluation(InventoryLocationPolicyRule.EvaluateTransfer(
            location is not null,
            location?.IsActive ?? false,
            location is not null && InventoryLocationPolicyRule.AllowsComponent(location, component),
            location?.StorageTempMinC,
            location?.StorageTempMaxC));
        return evaluation.IsHardStopped ? InventoryActionResult.Blocked(evaluation) : null;
    }

    public async Task<InventoryActionResult> TransferAsync(long unitId, long toLocationId, string? reason, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedTransferAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
        {
            return InventoryActionResult.Fail("Unit not found.");
        }

        if (!TransferableStatuses.Contains(unit.Status))
        {
            return InventoryActionResult.Fail($"A unit with status {unit.Status} cannot be transferred.");
        }

        if (_locations is not null)
        {
            var destination = await _locations.GetByIdAsync(toLocationId, ct);
            var productType = unit.ProductType
                ?? (_productTypes is not null ? await _productTypes.GetByIdAsync(unit.ProductTypeId, ct) : null);
            var component = productType?.ComponentClass ?? ComponentClass.Other;
            var storage = InventoryLocationPolicyRule.EvaluateTransfer(
                destination is not null,
                destination?.IsActive ?? false,
                destination is not null && InventoryLocationPolicyRule.AllowsComponent(destination, component),
                destination?.StorageTempMinC,
                destination?.StorageTempMaxC);
            var evaluation = new RuleEvaluation(storage);
            if (evaluation.IsHardStopped)
            {
                return InventoryActionResult.Blocked(evaluation);
            }
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

        _audit.Record(
            AuditEventType.ProductStatus,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { LocationId = fromLocationId, unit.Status },
            newValue: new
            {
                unit.UnitNumber,
                unit.Status,
                LocationId = toLocationId,
                Path = "Transfer",
                TransferReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()
            },
            reason: "Unit transferred.");
        await _unitOfWork.SaveChangesAsync(ct);
        return InventoryActionResult.Ok(unit);
    }

    /// <summary>
    /// Dangerous action: requires a reason, a distinct second verifier when policy
    /// demands it, and is recorded as a named Discard audit event in addition to the
    /// automatic Update audit (see docs/safety-rules.md 5).
    /// </summary>
    public async Task<InventoryActionResult> DiscardAsync(
        long unitId, DiscardUnitCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var secondVerifier = command.SecondVerifier;
        var reasonCode = command.ReasonCode?.Trim().ToUpperInvariant();
        var catalogReason = string.IsNullOrEmpty(reasonCode) || _discardReasons is null
            ? null
            : await _discardReasons.FirstOrDefaultAsync(r => r.Code == reasonCode, ct);
        var coded = DiscardReasonRule.Evaluate(reasonCode, catalogReason, command.Comment);
        if (coded.Severity == RuleSeverity.HardStop)
        {
            return InventoryActionResult.Blocked(new RuleEvaluation([coded]));
        }

        var reason = DiscardReasonRule.StoredText(catalogReason!, command.Comment);

        var denied = await RejectUnauthorizedDiscardAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var unit = await _repository.GetUnitAsync(unitId, ct);
        if (unit is null)
        {
            return InventoryActionResult.Fail("Unit not found.");
        }

        if (RefuseRepeat(unit, UnitStatus.Discarded, "discarded") is { } repeat)
        {
            return repeat;
        }

        var transition = InventoryStatusTransition.Evaluate(unit.Status, UnitStatus.Discarded);
        if (transition.Severity == RuleSeverity.HardStop)
        {
            return InventoryActionResult.Blocked(new RuleEvaluation(new[] { transition }));
        }

        if (ReportedTransfusionRule.Evaluate(unit.TransfusionReportedUtc, unit.UnitNumber) is { Severity: RuleSeverity.HardStop } reported)
        {
            return InventoryActionResult.Blocked(new RuleEvaluation(new[] { reported }));
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
        unit.DiscardReasonCode = catalogReason!.Code;
        var historyReason = string.IsNullOrWhiteSpace(secondVerifier)
            ? $"{catalogReason.Code}: {reason}"
            : $"{catalogReason.Code}: {reason}; second verifier {secondVerifier.Trim()}";

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
            newValue: new { Status = UnitStatus.Discarded, DiscardReasonCode = catalogReason.Code, DiscardReason = reason, SecondVerifier = secondVerifier },
            reason: historyReason);

        await _unitOfWork.SaveChangesAsync(ct);
        return InventoryActionResult.Ok(unit);
    }

    /// <summary>Sweeps units that are at/past expiration into the Expired status.</summary>
    public async Task<InventoryActionResult> ExpireDueUnitsAsync(CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedExpireAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        return await SweepExpiredUnitsAsync("ExpireSweep", ct);
    }

    /// <summary>
    /// Scheduled expiration sweep for the hosted service. It runs as the system
    /// account with no user permission check, so no endpoint may call it.
    /// </summary>
    public Task<InventoryActionResult> RunScheduledExpirationSweepAsync(CancellationToken ct = default) =>
        SweepExpiredUnitsAsync("ScheduledExpireSweep", ct);

    private async Task<InventoryActionResult> SweepExpiredUnitsAsync(string path, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var due = await _repository.GetExpirableUnitsAsync(now, ct);
        if (due.Count == 0)
        {
            return InventoryActionResult.OkSweep(0);
        }

        var expired = 0;
        var reservationsEnded = 0;
        var reservationsEndedFor = new List<string>();
        foreach (var unit in due)
        {
            if (!InventoryStatusTransition.IsAllowed(unit.Status, UnitStatus.Expired) || unit.TransfusionReportedUtc is not null)
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
                Reason = $"Expired at {ClinicalTimeText.Utc(unit.ExpiresUtc)}",
                ChangedBy = _currentUser.UserName,
                ChangedUtc = now
            });

            _audit.Record(
                AuditEventType.ProductStatus,
                nameof(BloodUnit),
                unit.Id,
                oldValue: new { Status = fromStatus },
                newValue: new
                {
                    unit.UnitNumber,
                    Status = UnitStatus.Expired,
                    Path = path,
                    unit.ExpiresUtc
                },
                reason: "Unit expired.");
            expired++;

            // An expired unit can no longer be issued, so its reservation must not keep showing as blood held for the patient.
            var ended = await EndReservationsAsync(unit, now, ct);
            reservationsEnded += ended.Count;
            reservationsEndedFor.AddRange(ended);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return InventoryActionResult.OkSweep(expired, reservationsEnded, reservationsEndedFor);
    }

    /// <summary>Ends the unit's reservations; returns each as "{unit} for {MRN}".</summary>
    private async Task<IReadOnlyList<string>> EndReservationsAsync(BloodUnit unit, DateTime now, CancellationToken ct)
    {
        if (_allocations is null)
        {
            return [];
        }

        var endedFor = new List<string>();

        var reserved = await _allocations.ListAsync(a => a.BloodProductId == unit.Id && a.Status == AllocationStatus.Reserved, ct);
        foreach (var row in reserved)
        {
            var tracked = await _allocations.GetByIdAsync(row.Id, ct) ?? row;
            tracked.Status = AllocationStatus.Expired;
            tracked.ReleaseReason = $"Unit {unit.UnitNumber} expired at {unit.ExpiresUtc:yyyy-MM-dd HH:mm} UTC.";
            _allocations.Update(tracked);
            _audit.Record(
                AuditEventType.Assignment,
                nameof(Allocation),
                tracked.Id,
                oldValue: new { Status = AllocationStatus.Reserved },
                newValue: new { Status = AllocationStatus.Expired, tracked.PatientId, unit.UnitNumber },
                reason: tracked.ReleaseReason);
            var patient = _patients is null ? null : await _patients.GetByIdAsync(tracked.PatientId, ct);
            endedFor.Add($"{unit.UnitNumber} for {patient?.MedicalRecordNumber ?? $"patient {tracked.PatientId}"}");
        }

        return endedFor;
    }

    /// <summary>Available, or Received when the product requires a retype that is not yet confirmed.</summary>
    private static readonly UnitStatus[] ReservedStatuses =
        [UnitStatus.Selected, UnitStatus.Assigned, UnitStatus.Allocated, UnitStatus.Crossmatched];

    /// <summary>
    /// For a unit with an active reservation, the reserved status it had before it was quarantined (Assigned when the
    /// history does not say); null when it is not reserved. End-user testing pass 28: a reserved unit came back from
    /// quarantine as Available.
    /// </summary>
    private async Task<UnitStatus?> ReservedStatusBeforeQuarantineAsync(BloodUnit unit, CancellationToken ct)
    {
        if (_allocations is null
            || !await _allocations.AnyAsync(a => a.BloodProductId == unit.Id && a.Status == AllocationStatus.Reserved, ct))
        {
            return null;
        }

        var before = (await _repository.GetHistoryAsync(unit.Id, ct))
            .Where(h => h.ToStatus == UnitStatus.Quarantine && h.FromStatus != UnitStatus.Quarantine)
            .OrderByDescending(h => h.ChangedUtc).ThenByDescending(h => h.Id)
            .Select(h => (UnitStatus?)h.FromStatus)
            .FirstOrDefault();
        return before is UnitStatus status && ReservedStatuses.Contains(status) ? status : UnitStatus.Assigned;
    }

    private async Task<UnitStatus> StockStatusAsync(BloodUnit unit, CancellationToken ct)
    {
        var product = unit.ProductType ?? await _repository.GetProductTypeAsync(unit.ProductTypeId, ct);
        if (product?.RequiresRetype != true)
        {
            return UnitStatus.Available;
        }

        var confirmed = await ProductRetypeConfirmation.IsConfirmedAsync(_retypeResults, product, unit.Id, ct);
        return RetypeStockStatus.ForStock(requiresRetype: true, confirmed);
    }

    /// <summary>
    /// The status table lets a unit "move" to the status it already has, so an action repeated on a unit already in its
    /// target status would replace the reason recorded the first time. Those actions refuse instead.
    /// </summary>
    private static InventoryActionResult? RefuseRepeat(BloodUnit unit, UnitStatus target, string already) =>
        unit.Status == target
            ? InventoryActionResult.Fail($"{unit.UnitNumber} is already {already}; the reason recorded then stays.")
            : null;

    private async Task<InventoryActionResult> ChangeStatusAsync(BloodUnit unit, UnitStatus toStatus, string reason, CancellationToken ct)
    {
        // Cycle 703 (OCD-083): a unit reported transfused keeps its status.
        if (toStatus != unit.Status && ReportedTransfusionRule.Evaluate(unit.TransfusionReportedUtc, unit.UnitNumber) is { Severity: RuleSeverity.HardStop } reported)
        {
            return InventoryActionResult.Blocked(new RuleEvaluation(new[] { reported }));
        }

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

    private void RecordReceiveAudit(BloodUnit unit, string path) =>
        _audit.Record(
            AuditEventType.ProductStatus,
            nameof(BloodUnit),
            unit.Id,
            newValue: new
            {
                unit.UnitNumber,
                unit.Isbt128DonationId,
                unit.Status,
                unit.DonationRestriction,
                Path = path
            },
            reason: "Unit received.");

    private async Task<InventoryActionResult?> RejectUnauthorizedAsync(
        Func<bool, RuleResult> evaluate, CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(
            _currentUser.UserName, PermissionCodes.InventoryRelease, ct);
        var auth = evaluate(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? InventoryActionResult.Blocked(new RuleEvaluation([auth]))
            : null;
    }

    private Task<InventoryActionResult?> RejectUnauthorizedReceiveAsync(CancellationToken ct) =>
        RejectUnauthorizedReceiveAsync(InventoryAuthorizationRule.EvaluateReceive, ct);

    private async Task<InventoryActionResult?> RejectUnauthorizedReceiveAsync(
        Func<bool, RuleResult> evaluate, CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(
            _currentUser.UserName, PermissionCodes.InventoryReceive, ct);
        var auth = evaluate(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? InventoryActionResult.Blocked(new RuleEvaluation([auth]))
            : null;
    }

    private async Task<InventoryActionResult?> RejectUnauthorizedDiscardAsync(CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(
            _currentUser.UserName, PermissionCodes.InventoryDiscard, ct);
        var auth = InventoryAuthorizationRule.EvaluateDiscard(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? InventoryActionResult.Blocked(new RuleEvaluation([auth]))
            : null;
    }

    private async Task<InventoryActionResult?> RejectUnauthorizedExpireAsync(CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(
            _currentUser.UserName, PermissionCodes.InventoryDiscard, ct);
        var auth = InventoryAuthorizationRule.EvaluateExpire(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? InventoryActionResult.Blocked(new RuleEvaluation([auth]))
            : null;
    }

    private async Task<InventoryActionResult?> RejectUnauthorizedTransferAsync(CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(
            _currentUser.UserName, PermissionCodes.InventoryTransfer, ct);
        var auth = InventoryAuthorizationRule.EvaluateTransfer(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? InventoryActionResult.Blocked(new RuleEvaluation([auth]))
            : null;
    }

    private async Task<InventoryActionResult?> RejectUnauthorizedRecallAsync(CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(
            _currentUser.UserName, PermissionCodes.InventoryRecall, ct);
        var auth = InventoryAuthorizationRule.EvaluateRecall(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? InventoryActionResult.Blocked(new RuleEvaluation([auth]))
            : null;
    }

    private async Task<InventoryActionResult?> RejectUnauthorizedReturnToSupplierAsync(CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(
            _currentUser.UserName, PermissionCodes.InventoryReceive, ct);
        var auth = InventoryAuthorizationRule.EvaluateReturnToSupplier(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? InventoryActionResult.Blocked(new RuleEvaluation([auth]))
            : null;
    }

    private static InventoryActionResult? RejectIncompleteRetypeAssignment(ProductType? product, RhType labeledRh)
    {
        if (product?.RequiresRetype != true)
        {
            return null;
        }

        var gate = ProductRetypeAssignment.Evaluate(product, labeledRh);
        return gate is { Severity: RuleSeverity.HardStop }
            ? InventoryActionResult.Blocked(new RuleEvaluation([gate]))
            : null;
    }

    private async Task<InventoryActionResult?> AttachPendingRetypeAsync(
        BloodUnit unit,
        ProductType? product,
        CancellationToken ct)
    {
        if (product?.RequiresRetype != true || _retype is null)
        {
            return null;
        }

        var attached = await _retype.EnsurePendingOnIntakeAsync(unit, product, ct);
        if (attached.Succeeded)
        {
            return null;
        }

        return attached.Evaluation is not null
            ? InventoryActionResult.Blocked(attached.Evaluation)
            : InventoryActionResult.Fail(attached.Error ?? "The applicable retype test could not be added.");
    }
}
