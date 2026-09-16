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
    private readonly BloodAttributeCompatLoader _bloodAttributeCompat;
    private readonly AntibodyScreenCompatLoader _antibodyScreenCompat;
    private readonly IPermissionEvaluator _permissions;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CrossmatchAttachmentService? _crossmatchAttachment;
    private readonly ElectronicCrossmatchEligibilityService? _exmEligibility;

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
        IRepository<OrderLine>? orderLines = null)
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
        _bloodAttributeCompat = bloodAttributeCompat;
        _antibodyScreenCompat = antibodyScreenCompat;
        _permissions = permissions;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _crossmatchAttachment = crossmatchAttachment;
        _exmEligibility = exmEligibility;
    }

    public async Task<IReadOnlyList<PatientAllocationRowDto>> ListActiveAsync(long patientId, CancellationToken ct = default)
    {
        var allocations = await _allocations.ListAsync(
            a => a.PatientId == patientId && a.Status == AllocationStatus.Reserved, ct);
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
                allocation.ExpiresUtc));
        }

        return rows;
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
        var results = new List<CompatibleUnitDto>();

        foreach (var unit in available.OrderBy(u => u.ExpiresUtc).ThenBy(u => u.UnitNumber))
        {
            if (!productTypes.TryGetValue(unit.ProductTypeId, out var product) || !unit.BloodType.IsKnown)
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
        if (product.RequiresCrossmatch && _crossmatchAttachment is not null)
        {
            productOrder = await _crossmatchAttachment.ResolveProductOrderAsync(patientId, product.Id, null, ct);
            if (productOrder is null)
            {
                return EvaluationResult<AllocatePatientUnitResultDto>.Fail(
                    "An open product order is required before selecting a unit that requires crossmatch.");
            }
        }
        else if (product.RequiresCrossmatch)
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

        if (request.SpecimenId is not null)
        {
            var specimen = await _specimens.GetByIdAsync(request.SpecimenId.Value, ct);
            if (specimen is null || specimen.PatientId != patientId)
            {
                return EvaluationResult<AllocatePatientUnitResultDto>.Fail(
                    "Specimen not found for this patient.");
            }
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
                request.SpecimenId,
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
                    patientId, allocation, unit, productOrder, request.SpecimenId, ct);
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
                request.SpecimenId));

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

        return EvaluationResult<AllocatePatientUnitResultDto>.Ok(
            new AllocatePatientUnitResultDto(row, orderId, xmTestCode, overrideApplied),
            allocResult.Evaluation);
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
