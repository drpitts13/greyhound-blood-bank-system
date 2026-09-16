using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Config;

namespace BloodBankLIS.Application.PatientWorkspace;

/// <summary>
/// Attaches configured serologic XM/CXM lines to product orders after the current
/// antibody screen is complete, and auto-adds/results electronic XM when a
/// candidate patient has units selected.
/// </summary>
public sealed class CrossmatchAttachmentService
{
    private readonly CrossmatchSettingsReader _settings;
    private readonly ElectronicCrossmatchEligibilityService _eligibility;
    private readonly AntibodyScreenCompatLoader _antibodyScreen;
    private readonly CompatibilityService _compatibility;
    private readonly IInventoryRepository _inventory;
    private readonly IRepository<Order> _orders;
    private readonly IRepository<OrderLine> _orderLines;
    private readonly IRepository<OrderSpecimen> _orderSpecimens;
    private readonly IRepository<ProductType> _productTypes;
    private readonly IRepository<TestDefinition> _testDefinitions;
    private readonly IRepository<TestResult> _results;
    private readonly IRepository<Allocation> _allocations;
    private readonly IRepository<Crossmatch> _crossmatches;
    private readonly IRepository<Specimen> _specimens;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter? _audit;

    public CrossmatchAttachmentService(
        CrossmatchSettingsReader settings,
        ElectronicCrossmatchEligibilityService eligibility,
        AntibodyScreenCompatLoader antibodyScreen,
        CompatibilityService compatibility,
        IInventoryRepository inventory,
        IRepository<Order> orders,
        IRepository<OrderLine> orderLines,
        IRepository<OrderSpecimen> orderSpecimens,
        IRepository<ProductType> productTypes,
        IRepository<TestDefinition> testDefinitions,
        IRepository<TestResult> results,
        IRepository<Allocation> allocations,
        IRepository<Crossmatch> crossmatches,
        IRepository<Specimen> specimens,
        IClock clock,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IAuditWriter? audit = null)
    {
        _settings = settings;
        _eligibility = eligibility;
        _antibodyScreen = antibodyScreen;
        _compatibility = compatibility;
        _inventory = inventory;
        _orders = orders;
        _orderLines = orderLines;
        _orderSpecimens = orderSpecimens;
        _productTypes = productTypes;
        _testDefinitions = testDefinitions;
        _results = results;
        _allocations = allocations;
        _crossmatches = crossmatches;
        _specimens = specimens;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task AttachSerologicIfNeededAsync(long patientId, CancellationToken ct = default)
    {
        if (!await _antibodyScreen.HasCurrentVerifiedAntibodyScreenAsync(patientId, _clock.UtcNow, ct))
        {
            return;
        }

        var board = await _eligibility.AssessAsync(patientId, ct);
        if (board?.Eligible == true)
        {
            return;
        }

        var settings = await _settings.GetActiveAsync(ct);
        var requiresComplex = await _antibodyScreen.RequiresComplexCrossmatchAsync(patientId, ct);
        var testCode = CrossmatchAttachmentRule.ChooseSerologicTestCode(
            electronicXmEligible: false,
            requiresComplex,
            settings.NegativeAntibodyHistoryTestCode,
            settings.PositiveAntibodyHistoryTestCode);
        var test = await FindActiveTestAsync(testCode, ct);
        if (test is null || !TestDefinitionValidator.IsCrossmatchResultType(test.ResultValueType))
        {
            return;
        }

        var targets = await ListProductOrdersNeedingCrossmatchAsync(patientId, ct);
        foreach (var (order, lines) in targets)
        {
            await AddTestLineAsync(order, lines, test, ct);
        }

        if (targets.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    public async Task<EvaluationResult<string?>> ApplyElectronicForUnitAsync(
        long patientId,
        Allocation allocation,
        BloodUnit unit,
        Order productOrder,
        long? specimenId,
        CancellationToken ct = default)
    {
        if (!await CanAutoResultElectronicAsync(patientId, ct))
        {
            return EvaluationResult<string?>.Ok(null);
        }

        var settings = await _settings.GetActiveAsync(ct);
        var test = await FindActiveTestAsync(settings.ElectronicCrossmatchTestCode, ct);
        if (test is null || test.ResultValueType != ResultValueType.Crossmatch)
        {
            return EvaluationResult<string?>.Fail("Electronic crossmatch test is not configured.");
        }

        var existing = await _crossmatches.FirstOrDefaultAsync(
            x => x.PatientId == patientId
                 && x.BloodProductId == unit.Id
                 && x.Result == CrossmatchResult.Compatible,
            ct);
        if (existing is not null)
        {
            return EvaluationResult<string?>.Ok(test.Code);
        }

        var resolvedSpecimenId = await ResolveSpecimenIdAsync(productOrder.Id, specimenId, allocation.SpecimenId, ct);
        if (resolvedSpecimenId is null)
        {
            return EvaluationResult<string?>.Fail("A current type-and-screen specimen is required for electronic crossmatch.");
        }

        var lines = (await _orderLines.ListAsync(l => l.OrderId == productOrder.Id && l.IsActive, ct)).ToList();
        var line = await AddTestLineAsync(productOrder, lines, test, ct);

        var result = new TestResult
        {
            SpecimenId = resolvedSpecimenId.Value,
            PatientId = patientId,
            OrderId = productOrder.Id,
            TestCode = test.Code,
            Value = "Compatible",
            Interpretation = "Compatible",
            Status = ResultStatus.Verified,
            Source = ResultSource.Calculated,
            SourceReference = $"EXM:{unit.UnitNumber}",
            EnteredBy = _currentUser.UserName,
            EnteredUtc = _clock.UtcNow,
            VerifiedBy = _currentUser.UserName,
            VerifiedUtc = _clock.UtcNow
        };
        await _results.AddAsync(result, ct);

        line.ResultStatus = ResultStatus.Verified;

        var recorded = await _compatibility.RecordElectronicCrossmatchSystemAsync(
            new RecordCrossmatchRequest(
                unit.Id,
                patientId,
                resolvedSpecimenId.Value,
                CrossmatchMethod.Electronic,
                CrossmatchResult.Compatible,
                AntibodyScreenNegative: true,
                Comment: $"Electronic crossmatch for {unit.UnitNumber}."),
            productOrder.Id,
            productOrder.EncounterId,
            ct);

        if (!recorded.Succeeded)
        {
            return recorded.Evaluation is not null
                ? EvaluationResult<string?>.Blocked(recorded.Evaluation)
                : EvaluationResult<string?>.Fail(recorded.Error ?? "Electronic crossmatch could not be recorded.");
        }

        _audit?.Record(
            AuditEventType.Crossmatch,
            nameof(TestResult),
            result.Id,
            newValue: new { test.Code, unit.UnitNumber, Method = CrossmatchMethod.Electronic, Result = CrossmatchResult.Compatible },
            reason: "Electronic crossmatch automatically resulted.");

        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<string?>.Ok(test.Code);
    }

    public async Task ApplyElectronicForReservedUnitsAsync(long patientId, CancellationToken ct = default)
    {
        if (!await CanAutoResultElectronicAsync(patientId, ct))
        {
            return;
        }

        var reserved = await _allocations.ListAsync(
            a => a.PatientId == patientId && a.Status == AllocationStatus.Reserved, ct);
        foreach (var allocation in reserved)
        {
            var unit = await _inventory.GetUnitAsync(allocation.BloodProductId, ct);
            if (unit is null)
            {
                continue;
            }

            var product = await _productTypes.GetByIdAsync(unit.ProductTypeId, ct);
            if (product is null || !product.RequiresCrossmatch)
            {
                continue;
            }

            var already = await _crossmatches.AnyAsync(
                x => x.PatientId == patientId
                     && x.BloodProductId == unit.Id
                     && x.Result == CrossmatchResult.Compatible,
                ct);
            if (already)
            {
                continue;
            }

            var order = await ResolveProductOrderAsync(patientId, product.Id, allocation.OrderId, ct);
            if (order is null)
            {
                continue;
            }

            allocation.OrderId = order.Id;
            await ApplyElectronicForUnitAsync(patientId, allocation, unit, order, allocation.SpecimenId, ct);
        }
    }

    public async Task<Order?> ResolveProductOrderAsync(
        long patientId,
        long productTypeId,
        long? preferredOrderId,
        CancellationToken ct = default)
    {
        if (preferredOrderId is > 0)
        {
            var preferred = await _orders.FirstOrDefaultAsync(
                o => o.Id == preferredOrderId.Value && o.PatientId == patientId, ct);
            if (preferred is not null && IsOpen(preferred))
            {
                return preferred;
            }
        }

        var orders = (await _orders.ListAsync(
                o => o.PatientId == patientId
                     && o.Status != OrderStatus.Completed
                     && o.Status != OrderStatus.Cancelled
                     && o.Status != OrderStatus.Discontinued,
                ct))
            .OrderByDescending(o => o.OrderedUtc)
            .ToList();
        if (orders.Count == 0)
        {
            return null;
        }

        var orderIds = orders.Select(o => o.Id).ToList();
        var lines = await _orderLines.ListAsync(
            l => orderIds.Contains(l.OrderId)
                 && l.IsActive
                 && l.LineCategory == OrderCategory.Product
                 && l.ProductTypeId == productTypeId,
            ct);
        var matchIds = lines.Select(l => l.OrderId).ToHashSet();
        return orders.FirstOrDefault(o => matchIds.Contains(o.Id));
    }

    public async Task AfterAntibodyScreenVerifiedAsync(long patientId, CancellationToken ct = default)
    {
        var board = await _eligibility.AssessAsync(patientId, ct);
        if (board?.Eligible == true)
        {
            await ApplyElectronicForReservedUnitsAsync(patientId, ct);
            return;
        }

        await AttachSerologicIfNeededAsync(patientId, ct);
    }

    private async Task<bool> CanAutoResultElectronicAsync(long patientId, CancellationToken ct)
    {
        var board = await _eligibility.AssessAsync(patientId, ct);
        if (board?.Eligible != true)
        {
            return false;
        }

        return await _antibodyScreen.HasCurrentVerifiedAntibodyScreenAsync(patientId, _clock.UtcNow, ct);
    }

    private async Task<List<(Order Order, List<OrderLine> Lines)>> ListProductOrdersNeedingCrossmatchAsync(
        long patientId,
        CancellationToken ct)
    {
        var orders = (await _orders.ListAsync(
                o => o.PatientId == patientId
                     && o.Status != OrderStatus.Completed
                     && o.Status != OrderStatus.Cancelled
                     && o.Status != OrderStatus.Discontinued,
                ct))
            .ToList();
        if (orders.Count == 0)
        {
            return [];
        }

        var orderIds = orders.Select(o => o.Id).ToList();
        var allLines = await _orderLines.ListAsync(l => orderIds.Contains(l.OrderId) && l.IsActive, ct);
        var productTypes = (await _productTypes.ListAsync(ct)).ToDictionary(p => p.Id);
        var xmCodes = await CrossmatchTestCodesAsync(ct);

        var targets = new List<(Order, List<OrderLine>)>();
        foreach (var order in orders)
        {
            var lines = allLines.Where(l => l.OrderId == order.Id).ToList();
            var needsXm = lines.Any(l =>
                l.LineCategory == OrderCategory.Product
                && l.ProductTypeId is > 0
                && productTypes.TryGetValue(l.ProductTypeId.Value, out var pt)
                && pt.RequiresCrossmatch);
            if (!needsXm)
            {
                continue;
            }

            if (lines.Any(l =>
                    l.LineCategory == OrderCategory.Test
                    && l.TestCode is not null
                    && xmCodes.Contains(l.TestCode)))
            {
                continue;
            }

            targets.Add((order, lines));
        }

        return targets;
    }

    private async Task<OrderLine> AddTestLineAsync(
        Order order,
        List<OrderLine> existing,
        TestDefinition test,
        CancellationToken ct)
    {
        var already = existing.FirstOrDefault(l =>
            l.IsActive
            && l.LineCategory == OrderCategory.Test
            && string.Equals(l.TestCode, test.Code, StringComparison.OrdinalIgnoreCase));
        if (already is not null)
        {
            return already;
        }

        var line = new OrderLine
        {
            OrderId = order.Id,
            LineNumber = existing.Count == 0 ? 1 : existing.Max(l => l.LineNumber) + 1,
            LineCategory = OrderCategory.Test,
            LineName = test.Name,
            TestCode = test.Code,
            OrderType = OrderType.Crossmatch,
            ResultStatus = ResultStatus.Pending,
            IsActive = true
        };
        await _orderLines.AddAsync(line, ct);
        existing.Add(line);
        var trackedOrder = await _orders.GetByIdAsync(order.Id, ct) ?? order;
        OrderLineBuilder.ApplyHeaderFromLines(trackedOrder, existing);
        _audit?.Record(
            AuditEventType.OrderChange,
            nameof(OrderLine),
            null,
            newValue: new { order.Id, test.Code, test.Name },
            reason: "Crossmatch test attached to product order.");
        return line;
    }

    private async Task<HashSet<string>> CrossmatchTestCodesAsync(CancellationToken ct)
    {
        var tests = await _testDefinitions.ListAsync(
            t => t.IsActive
                 && (t.ResultValueType == ResultValueType.Crossmatch
                     || t.ResultValueType == ResultValueType.ComplexCrossmatch),
            ct);
        return tests.Select(t => t.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private Task<TestDefinition?> FindActiveTestAsync(string? code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Task.FromResult<TestDefinition?>(null);
        }

        var normalized = code.Trim().ToUpperInvariant();
        return _testDefinitions.FirstOrDefaultAsync(
            t => t.IsActive && !t.IsDraft && t.Code == normalized, ct);
    }

    private async Task<long?> ResolveSpecimenIdAsync(
        long orderId,
        long? requested,
        long? allocationSpecimenId,
        CancellationToken ct)
    {
        if (requested is > 0)
        {
            return requested;
        }

        if (allocationSpecimenId is > 0)
        {
            return allocationSpecimenId;
        }

        var links = await _orderSpecimens.ListAsync(l => l.OrderId == orderId, ct);
        var linked = links.OrderByDescending(l => l.IsPrimary).FirstOrDefault();
        if (linked is not null)
        {
            return linked.SpecimenId;
        }

        var screens = await _results.ListAsync(
            r => r.OrderId == orderId && r.Status == ResultStatus.Verified, ct);
        return screens.OrderByDescending(r => r.VerifiedUtc).FirstOrDefault()?.SpecimenId;
    }

    private static bool IsOpen(Order order) =>
        order.Status is not (OrderStatus.Completed or OrderStatus.Cancelled or OrderStatus.Discontinued);
}
