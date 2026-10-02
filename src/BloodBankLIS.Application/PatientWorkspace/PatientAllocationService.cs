using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Config;

namespace BloodBankLIS.Application.PatientWorkspace;

public sealed class PatientAllocationService
{
    private readonly CompatibilityService _compatibility;
    private readonly OrderService _orders;
    private readonly IInventoryRepository _inventory;
    private readonly IRepository<Allocation> _allocations;
    private readonly IRepository<Crossmatch> _crossmatches;
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<ProductType> _productTypes;
    private readonly IRepository<PatientBloodTypeHistory> _bloodTypes;
    private readonly IRepository<TestDefinition> _testDefinitions;
    private readonly IRepository<Encounter> _encounters;
    private readonly IRepository<OrderingLocation> _orderingLocations;
    private readonly IRepository<Specimen> _specimens;
    private readonly IRepository<ExceptionDefinition> _exceptionDefinitions;
    private readonly IRepository<Override> _overrides;
    private readonly IRepository<OrderLine>? _orderLines;
    private readonly IRepository<OrderAction>? _orderActions;
    private readonly IRepository<ActionDefinition>? _actionDefinitions;
    private readonly IRepository<Order>? _orderRepo;
    private readonly IRepository<OrderSpecimen>? _orderSpecimens;
    private readonly IRepository<Issue>? _issues;
    private readonly BloodAttributeCompatLoader _bloodAttributeCompat;
    private readonly AntibodyScreenCompatLoader _antibodyScreenCompat;
    private readonly IPermissionEvaluator _permissions;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CrossmatchAttachmentService? _crossmatchAttachment;
    private readonly ElectronicCrossmatchEligibilityService? _exmEligibility;
    private readonly IRepository<ProductRetypeResult>? _retypes;
    private readonly AutoThawService? _autoThaw;
    private readonly FacilityPolicyService? _policy;
    private readonly IRepository<SpecialTransfusionRequirement>? _specialRequirements;
    private readonly IRepository<SpecialRequirementDefinition>? _requirementDefinitions;
    private readonly IRepository<ProductAttributeAssignment>? _productAttributeAssignments;
    private readonly IRepository<ProductAttribute>? _productAttributes;
    private readonly IRepository<InventoryLocation>? _locations;

    public PatientAllocationService(
        CompatibilityService compatibility,
        OrderService orders,
        IInventoryRepository inventory,
        IRepository<Allocation> allocations,
        IRepository<Crossmatch> crossmatches,
        IRepository<Patient> patients,
        IRepository<ProductType> productTypes,
        IRepository<PatientBloodTypeHistory> bloodTypes,
        IRepository<TestDefinition> testDefinitions,
        IRepository<Encounter> encounters,
        IRepository<OrderingLocation> orderingLocations,
        IRepository<Specimen> specimens,
        IRepository<ExceptionDefinition> exceptionDefinitions,
        IRepository<Override> overrides,
        BloodAttributeCompatLoader bloodAttributeCompat,
        AntibodyScreenCompatLoader antibodyScreenCompat,
        IPermissionEvaluator permissions,
        IClock clock,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        CrossmatchAttachmentService? crossmatchAttachment = null,
        ElectronicCrossmatchEligibilityService? exmEligibility = null,
        IRepository<OrderLine>? orderLines = null,
        IRepository<OrderAction>? orderActions = null,
        IRepository<ActionDefinition>? actionDefinitions = null,
        IRepository<Order>? orderRepo = null,
        IRepository<OrderSpecimen>? orderSpecimens = null,
        IRepository<Issue>? issues = null,
        IRepository<ProductRetypeResult>? retypes = null,
        AutoThawService? autoThaw = null,
        FacilityPolicyService? policy = null,
        IRepository<SpecialTransfusionRequirement>? specialRequirements = null,
        IRepository<SpecialRequirementDefinition>? requirementDefinitions = null,
        IRepository<ProductAttributeAssignment>? productAttributeAssignments = null,
        IRepository<ProductAttribute>? productAttributes = null,
        IRepository<InventoryLocation>? locations = null)
    {
        _compatibility = compatibility;
        _orders = orders;
        _inventory = inventory;
        _allocations = allocations;
        _crossmatches = crossmatches;
        _patients = patients;
        _productTypes = productTypes;
        _bloodTypes = bloodTypes;
        _testDefinitions = testDefinitions;
        _encounters = encounters;
        _orderingLocations = orderingLocations;
        _specimens = specimens;
        _exceptionDefinitions = exceptionDefinitions;
        _overrides = overrides;
        _orderLines = orderLines;
        _orderActions = orderActions;
        _actionDefinitions = actionDefinitions;
        _orderRepo = orderRepo;
        _orderSpecimens = orderSpecimens;
        _issues = issues;
        _bloodAttributeCompat = bloodAttributeCompat;
        _antibodyScreenCompat = antibodyScreenCompat;
        _permissions = permissions;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _crossmatchAttachment = crossmatchAttachment;
        _exmEligibility = exmEligibility;
        _retypes = retypes;
        _autoThaw = autoThaw;
        _policy = policy;
        _specialRequirements = specialRequirements;
        _requirementDefinitions = requirementDefinitions;
        _productAttributeAssignments = productAttributeAssignments;
        _productAttributes = productAttributes;
        _locations = locations;
    }

