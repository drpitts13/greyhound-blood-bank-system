using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.Inventory;
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
        AutoThawService? autoThaw = null)
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
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.PerformedUtc).First());

        var orderIds = allocations.Where(a => a.OrderId.HasValue).Select(a => a.OrderId!.Value).Distinct().ToList();
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
            var requiresXm = product?.RequiresCrossmatch ?? false;
            xmByUnit.TryGetValue(unit.Id, out var xm);
            var hasException = await HasCompatibilityExceptionAsync(patientId, unit, product, ct);

            var display = ProductAllocationDisplayStatusRule.Evaluate(
                requiresXm,
                xm?.Result,
                hasException);

            issues.TryGetValue(unit.Id, out var issue);
            rows.Add(new PatientAllocationRowDto(
                allocation.Id,
                unit.Id,
                unit.UnitNumber,
                product?.ProductCode ?? "—",
                product?.Name ?? "—",
                requiresXm,
                display,
                allocation.Status,
                xm?.Result,
                null,
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
                pendingAutoThaw.Contains(allocation.Id)));
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
        var results = new List<CompatibleUnitDto>();

        foreach (var unit in available.OrderBy(u => u.ExpiresUtc).ThenBy(u => u.UnitNumber))
        {
            if (!productTypes.TryGetValue(unit.ProductTypeId, out var product) || !unit.BloodType.IsKnown)
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
            // Exclude HardStops and antigen-neg Warnings (those need supervisor override).
            if (eval.Any(r => r.Severity == RuleSeverity.HardStop
                              || r.Code == BloodAttributeCompatibilityRule.AntigenNegCode))
            {
                continue;
            }

            results.Add(new CompatibleUnitDto(
                unit.Id,
                unit.UnitNumber,
                product.Id,
                product.ProductCode,
                product.Name,
                product.RequiresCrossmatch,
                unit.Abo,
                unit.RhD,
                unit.BloodType.ToString(),
                unit.ExpiresUtc));
        }

        return OperationResult<IReadOnlyList<CompatibleUnitDto>>.Ok(results);
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
        if (product.RequiresCrossmatch && _crossmatchAttachment is not null && productOrder is null)
        {
            return EvaluationResult<AllocatePatientUnitResultDto>.Fail(
                "An open product order is required before selecting a unit that requires crossmatch.");
        }
        else if (product.RequiresCrossmatch && _crossmatchAttachment is null)
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

            if (product.RequiresCrossmatch
                && _crossmatchAttachment is not null
                && !await _antibodyScreenCompat.HasCompletedTypeAndScreenAsync(specimen.Id, _clock.UtcNow, ct))
            {
                return EvaluationResult<AllocatePatientUnitResultDto>.Fail(
                    "A current specimen with a completed type and screen is required to allocate this product.");
            }
        }
        else if (product.RequiresCrossmatch && _crossmatchAttachment is not null)
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
                antigenNegOverrideAuthorized),
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

        if (product.RequiresCrossmatch && productOrder is not null)
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
        else if (product.RequiresCrossmatch && requestedXmTest is not null)
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

        if (antigenNegOverrideAuthorized
            && !string.IsNullOrWhiteSpace(request.OverrideReason)
            && !string.IsNullOrWhiteSpace(request.AuthorizedBy))
        {
            await _overrides.AddAsync(new Override
            {
                Action = OverrideAction.WarningOverride,
                ContextType = nameof(Allocation),
                ContextId = allocation.Id,
                RuleCode = BloodAttributeCompatibilityRule.AntigenNegCode,
                Reason = request.OverrideReason.Trim(),
                AuthorizedBy = request.AuthorizedBy.Trim(),
                OverriddenUtc = _clock.UtcNow
            }, ct);
            overrideApplied = true;
        }

        await _unitOfWork.SaveChangesAsync(ct);

        var hasException = await HasCompatibilityExceptionAsync(patientId, unit, product, ct);
        var latestXm = (await _crossmatches.ListAsync(
                x => x.PatientId == patientId && x.BloodProductId == unit.Id, ct))
            .OrderByDescending(x => x.PerformedUtc)
            .FirstOrDefault();

        var row = new PatientAllocationRowDto(
            allocation.Id,
            unit.Id,
            unit.UnitNumber,
            product.ProductCode,
            product.Name,
            product.RequiresCrossmatch,
            ProductAllocationDisplayStatusRule.Evaluate(product.RequiresCrossmatch, latestXm?.Result, hasException),
            allocation.Status,
            latestXm?.Result,
            xmTestCode,
            allocation.OrderId,
            allocation.EncounterId,
            allocation.SpecimenId,
            allocation.AllocatedUtc,
            allocation.AllocatedBy,
            allocation.ExpiresUtc);

        var autoThawEligible = _autoThaw is not null && await _autoThaw.IsEligibleAsync(unit, product, ct);
        return EvaluationResult<AllocatePatientUnitResultDto>.Ok(
            new AllocatePatientUnitResultDto(row, orderId, xmTestCode, overrideApplied, autoThawEligible),
            allocResult.Evaluation);
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
}