    public async Task<IReadOnlyList<PatientAllocationRowDto>> ListActiveAsync(
        long patientId,
        bool includeIssuedTransfused = false,
        CancellationToken ct = default)
    {
        var allocations = await _allocations.ListAsync(
            a => a.PatientId == patientId
                 && (a.Status == AllocationStatus.Reserved
                     || (includeIssuedTransfused && a.Status == AllocationStatus.Consumed)), ct);
        if (allocations.Count == 0)
        {
            return Array.Empty<PatientAllocationRowDto>();
        }

        var unitIds = allocations.Select(a => a.BloodProductId).Distinct().ToList();
        var units = (await _inventory.SearchAsync(new InventorySearchCriteria(), ct))
            .Where(u => unitIds.Contains(u.Id))
            .ToDictionary(u => u.Id);
        // Prefer direct loads for allocated units not returned by broad search edge cases.
        foreach (var id in unitIds.Where(id => !units.ContainsKey(id)))
        {
            var unit = await _inventory.GetUnitAsync(id, ct);
            if (unit is not null)
            {
                units[id] = unit;
            }
        }

        var productTypes = (await _productTypes.ListAsync(ct)).ToDictionary(p => p.Id);
        var crossmatches = await _crossmatches.ListAsync(x => x.PatientId == patientId, ct);
        var xmByUnit = crossmatches
            .GroupBy(x => x.BloodProductId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var orderIds = allocations.Where(a => a.OrderId.HasValue).Select(a => a.OrderId!.Value).Distinct().ToList();
        var linkedOrderIds = _orderSpecimens is null || orderIds.Count == 0
            ? new HashSet<long>()
            : (await _orderSpecimens.ListAsync(os => orderIds.Contains(os.OrderId), ct))
                .Select(os => os.OrderId)
                .ToHashSet();
        var xmCodeByOrder = await CrossmatchTestCodeByOrderAsync(orderIds, ct);
        var pendingByOrder = await LoadPendingActionsAsync(orderIds, ct);
        var pendingAutoThaw = _autoThaw is null
            ? new HashSet<long>()
            : await _autoThaw.PendingAllocationIdsAsync(allocations.Select(a => a.Id).ToList(), ct);
        var issues = _issues is null || allocations.Count == 0
            ? new Dictionary<long, Issue>()
            : (await _issues.ListAsync(
                    i => i.PatientId == patientId && unitIds.Contains(i.BloodProductId), ct))
                .GroupBy(i => i.BloodProductId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.IssuedUtc).First());

        var rows = new List<PatientAllocationRowDto>();
        foreach (var allocation in allocations.OrderByDescending(a => a.AllocatedUtc))
        {
            if (!units.TryGetValue(allocation.BloodProductId, out var unit))
            {
                continue;
            }

            productTypes.TryGetValue(unit.ProductTypeId, out var product);
            var requiresXm = RequiresCrossmatch(product);
            var unitCrossmatches = xmByUnit.GetValueOrDefault(unit.Id) ?? [];
            var xm = unitCrossmatches
                .OrderByDescending(x => x.PerformedUtc)
                .ThenByDescending(x => x.Id)
                .FirstOrDefault();
            var hasException = await HasCompatibilityExceptionAsync(patientId, unit, product, ct);

            var display = ProductAllocationDisplayStatusRule.Evaluate(
                requiresXm,
                xm?.Result,
                hasException);

            issues.TryGetValue(unit.Id, out var issue);
            var thawCode = await AutoThawCodeAsync(allocation, unit, product, issue, ct);
            rows.Add(new PatientAllocationRowDto(
                allocation.Id,
                unit.Id,
                unit.UnitNumber,
                product?.ProductCode ?? "—",
                product?.Name ?? "—",
                requiresXm,
                display,
                allocation.Status,
                RecordedCrossmatchResult(xm?.Result),
                allocation.OrderId is long xmOrderId && xmCodeByOrder.TryGetValue(xmOrderId, out var xmCode) ? xmCode : null,
                allocation.OrderId,
                allocation.EncounterId,
                allocation.SpecimenId,
                allocation.AllocatedUtc,
                allocation.AllocatedBy,
                allocation.ExpiresUtc,
                allocation.OrderId is long oid ? pendingByOrder.GetValueOrDefault(oid) : null,
                unit.Status,
                issue?.Id,
                issue?.Status,
                pendingAutoThaw.Contains(allocation.Id),
                thawCode,
                allocation.OrderId is long linkedOrderId && linkedOrderIds.Contains(linkedOrderId),
                BloodUnitExpirationRule.IsExpired(unit.ExpiresUtc, _clock.UtcNow),
                unit.ExpiresUtc,
                unit.BloodType.ToString(),
                await StorageLocationNameAsync(unit.CurrentLocationId, ct),
                CrossmatchValidityRule.CurrentCompatibleExpiresUtc(
                    unitCrossmatches.Select(x => (x.Result, x.ExpiresUtc)),
                    _clock.UtcNow)));
        }

        return rows;
    }

    private async Task<Dictionary<long, IReadOnlyList<OrderActionDto>>> LoadPendingActionsAsync(
        IReadOnlyList<long> orderIds,
        CancellationToken ct)
    {
        if (_orderActions is null || orderIds.Count == 0)
        {
            return new Dictionary<long, IReadOnlyList<OrderActionDto>>();
        }

        var actions = await _orderActions.ListAsync(
            a => orderIds.Contains(a.OrderId) && a.Status == OrderActionStatus.Pending, ct);
        if (actions.Count == 0)
        {
            return new Dictionary<long, IReadOnlyList<OrderActionDto>>();
        }

        var defIds = actions.Select(a => a.ActionDefinitionId).Distinct().ToList();
        var defs = _actionDefinitions is null
            ? new Dictionary<long, ActionDefinition>()
            : (await _actionDefinitions.ListAsync(d => defIds.Contains(d.Id), ct)).ToDictionary(d => d.Id);

        return actions
            .GroupBy(a => a.OrderId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<OrderActionDto>)g
                    .OrderBy(a => a.Id)
                    .Select(a => OrderActionDto.From(a, defs.GetValueOrDefault(a.ActionDefinitionId)))
                    .ToList());
    }

    private async Task<HashSet<long>> ConfirmedRetypeUnitIdsAsync(IReadOnlyList<long> unitIds, CancellationToken ct)
    {
        if (_retypes is null || unitIds.Count == 0)
        {
            return [];
        }

        var confirmed = await _retypes.ListAsync(
            r => unitIds.Contains(r.BloodProductId)
                 && r.Status == ResultStatus.Verified
                 && r.MatchesLabel,
            ct);
        return confirmed.Select(r => r.BloodProductId).ToHashSet();
    }

    public async Task<OperationResult<IReadOnlyList<CompatibleUnitDto>>> ListCompatibleUnitsAsync(
        long patientId, CancellationToken ct = default)
    {
        if (await _patients.GetByIdAsync(patientId, ct) is null)
        {
            return OperationResult<IReadOnlyList<CompatibleUnitDto>>.Fail("Patient not found.");
        }

        var bloodType = await _bloodTypes.FirstOrDefaultAsync(h => h.PatientId == patientId && h.IsCurrent, ct);
        if (bloodType is null || !bloodType.BloodType.IsKnown)
        {
            return OperationResult<IReadOnlyList<CompatibleUnitDto>>.Fail(
                "Patient has no current ABO/Rh on file; compatible units cannot be listed.");
        }

        var available = await _inventory.SearchAsync(new InventorySearchCriteria(Status: UnitStatus.Available), ct);
        var productTypes = (await _productTypes.ListAsync(ct)).ToDictionary(p => p.Id);
        var retypedUnitIds = await ConfirmedRetypeUnitIdsAsync(available.Select(u => u.Id).ToList(), ct);
        var ranked = new List<RankedCompatibleUnit>();
        var nearExpiryWindow = await NearExpiryWindowAsync(ct);
        var specialRequirements = await UnitSelectionRequirementsAsync(patientId, ct);
        var attributesByProduct = await ProductAttributeCodesByProductTypeAsync(ct);
        var crossmatchExpires = await CurrentCompatibleCrossmatchExpirationsAsync(patientId, ct);
        var latestCrossmatchResults = await LatestRecordedCrossmatchResultsAsync(patientId, ct);
        var crossmatchedUnitIds = crossmatchExpires.Keys.ToHashSet();
        var incompatibleUnitIds = await IncompatibleCrossmatchUnitIdsAsync(patientId, ct);
        var locationNames = await StorageLocationNamesAsync(available.Select(u => u.CurrentLocationId), ct);

        foreach (var unit in available)
        {
            if (!productTypes.TryGetValue(unit.ProductTypeId, out var product) || !unit.BloodType.IsKnown)
            {
                continue;
            }

            if (BloodUnitExpirationRule.IsExpired(unit.ExpiresUtc, _clock.UtcNow))
            {
                continue;
            }

            if (ProductRetypeAllocationRule.Evaluate(product.RequiresRetype, retypedUnitIds.Contains(unit.Id)).Severity
                == RuleSeverity.HardStop)
            {
                continue;
            }

            var eval = new List<RuleResult>();
            eval.AddRange(AboCompatibilityRule.Evaluate(bloodType.BloodType, unit.BloodType, product.ComponentClass));
            var bloodAttrs = await _bloodAttributeCompat.LoadAsync(patientId, unit.Id, ct);
            eval.AddRange(BloodAttributeCompatibilityRule.Evaluate(
                product.ComponentClass,
                bloodAttrs.PatientSignificantAntibodies,
                bloodAttrs.PatientAntigens,
                bloodAttrs.UnitSignificantAntibodies,
                bloodAttrs.UnitAntigens));
            if (eval.Any(r => r.Severity == RuleSeverity.HardStop))
            {
                continue;
            }

            attributesByProduct.TryGetValue(unit.ProductTypeId, out var attributeCodes);
            if (SpecialTransfusionRequirementRule.FailsUnitSelection(
                    specialRequirements,
                    attributeCodes ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    bloodAttrs.UnitAntigens,
                    _clock.UtcNow))
            {
                continue;
            }

            var antigenWarnings = eval
                .Where(r => r.Code == BloodAttributeCompatibilityRule.AntigenNegCode
                            && r.Severity == RuleSeverity.Warning)
                .ToList();
            var antigenFlag = BloodAttributeCompatibilityRule.SummarizeAntigenNegFlag(antigenWarnings);
            if (!ProductSelectionOrder.TryPlace(
                    unit.DonationRestriction,
                    unit.ReservedPatientId,
                    patientId,
                    bloodType.BloodType,
                    unit.BloodType,
                    antigenFlag,
                    out var placement))
            {
                continue;
            }

            var crossmatchCompatible = crossmatchedUnitIds.Contains(unit.Id);
            var crossmatchIncompatible = !crossmatchCompatible && incompatibleUnitIds.Contains(unit.Id);
            ranked.Add(new RankedCompatibleUnit(
                new CompatibleUnitDto(
                    unit.Id,
                    unit.UnitNumber,
                    product.Id,
                    product.ProductCode,
                    product.Name,
                    RequiresCrossmatch(product),
                    unit.Abo,
                    unit.RhD,
                    unit.BloodType.ToString(),
                    unit.ExpiresUtc,
                    antigenFlag,
                    antigenWarnings.Count == 0
                        ? null
                        : string.Join(" ", antigenWarnings.Select(r => r.Message)),
                    unit.DonationRestriction,
                    placement.Cue,
                    BloodUnitExpirationRule.IsNearExpiry(unit.ExpiresUtc, _clock.UtcNow, nearExpiryWindow),
                    crossmatchCompatible,
                    crossmatchIncompatible,
                    unit.CurrentLocationId is long storedAt && locationNames.TryGetValue(storedAt, out var storedName)
                        ? storedName
                        : null,
                    crossmatchExpires.TryGetValue(unit.Id, out var xmExpires) ? xmExpires : null,
                    latestCrossmatchResults.TryGetValue(unit.Id, out var latestXm) ? latestXm : null),
                placement.Band,
                placement.AntigenBand,
                crossmatchCompatible ? 0 : crossmatchIncompatible ? 2 : 1,
                unit.ExpiresUtc,
                unit.UnitNumber));
        }

        var results = ranked
            .OrderBy(r => r.Band)
            .ThenBy(r => r.AntigenBand)
            .ThenBy(r => r.CrossmatchBand)
            .ThenBy(r => r.ExpiresUtc)
            .ThenBy(r => r.UnitNumber, StringComparer.Ordinal)
            .Select(r => r.Dto)
            .ToList();
        return OperationResult<IReadOnlyList<CompatibleUnitDto>>.Ok(results);
    }

    private async Task<string?> StorageLocationNameAsync(long? locationId, CancellationToken ct)
    {
        if (locationId is not long id)
        {
            return null;
        }

        var names = await StorageLocationNamesAsync([id], ct);
        return names.TryGetValue(id, out var name) ? name : null;
    }

    private async Task<Dictionary<long, string?>> StorageLocationNamesAsync(
        IEnumerable<long?> locationIds,
        CancellationToken ct)
    {
        var names = new Dictionary<long, string?>();
        if (_locations is null)
        {
            return names;
        }

        var ids = locationIds.Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return names;
        }

        foreach (var location in await _locations.ListAsync(l => ids.Contains(l.Id), ct))
        {
            names[location.Id] = ReadyToIssueFormCue.StorageLocationLabel(location.Name, location.Code);
        }

        return names;
    }

    private async Task<TimeSpan> NearExpiryWindowAsync(CancellationToken ct)
    {
        var hours = _policy is null ? 24 : await _policy.GetNearExpiryWarningHoursAsync(ct);
        if (hours < 1)
        {
            hours = 1;
        }

        return TimeSpan.FromHours(hours);
    }

    private async Task<Dictionary<long, CrossmatchResult>> LatestRecordedCrossmatchResultsAsync(
        long patientId, CancellationToken ct)
    {
        var rows = await _crossmatches.ListAsync(x => x.PatientId == patientId, ct);
        var results = new Dictionary<long, CrossmatchResult>();
        foreach (var group in rows.GroupBy(x => x.BloodProductId))
        {
            var latest = group
                .OrderByDescending(x => x.PerformedUtc)
                .ThenByDescending(x => x.Id)
                .First();
            if (latest.Result is CrossmatchResult.Compatible or CrossmatchResult.Incompatible)
            {
                results[group.Key] = latest.Result;
            }
        }

        return results;
    }

    private async Task<Dictionary<long, DateTime?>> CurrentCompatibleCrossmatchExpirationsAsync(
        long patientId, CancellationToken ct)
    {
        var rows = await _crossmatches.ListAsync(
            x => x.PatientId == patientId && x.Result == CrossmatchResult.Compatible, ct);
        var now = _clock.UtcNow;
        var expirations = new Dictionary<long, DateTime?>();
        foreach (var group in rows.GroupBy(x => x.BloodProductId))
        {
            var matches = group.Select(x => (x.Result, x.ExpiresUtc)).ToList();
            if (!matches.Any(x => CrossmatchValidityRule.IsCurrentCompatible(x.Result, x.ExpiresUtc, now)))
            {
                continue;
            }

            expirations[group.Key] = CrossmatchValidityRule.CurrentCompatibleExpiresUtc(matches, now);
        }

        return expirations;
    }

    private async Task<HashSet<long>> IncompatibleCrossmatchUnitIdsAsync(long patientId, CancellationToken ct)
    {
        var rows = await _crossmatches.ListAsync(
            x => x.PatientId == patientId && x.Result == CrossmatchResult.Incompatible, ct);
        return rows
            .Where(x => CrossmatchValidityRule.CountsAsIncompatible(x.Result))
            .Select(x => x.BloodProductId)
            .ToHashSet();
    }

    private async Task<IReadOnlyList<SpecialTransfusionRequirementRule.RequirementRef>> UnitSelectionRequirementsAsync(
        long patientId, CancellationToken ct)
    {
        if (_specialRequirements is null)
        {
            return [];
        }

        var rows = await _specialRequirements.ListAsync(r => r.PatientId == patientId && r.IsActive, ct);
        var definitions = _requirementDefinitions is null
            ? new Dictionary<long, SpecialRequirementDefinition>()
            : (await _requirementDefinitions.ListAsync(ct)).ToDictionary(d => d.Id);
        return rows.Select(row => ToRequirementRef(row, definitions)).ToList();
    }

    private async Task<Dictionary<long, HashSet<string>>> ProductAttributeCodesByProductTypeAsync(CancellationToken ct)
    {
        if (_productAttributeAssignments is null || _productAttributes is null)
        {
            return [];
        }

        var assignments = await _productAttributeAssignments.ListAsync(a => a.IsActive, ct);
        var attributes = (await _productAttributes.ListAsync(a => a.IsActive, ct))
            .ToDictionary(a => a.Id);
        var byProduct = new Dictionary<long, HashSet<string>>();
        foreach (var assignment in assignments)
        {
            if (!attributes.TryGetValue(assignment.ProductAttributeId, out var attribute))
            {
                continue;
            }

            if (!byProduct.TryGetValue(assignment.ProductTypeId, out var codes))
            {
                codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                byProduct[assignment.ProductTypeId] = codes;
            }

            codes.Add(attribute.Code);
        }

        return byProduct;
    }

    private static SpecialTransfusionRequirementRule.RequirementRef ToRequirementRef(
        SpecialTransfusionRequirement row,
        IReadOnlyDictionary<long, SpecialRequirementDefinition> definitions)
    {
        if (row.RequirementDefinitionId is long id && definitions.TryGetValue(id, out var definition))
        {
            return new SpecialTransfusionRequirementRule.RequirementRef(
                definition.Code,
                definition.EnforcementKind,
                definition.ProductAttributeCode,
                row.AntigenCode,
                row.EffectiveUtc,
                row.ExpiresUtc,
                row.IsActive);
        }

        return SpecialTransfusionRequirementRule.RequirementRef.FromLegacy(
            row.RequirementType, row.AntigenCode, row.EffectiveUtc, row.ExpiresUtc, row.IsActive);
    }

    public async Task<IReadOnlyList<CrossmatchTestOptionDto>> ListCrossmatchTestsAsync(CancellationToken ct = default)
    {
        var tests = await _testDefinitions.ListAsync(
            t => t.IsActive
                 && !t.IsDraft
                 && (t.ResultValueType == ResultValueType.Crossmatch
                     || t.ResultValueType == ResultValueType.ComplexCrossmatch),
            ct);

        return tests
            .OrderBy(t => t.ResultValueType)
            .ThenBy(t => t.Code)
            .Select(t => new CrossmatchTestOptionDto(t.Code, t.Name, t.ResultValueType))
            .ToList();
    }

    public async Task<EvaluationResult<AllocatePatientUnitResultDto>> AllocateAsync(
        long patientId,
        AllocatePatientUnitRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await _patients.GetByIdAsync(patientId, ct) is null)
        {
            return EvaluationResult<AllocatePatientUnitResultDto>.Fail("Patient not found.");
        }

        var unit = await _inventory.GetUnitAsync(request.BloodUnitId, ct);
        if (unit is null)
        {
            return EvaluationResult<AllocatePatientUnitResultDto>.Fail("Unit not found.");
        }

        var product = await _productTypes.GetByIdAsync(unit.ProductTypeId, ct);
        if (product is null)
        {
            return EvaluationResult<AllocatePatientUnitResultDto>.Fail("Product type not found.");
        }

        Order? productOrder = null;
        TestDefinition? requestedXmTest = null;
        var resolvedOrder = await ResolveExplicitOrUniqueProductOrderAsync(
            patientId, product.Id, request.OrderId, ct);
        if (!resolvedOrder.Succeeded)
        {
            return EvaluationResult<AllocatePatientUnitResultDto>.Fail(
                resolvedOrder.Error ?? "An open product order is required.");
        }

        productOrder = resolvedOrder.Value;
        if (RequiresCrossmatch(product) && _crossmatchAttachment is not null && productOrder is null)
        {
            return EvaluationResult<AllocatePatientUnitResultDto>.Fail(
                "An open product order is required before selecting a unit that requires crossmatch.");
        }
        else if (RequiresCrossmatch(product) && _crossmatchAttachment is null)
        {
            if (string.IsNullOrWhiteSpace(request.CrossmatchTestCode))
            {
                return EvaluationResult<AllocatePatientUnitResultDto>.Fail(
                    "A crossmatch or complex crossmatch test must be selected for this product.");
            }

            var xmCode = request.CrossmatchTestCode.Trim().ToUpperInvariant();
            requestedXmTest = await _testDefinitions.FirstOrDefaultAsync(
                t => t.IsActive && !t.IsDraft && t.Code == xmCode, ct);
            if (requestedXmTest is null || !TestDefinitionValidator.IsCrossmatchResultType(requestedXmTest.ResultValueType))
            {
                return EvaluationResult<AllocatePatientUnitResultDto>.Fail(
                    $"Test '{request.CrossmatchTestCode}' is not an active crossmatch or complex crossmatch test.");
            }

            var requiresComplexXm = await _antibodyScreenCompat.RequiresComplexCrossmatchAsync(patientId, ct);
            if (requiresComplexXm && requestedXmTest.ResultValueType == ResultValueType.Crossmatch)
            {
                var abRule = AntibodyHistoryCrossmatchRule.Evaluate(true, requestedXmTest.ResultValueType, overrideAuthorized: false);
                if (string.IsNullOrWhiteSpace(request.OverrideReason)
                    || string.IsNullOrWhiteSpace(request.AuthorizedBy))
                {
                    return EvaluationResult<AllocatePatientUnitResultDto>.Blocked(new RuleEvaluation([abRule]));
                }

                var definition = await _exceptionDefinitions.FirstOrDefaultAsync(
                    e => e.RuleCode == AntibodyHistoryCrossmatchRule.RuleCode && e.IsActive, ct);
                var userLevel = await _permissions.GetMaxSecurityLevelAsync(_currentUser.UserName, ct);
                var access = ExceptionOverridePolicy.EvaluateAccess(
                    userLevel, definition, AntibodyHistoryCrossmatchRule.RuleCode);
                if (access.Severity == RuleSeverity.HardStop)
                {
                    return EvaluationResult<AllocatePatientUnitResultDto>.Blocked(
                        new RuleEvaluation([access, abRule]));
                }
            }
        }

        long? encounterId = request.EncounterId;
        if (encounterId is null)
        {
            var active = (await _encounters.ListAsync(
                    e => e.PatientId == patientId && e.Status == EncounterStatus.Active, ct))
                .OrderByDescending(e => e.AdmitUtc)
                .FirstOrDefault();
            encounterId = active?.Id;
        }
        else
        {
            var enc = await _encounters.GetByIdAsync(encounterId.Value, ct);
            if (enc is null || enc.PatientId != patientId)
            {
                return EvaluationResult<AllocatePatientUnitResultDto>.Fail(
                    "Encounter not found for this patient.");
            }
        }

        long? specimenId = request.SpecimenId;
        if (specimenId is null && productOrder is not null && _orderSpecimens is not null)
        {
            var links = await _orderSpecimens.ListAsync(os => os.OrderId == productOrder.Id, ct);
            specimenId = links.OrderByDescending(l => l.IsPrimary).FirstOrDefault()?.SpecimenId;
        }

        if (specimenId is not null)
        {
            var specimen = await _specimens.GetByIdAsync(specimenId.Value, ct);
            if (specimen is null || specimen.PatientId != patientId)
            {
                return EvaluationResult<AllocatePatientUnitResultDto>.Fail(
                    "Specimen not found for this patient.");
            }

            if (RequiresCrossmatch(product)
                && _crossmatchAttachment is not null
                && !await _antibodyScreenCompat.HasCompletedTypeAndScreenAsync(specimen.Id, _clock.UtcNow, ct))
            {
                return EvaluationResult<AllocatePatientUnitResultDto>.Fail(
                    "A current specimen with a completed type and screen is required to allocate this product.");
            }
        }
        else if (RequiresCrossmatch(product) && _crossmatchAttachment is not null)
        {
            return EvaluationResult<AllocatePatientUnitResultDto>.Fail(
                "Link a current type-and-screen specimen to the product order before allocating this unit.");
        }

        var bloodAttrs = await _bloodAttributeCompat.LoadAsync(patientId, unit.Id, ct);
        var antigenResults = BloodAttributeCompatibilityRule.Evaluate(
            product.ComponentClass,
            bloodAttrs.PatientSignificantAntibodies,
            bloodAttrs.PatientAntigens,
            bloodAttrs.UnitSignificantAntibodies,
            bloodAttrs.UnitAntigens);
        var antigenNegWarnings = antigenResults
            .Where(r => r.Code == BloodAttributeCompatibilityRule.AntigenNegCode
                        && r.Severity == RuleSeverity.Warning)
            .ToList();
        var antigenNegOverrideAuthorized = false;
        if (antigenNegWarnings.Count > 0)
        {
            var antigenEval = new RuleEvaluation(antigenNegWarnings);
            if (string.IsNullOrWhiteSpace(request.OverrideReason)
                || string.IsNullOrWhiteSpace(request.AuthorizedBy))
            {
                return EvaluationResult<AllocatePatientUnitResultDto>.Blocked(antigenEval);
            }

            var antigenDef = await _exceptionDefinitions.FirstOrDefaultAsync(
                e => e.RuleCode == BloodAttributeCompatibilityRule.AntigenNegCode && e.IsActive, ct);
            var userLevel = await _permissions.GetMaxSecurityLevelAsync(_currentUser.UserName, ct);
            var access = ExceptionOverridePolicy.EvaluateAccess(
                userLevel, antigenDef, BloodAttributeCompatibilityRule.AntigenNegCode);
            if (access.Severity == RuleSeverity.HardStop)
            {
                return EvaluationResult<AllocatePatientUnitResultDto>.Blocked(
                    new RuleEvaluation([access, .. antigenNegWarnings]));
            }

            antigenNegOverrideAuthorized = true;
        }

        var allocResult = await _compatibility.AllocateUnitAsync(
            new AllocateUnitRequest(
                request.BloodUnitId,
                patientId,
                specimenId,
                request.ExpiresUtc,
                antigenNegOverrideAuthorized ? request.OverrideReason : null,
                antigenNegOverrideAuthorized ? request.AuthorizedBy : null),
            ct);

        if (!allocResult.Succeeded || allocResult.Value is null)
        {
            return allocResult.Evaluation is not null
                ? EvaluationResult<AllocatePatientUnitResultDto>.Blocked(allocResult.Evaluation)
                : EvaluationResult<AllocatePatientUnitResultDto>.Fail(allocResult.Error ?? "Allocation failed.");
        }

        var allocation = allocResult.Value;
        allocation.EncounterId = encounterId;

        long? orderId = productOrder?.Id;
        string? xmTestCode = null;
        var overrideApplied = false;

        if (RequiresCrossmatch(product) && productOrder is not null)
        {
            allocation.OrderId = productOrder.Id;
            orderId = productOrder.Id;

            var exmEligible = _exmEligibility is not null
                && (await _exmEligibility.AssessAsync(patientId, ct))?.Eligible == true;
            if (exmEligible && _crossmatchAttachment is not null)
            {
                var exm = await _crossmatchAttachment.ApplyElectronicForUnitAsync(
                    patientId, allocation, unit, productOrder, specimenId, ct);
                if (!exm.Succeeded)
                {
                    return exm.Evaluation is not null
                        ? EvaluationResult<AllocatePatientUnitResultDto>.Blocked(exm.Evaluation)
                        : EvaluationResult<AllocatePatientUnitResultDto>.Fail(
                            exm.Error ?? "Electronic crossmatch could not be recorded.");
                }

                xmTestCode = exm.Value;
            }
            else if (_crossmatchAttachment is not null)
            {
                await _crossmatchAttachment.AttachSerologicIfNeededAsync(patientId, ct);
                if (_orderLines is not null)
                {
                    var lines = await _orderLines.ListAsync(l => l.OrderId == productOrder.Id && l.IsActive, ct);
                    xmTestCode = lines
                        .Where(l => l.LineCategory == OrderCategory.Test && OrderLineBuilder.IsCrossmatchTestCode(l.TestCode))
                        .Select(l => l.TestCode)
                        .FirstOrDefault();
                }
            }
        }
        else if (RequiresCrossmatch(product) && requestedXmTest is not null)
        {
            if (encounterId is null)
            {
                return EvaluationResult<AllocatePatientUnitResultDto>.Fail(
                    "An active visit is required to order a crossmatch when allocating this product.");
            }

            var locationId = request.OrderingLocationId
                ?? (await _orderingLocations.ListAsync(l => l.IsActive, ct))
                    .OrderBy(l => l.Code)
                    .FirstOrDefault()?.Id;
            if (locationId is null)
            {
                return EvaluationResult<AllocatePatientUnitResultDto>.Fail(
                    "No active ordering location is configured; cannot order the crossmatch.");
            }

            var orderNumber = $"XM-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
            var orderResult = await _orders.CreateForAllocationAsync(patientId, new CreateOrderRequest(
                encounterId.Value,
                locationId.Value,
                orderNumber,
                [new OrderLineInputDto(OrderCategory.Test, requestedXmTest.Code, null)],
                OrderPriority.Stat,
                _clock.UtcNow,
                null,
                OrderSource.Manual,
                null,
                _currentUser.UserName,
                specimenId));

            if (!orderResult.Succeeded || orderResult.Value is null)
            {
                await _compatibility.ReleaseAllocationAsync(
                    allocation.Id,
                    "Released: crossmatch order could not be created after allocation.",
                    ct);
                return EvaluationResult<AllocatePatientUnitResultDto>.Fail(
                    orderResult.Error ?? "Allocation succeeded but crossmatch order could not be created.");
            }

            orderId = orderResult.Value.Id;
            allocation.OrderId = orderId;
            xmTestCode = requestedXmTest.Code;

            if (await _antibodyScreenCompat.RequiresComplexCrossmatchAsync(patientId, ct)
                && requestedXmTest.ResultValueType == ResultValueType.Crossmatch
                && !string.IsNullOrWhiteSpace(request.OverrideReason)
                && !string.IsNullOrWhiteSpace(request.AuthorizedBy))
            {
                await _overrides.AddAsync(new Override
                {
                    Action = OverrideAction.WarningOverride,
                    ContextType = nameof(Allocation),
                    ContextId = allocation.Id,
                    RuleCode = AntibodyHistoryCrossmatchRule.RuleCode,
                    Reason = request.OverrideReason.Trim(),
                    AuthorizedBy = request.AuthorizedBy.Trim(),
                    OverriddenUtc = _clock.UtcNow
                }, ct);
                overrideApplied = true;
            }
        }

        if (productOrder is not null && allocation.OrderId is null)
        {
            allocation.OrderId = productOrder.Id;
        }

        if (antigenNegOverrideAuthorized)
        {
            overrideApplied = true;
        }

        await _unitOfWork.SaveChangesAsync(ct);

        var hasException = await HasCompatibilityExceptionAsync(patientId, unit, product, ct);
        var unitCrossmatches = await _crossmatches.ListAsync(
            x => x.PatientId == patientId && x.BloodProductId == unit.Id, ct);
        var latestXm = unitCrossmatches
            .OrderByDescending(x => x.PerformedUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefault();

        var row = new PatientAllocationRowDto(
            allocation.Id,
            unit.Id,
            unit.UnitNumber,
            product.ProductCode,
            product.Name,
            RequiresCrossmatch(product),
            ProductAllocationDisplayStatusRule.Evaluate(RequiresCrossmatch(product), latestXm?.Result, hasException),
            allocation.Status,
            RecordedCrossmatchResult(latestXm?.Result),
            xmTestCode,
            allocation.OrderId,
            allocation.EncounterId,
            allocation.SpecimenId,
            allocation.AllocatedUtc,
            allocation.AllocatedBy,
            allocation.ExpiresUtc);

        var thawCode = await AutoThawCodeAsync(allocation, unit, product, issue: null, ct);
        var orderLinked = allocation.OrderId is long linkedOrderId
            && _orderSpecimens is not null
            && await _orderSpecimens.AnyAsync(os => os.OrderId == linkedOrderId, ct);
        row = row with
        {
            AutoThawProductCode = thawCode,
            OrderSpecimenLinked = orderLinked,
            UnitOutdated = BloodUnitExpirationRule.IsExpired(unit.ExpiresUtc, _clock.UtcNow),
            UnitExpiresUtc = unit.ExpiresUtc,
            UnitBloodType = unit.BloodType.ToString(),
            StorageLocationName = await StorageLocationNameAsync(unit.CurrentLocationId, ct),
            CrossmatchExpiresUtc = CrossmatchValidityRule.CurrentCompatibleExpiresUtc(
                unitCrossmatches.Select(x => (x.Result, x.ExpiresUtc)),
                _clock.UtcNow)
        };

        return EvaluationResult<AllocatePatientUnitResultDto>.Ok(
            new AllocatePatientUnitResultDto(row, orderId, xmTestCode, overrideApplied, thawCode is not null),
            allocResult.Evaluation);
    }

    private async Task<string?> AutoThawCodeAsync(
        Allocation allocation,
        BloodUnit unit,
        ProductType? product,
        Issue? issue,
        CancellationToken ct)
    {
        if (_autoThaw is null
            || allocation.Status != AllocationStatus.Reserved
            || issue is not null
            || unit.Status is UnitStatus.Issued or UnitStatus.Transfused or UnitStatus.TransfusionStarted
                or UnitStatus.TransfusionStopped or UnitStatus.Discarded or UnitStatus.Modified)
        {
            return null;
        }

        if (await _autoThaw.HasCompletedAsync(unit.Id, ct))
        {
            return null;
        }

        return await _autoThaw.MatchCodeAsync(unit, product, ct);
    }

    public async Task<IReadOnlyList<AllocatableOrderDto>> ListAllocatableOrdersAsync(
        long productTypeId,
        long? patientId = null,
        CancellationToken ct = default)
    {
        if (_orderRepo is null || _orderLines is null)
        {
            return [];
        }

        var orders = (await _orderRepo.ListAsync(
                o => o.Status != OrderStatus.Completed
                     && o.Status != OrderStatus.Cancelled
                     && o.Status != OrderStatus.Discontinued
                     && (patientId == null || o.PatientId == patientId.Value),
                ct))
            .OrderByDescending(o => o.OrderedUtc)
            .ToList();
        if (orders.Count == 0)
        {
            return [];
        }

        var orderIds = orders.Select(o => o.Id).ToList();
        var lines = await _orderLines.ListAsync(
            l => orderIds.Contains(l.OrderId)
                 && l.IsActive
                 && l.LineCategory == OrderCategory.Product
                 && l.ProductTypeId == productTypeId,
            ct);
        var matchIds = lines.Select(l => l.OrderId).ToHashSet();
        orders = orders.Where(o => matchIds.Contains(o.Id)).ToList();
        if (orders.Count == 0)
        {
            return [];
        }

        var patientIds = orders.Select(o => o.PatientId).Distinct().ToList();
        var patients = (await _patients.ListAsync(p => patientIds.Contains(p.Id), ct)).ToDictionary(p => p.Id);
        var links = _orderSpecimens is null
            ? Array.Empty<OrderSpecimen>()
            : await _orderSpecimens.ListAsync(os => matchIds.Contains(os.OrderId), ct);
        var specimenByOrder = links
            .GroupBy(l => l.OrderId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.IsPrimary).First().SpecimenId);
        var specimenIds = specimenByOrder.Values.Distinct().ToList();
        var specimens = specimenIds.Count == 0
            ? new Dictionary<long, Specimen>()
            : (await _specimens.ListAsync(s => specimenIds.Contains(s.Id), ct)).ToDictionary(s => s.Id);

        return orders.Select(o =>
        {
            patients.TryGetValue(o.PatientId, out var patient);
            long? specimenId = specimenByOrder.TryGetValue(o.Id, out var sid) ? sid : null;
            var accession = specimenId is long id && specimens.TryGetValue(id, out var spec)
                ? spec.AccessionNumber
                : null;
            return new AllocatableOrderDto(
                o.Id,
                o.PatientId,
                patient?.MedicalRecordNumber ?? string.Empty,
                patient is null ? string.Empty : $"{patient.LastName}, {patient.FirstName}",
                o.OrderNumber,
                o.OrderName,
                productTypeId,
                specimenId,
                accession,
                o.OrderedUtc);
        }).ToList();
    }

    public async Task<IReadOnlyList<AllocatableOrderDto>> ListAllocatableOrdersForUnitAsync(
        long unitId,
        CancellationToken ct = default)
    {
        var unit = await _inventory.GetUnitAsync(unitId, ct);
        if (unit is null)
        {
            return [];
        }

        return await ListAllocatableOrdersAsync(unit.ProductTypeId, patientId: null, ct);
    }

    private async Task<OperationResult<Order?>> ResolveExplicitOrUniqueProductOrderAsync(
        long patientId,
        long productTypeId,
        long? requestedOrderId,
        CancellationToken ct)
    {
        if (requestedOrderId is > 0)
        {
            if (_crossmatchAttachment is not null)
            {
                var preferred = await _crossmatchAttachment.ResolveProductOrderAsync(
                    patientId, productTypeId, requestedOrderId, ct);
                if (preferred is null)
                {
                    return OperationResult<Order?>.Fail(
                        "The selected order is not an open order for this product.");
                }

                return OperationResult<Order?>.Ok(preferred);
            }

            if (_orderRepo is not null)
            {
                var order = await _orderRepo.FirstOrDefaultAsync(
                    o => o.Id == requestedOrderId.Value && o.PatientId == patientId, ct);
                if (order is null
                    || order.Status is OrderStatus.Completed or OrderStatus.Cancelled or OrderStatus.Discontinued)
                {
                    return OperationResult<Order?>.Fail(
                        "The selected order is not an open order for this product.");
                }

                return OperationResult<Order?>.Ok(order);
            }
        }

        var matches = await ListAllocatableOrdersAsync(productTypeId, patientId, ct);
        if (matches.Count > 1)
        {
            return OperationResult<Order?>.Fail(
                "Select the open product order to allocate this unit to.");
        }

        if (matches.Count == 1)
        {
            if (_crossmatchAttachment is not null)
            {
                var unique = await _crossmatchAttachment.ResolveProductOrderAsync(
                    patientId, productTypeId, matches[0].OrderId, ct);
                return OperationResult<Order?>.Ok(unique);
            }

            if (_orderRepo is not null)
            {
                var unique = await _orderRepo.GetByIdAsync(matches[0].OrderId, ct);
                return OperationResult<Order?>.Ok(unique);
            }
        }

        if (_crossmatchAttachment is null)
        {
            return OperationResult<Order?>.Ok(null);
        }

        return OperationResult<Order?>.Fail(
            "An open product order is required to allocate this unit.");
    }

    private async Task<bool> HasCompatibilityExceptionAsync(
        long patientId, BloodUnit unit, ProductType? product, CancellationToken ct)
    {
        if (product is null)
        {
            return false;
        }

        var bloodType = await _bloodTypes.FirstOrDefaultAsync(h => h.PatientId == patientId && h.IsCurrent, ct);
        var results = new List<RuleResult>();
        if (bloodType is not null && unit.BloodType.IsKnown)
        {
            results.AddRange(AboCompatibilityRule.Evaluate(bloodType.BloodType, unit.BloodType, product.ComponentClass));
        }

        var bloodAttrs = await _bloodAttributeCompat.LoadAsync(patientId, unit.Id, ct);
        results.AddRange(BloodAttributeCompatibilityRule.Evaluate(
            product.ComponentClass,
            bloodAttrs.PatientSignificantAntibodies,
            bloodAttrs.PatientAntigens,
            bloodAttrs.UnitSignificantAntibodies,
            bloodAttrs.UnitAntigens));

        return results.Any(r => r.Severity is RuleSeverity.HardStop or RuleSeverity.Warning);
    }

    private static CrossmatchResult? RecordedCrossmatchResult(CrossmatchResult? result) =>
        result is CrossmatchResult.Compatible or CrossmatchResult.Incompatible ? result : null;

    private static bool RequiresCrossmatch(ProductType? product) =>
        product is not null
        && CrossmatchApplicability.RequiresCrossmatch(product.ComponentClass, product.RequiresCrossmatch);

    private async Task<Dictionary<long, string>> CrossmatchTestCodeByOrderAsync(
        IReadOnlyList<long> orderIds,
        CancellationToken ct)
    {
        var codes = new Dictionary<long, string>();
        if (_orderLines is null || orderIds.Count == 0)
        {
            return codes;
        }

        var lines = await _orderLines.ListAsync(
            l => orderIds.Contains(l.OrderId) && l.IsActive && l.LineCategory == OrderCategory.Test, ct);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line.TestCode) || !OrderLineBuilder.IsCrossmatchTestCode(line.TestCode))
            {
                continue;
            }

            if (!codes.TryGetValue(line.OrderId, out var existing) || CrossmatchEntryNeed.IsElectronic(existing))
            {
                codes[line.OrderId] = line.TestCode;
            }
        }

        return codes;
    }

    private readonly record struct RankedCompatibleUnit(
        CompatibleUnitDto Dto,
        int Band,
        int AntigenBand,
        int CrossmatchBand,
        DateTime ExpiresUtc,
        string UnitNumber);
}
