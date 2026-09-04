using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Application.Rules;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.PatientWorkspace;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.PatientWorkspace;

public sealed class OrderService
{
    private readonly IRepository<Order> _orders;
    private readonly IRepository<OrderLine> _orderLines;
    private readonly IRepository<OrderSpecimen> _orderSpecimens;
    private readonly IRepository<Encounter> _encounters;
    private readonly IRepository<OrderingLocation> _locations;
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<Specimen> _specimens;
    private readonly IRepository<OrderingProvider> _providers;
    private readonly IRepository<ProductType> _productTypes;
    private readonly IRepository<TestDefinition> _testDefinitions;
    private readonly IRepository<TestGrouper> _testGroupers;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly RuleEngineService? _ruleEngine;
    private readonly IPermissionEvaluator? _permissions;
    private readonly ICurrentUser? _currentUser;

    public OrderService(
        IRepository<Order> orders,
        IRepository<OrderLine> orderLines,
        IRepository<OrderSpecimen> orderSpecimens,
        IRepository<Encounter> encounters,
        IRepository<OrderingLocation> locations,
        IRepository<Patient> patients,
        IRepository<Specimen> specimens,
        IRepository<OrderingProvider> providers,
        IRepository<ProductType> productTypes,
        IRepository<TestDefinition> testDefinitions,
        IRepository<TestGrouper> testGroupers,
        IClock clock,
        IUnitOfWork unitOfWork,
        RuleEngineService? ruleEngine = null,
        IPermissionEvaluator? permissions = null,
        ICurrentUser? currentUser = null)
    {
        _ruleEngine = ruleEngine;
        _permissions = permissions;
        _currentUser = currentUser;
        _orders = orders;
        _orderLines = orderLines;
        _orderSpecimens = orderSpecimens;
        _encounters = encounters;
        _locations = locations;
        _patients = patients;
        _specimens = specimens;
        _providers = providers;
        _productTypes = productTypes;
        _testDefinitions = testDefinitions;
        _testGroupers = testGroupers;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<PatientOrderDto>> ListByPatientAsync(
        long patientId,
        long? encounterId = null,
        OrderCategory? category = null,
        bool? activeOnly = null,
        string? search = null,
        CancellationToken ct = default)
    {
        var orders = await _orders.ListAsync(o => o.PatientId == patientId, ct);
        var encounters = (await _encounters.ListAsync(e => e.PatientId == patientId, ct)).ToDictionary(e => e.Id);
        var locations = (await _locations.ListAsync(ct)).ToDictionary(l => l.Id);
        var orderIds = orders.Select(o => o.Id).ToList();
        var lines = orderIds.Count == 0
            ? Array.Empty<OrderLine>()
            : await _orderLines.ListAsync(l => orderIds.Contains(l.OrderId) && l.IsActive, ct);
        var linesByOrder = lines
            .GroupBy(l => l.OrderId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<OrderLine>)g.OrderBy(x => x.LineNumber).ToList());
        var links = orderIds.Count == 0
            ? Array.Empty<OrderSpecimen>()
            : await _orderSpecimens.ListAsync(os => orderIds.Contains(os.OrderId), ct);
        var specimenIds = links.Select(l => l.SpecimenId).Distinct().ToList();
        var specimens = specimenIds.Count == 0
            ? new Dictionary<long, Specimen>()
            : (await _specimens.ListAsync(s => specimenIds.Contains(s.Id), ct)).ToDictionary(s => s.Id);

        var primarySpecimenByOrder = links
            .GroupBy(l => l.OrderId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.IsPrimary).First().SpecimenId);

        IEnumerable<Order> query = orders;

        if (encounterId.HasValue)
        {
            query = query.Where(o => o.EncounterId == encounterId.Value);
        }

        if (category == OrderCategory.Mixed)
        {
            query = query.Where(o => o.OrderCategory == OrderCategory.Mixed);
        }
        else if (category is OrderCategory.Test or OrderCategory.Product)
        {
            query = query.Where(o =>
            {
                var orderLines = linesByOrder.GetValueOrDefault(o.Id) ?? Array.Empty<OrderLine>();
                return category.Value switch
                {
                    OrderCategory.Test => orderLines.Any(l => l.LineCategory == OrderCategory.Test),
                    OrderCategory.Product => orderLines.Any(l => l.LineCategory == OrderCategory.Product),
                    _ => true
                };
            });
        }

        if (activeOnly == true)
        {
            query = query.Where(o => o.Status is not (OrderStatus.Completed or OrderStatus.Cancelled or OrderStatus.Discontinued));
        }
        else if (activeOnly == false)
        {
            query = query.Where(o => o.Status is OrderStatus.Completed or OrderStatus.Cancelled or OrderStatus.Discontinued);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            query = query.Where(o =>
                o.OrderName.Contains(q, StringComparison.OrdinalIgnoreCase)
                || o.OrderNumber.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (o.OrderingProvider?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (encounters.GetValueOrDefault(o.EncounterId)?.VisitNumber.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (linesByOrder.GetValueOrDefault(o.Id)?.Any(l => l.LineName.Contains(q, StringComparison.OrdinalIgnoreCase)) ?? false)
                || (primarySpecimenByOrder.TryGetValue(o.Id, out var sid)
                    && specimens.GetValueOrDefault(sid)?.AccessionNumber.Contains(q, StringComparison.OrdinalIgnoreCase) == true));
        }

        return query
            .OrderByDescending(o => o.OrderedUtc)
            .ThenByDescending(o => o.Priority)
            .Select(o =>
            {
                encounters.TryGetValue(o.EncounterId, out var enc);
                locations.TryGetValue(o.OrderingLocationId, out var loc);
                long? primarySpecimenId = primarySpecimenByOrder.GetValueOrDefault(o.Id);
                string? accession = null;
                if (primarySpecimenId.HasValue && specimens.TryGetValue(primarySpecimenId.Value, out var sp))
                {
                    accession = sp.AccessionNumber;
                }

                var orderLines = linesByOrder.GetValueOrDefault(o.Id) ?? Array.Empty<OrderLine>();

                return new PatientOrderDto(
                    o.Id, o.PatientId, o.EncounterId, enc?.VisitNumber ?? "—",
                    o.OrderNumber, o.OrderCategory, o.OrderName, o.Priority, o.Status,
                    o.OrderingLocationId, loc?.Name ?? "—", o.OrderingProviderId, o.OrderingProvider,
                    primarySpecimenId, accession, o.ResultStatus, o.FulfillmentStatus,
                    o.Source, o.OrderedUtc, o.CancellationReason,
                    orderLines.Select(OrderLineDto.From).ToList());
            })
            .ToList();
    }

    public async Task<OperationResult<Order>> CreateAsync(long patientId, CreateOrderRequest request, CancellationToken ct = default)
    {
        if (request.Lines.Count == 0)
        {
            return OperationResult<Order>.Fail("At least one test or product is required.");
        }

        var patient = await _patients.GetByIdAsync(patientId, ct);
        var patientExists = patient is not null;
        if (patient is not null)
        {
            var clinical = PatientMergeRule.EvaluateClinicalUse(patient.Status);
            if (clinical.Severity == RuleSeverity.HardStop)
            {
                return OperationResult<Order>.Fail(clinical.Message);
            }
        }

        var encounter = await _encounters.GetByIdAsync(request.EncounterId, ct);
        var encounterExists = encounter is not null;
        var encounterBelongs = encounter?.PatientId == patientId;
        var location = await _locations.GetByIdAsync(request.OrderingLocationId, ct);

        if (await _orders.AnyAsync(o => o.OrderNumber == request.OrderNumber, ct))
        {
            return OperationResult<Order>.Fail($"Order number '{request.OrderNumber}' already exists.");
        }

        var (orderingProviderId, orderingProviderName) = await ResolveOrderingProviderAsync(request.OrderingProviderId, ct);
        var productTypeMap = await LoadProductTypeMapAsync(ct);
        var lineInputs = OrderLineBuilder.WithCrossmatchLineIfNeeded(request.Lines, productTypeMap);
        lineInputs = await ExpandGrouperLinesAsync(lineInputs, ct);
        var builtLines = await BuildLinesAsync(lineInputs, productTypeMap, ct);

        var order = new Order
        {
            PatientId = patientId,
            EncounterId = request.EncounterId,
            OrderingLocationId = request.OrderingLocationId,
            OrderNumber = request.OrderNumber.Trim(),
            Priority = request.Priority,
            Status = OrderStatus.New,
            Source = request.Source,
            OrderingProviderId = orderingProviderId,
            OrderingProvider = orderingProviderName,
            OrderedUtc = request.OrderedUtc,
            SourceSystem = request.SourceSystem,
            OrderedByUser = request.OrderedByUser
        };

        // Rules see the specimen the order will be linked to, so resolve it before evaluating.
        var specimenType = await PeekSpecimenTypeAsync(patientId, request.EncounterId, request.SpecimenId, ct);
        var ruleOutcome = await ApplyOrderRulesAsync(patientId, order, builtLines, specimenType, ct);
        if (ruleOutcome.IsBlocked)
        {
            return OperationResult<Order>.Fail(ruleOutcome.BlockMessage!);
        }

        builtLines = ruleOutcome.Lines;
        OrderLineBuilder.ApplyHeaderFromLines(order, builtLines);

        var validation = OrderValidator.Validate(
            order,
            builtLines,
            patientExists,
            encounterExists,
            encounterBelongs,
            location is not null,
            location?.IsActive == true);

        if (validation.IsHardStopped)
        {
            return OperationResult<Order>.Fail(validation.HardStops.First().Message);
        }

        await _orders.AddAsync(order, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        foreach (var line in builtLines)
        {
            line.OrderId = order.Id;
            await _orderLines.AddAsync(line, ct);
        }

        if (_ruleEngine is not null)
        {
            await _ruleEngine.PersistOrderLogsAsync(ruleOutcome, order.Id, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        if (request.SpecimenId is > 0)
        {
            var link = await LinkSpecimenCoreAsync(patientId, order.Id, new LinkOrderSpecimenRequest(request.SpecimenId.Value), ct);
            if (!link.Succeeded)
            {
                return OperationResult<Order>.Fail(link.Error ?? "Order was created but the specimen could not be linked.");
            }
        }
        else
        {
            await AssociateCurrentSpecimenAsync(order.Id, patientId, request.EncounterId, ct);
        }

        return OperationResult<Order>.Ok(order, ruleOutcome.Warnings);
    }

    public async Task<OperationResult<Order>> UpdateAsync(long patientId, long orderId, UpdateOrderRequest request, CancellationToken ct = default)
    {
        if (request.Lines.Count == 0)
        {
            return OperationResult<Order>.Fail("At least one test or product is required.");
        }

        var unauthorized = await RejectUnauthorizedAsync<Order>(OrderAuthorizationRule.EvaluateUpdate, ct);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        var order = await _orders.FirstOrDefaultAsync(o => o.Id == orderId && o.PatientId == patientId, ct);
        if (order is null)
        {
            return OperationResult<Order>.Fail("Order not found.");
        }

        var merged = await RejectMergedPatientMessageAsync(patientId, ct);
        if (merged is not null)
        {
            return OperationResult<Order>.Fail(merged);
        }

        var editRule = OrderValidator.ValidateEditable(order);
        if (editRule is not null)
        {
            return OperationResult<Order>.Fail(editRule.Message);
        }

        var encounter = await _encounters.GetByIdAsync(request.EncounterId, ct);
        var location = await _locations.GetByIdAsync(request.OrderingLocationId, ct);
        var (orderingProviderId, orderingProviderName) = await ResolveOrderingProviderAsync(request.OrderingProviderId, ct);
        var productTypeMap = await LoadProductTypeMapAsync(ct);
        var lineInputs = OrderLineBuilder.WithCrossmatchLineIfNeeded(request.Lines, productTypeMap);
        lineInputs = await ExpandGrouperLinesAsync(lineInputs, ct);
        var builtLines = await BuildLinesAsync(lineInputs, productTypeMap, ct);

        order.EncounterId = request.EncounterId;
        order.OrderingLocationId = request.OrderingLocationId;
        order.Priority = request.Priority;
        order.OrderingProviderId = orderingProviderId;
        order.OrderingProvider = orderingProviderName;

        var specimenType = await PeekSpecimenTypeAsync(patientId, request.EncounterId, specimenId: null, ct);
        var ruleOutcome = await ApplyOrderRulesAsync(patientId, order, builtLines, specimenType, ct);
        if (ruleOutcome.IsBlocked)
        {
            return OperationResult<Order>.Fail(ruleOutcome.BlockMessage!);
        }

        builtLines = ruleOutcome.Lines;
        OrderLineBuilder.ApplyHeaderFromLines(order, builtLines);

        var validation = OrderValidator.Validate(
            order,
            builtLines,
            patientExists: true,
            encounterExists: encounter is not null,
            encounterBelongsToPatient: encounter?.PatientId == patientId,
            orderingLocationExists: location is not null,
            orderingLocationActive: location?.IsActive == true);

        if (validation.IsHardStopped)
        {
            return OperationResult<Order>.Fail(validation.HardStops.First().Message);
        }

        var existingLines = await _orderLines.ListAsync(l => l.OrderId == orderId && l.IsActive, ct);
        foreach (var existing in existingLines)
        {
            var tracked = await _orderLines.FirstOrDefaultAsync(l => l.Id == existing.Id, ct);
            if (tracked is not null)
            {
                tracked.IsActive = false;
                _orderLines.Update(tracked);
            }
        }

        _orders.Update(order);
        await _unitOfWork.SaveChangesAsync(ct);

        foreach (var line in builtLines)
        {
            line.OrderId = order.Id;
            await _orderLines.AddAsync(line, ct);
        }

        if (_ruleEngine is not null)
        {
            await _ruleEngine.PersistOrderLogsAsync(ruleOutcome, order.Id, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        await AssociateCurrentSpecimenAsync(order.Id, patientId, request.EncounterId, ct);

        return OperationResult<Order>.Ok(order, ruleOutcome.Warnings);
    }

    public async Task<OperationResult<Order>> CancelAsync(long patientId, long orderId, CancelOrderRequest request, CancellationToken ct = default)
    {
        var unauthorized = await RejectUnauthorizedAsync<Order>(OrderAuthorizationRule.EvaluateCancel, ct);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        var order = await _orders.FirstOrDefaultAsync(o => o.Id == orderId && o.PatientId == patientId, ct);
        if (order is null)
        {
            return OperationResult<Order>.Fail("Order not found.");
        }

        var merged = await RejectMergedPatientMessageAsync(patientId, ct);
        if (merged is not null)
        {
            return OperationResult<Order>.Fail(merged);
        }

        order.Status = OrderStatus.Cancelled;
        order.CancellationReason = request.CancellationReason.Trim();

        var activeLines = await _orderLines.ListAsync(l => l.OrderId == orderId && l.IsActive, ct);
        var validation = OrderValidator.Validate(
            order,
            activeLines,
            patientExists: true,
            encounterExists: true,
            encounterBelongsToPatient: true,
            orderingLocationExists: true,
            orderingLocationActive: true);

        if (validation.IsHardStopped)
        {
            return OperationResult<Order>.Fail(validation.HardStops.First().Message);
        }

        _orders.Update(order);
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<Order>.Ok(order);
    }

    public async Task<OperationResult<PatientOrderDto>> LinkSpecimenAsync(
        long patientId,
        long orderId,
        LinkOrderSpecimenRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var unauthorized = await RejectUnauthorizedAsync<PatientOrderDto>(OrderAuthorizationRule.EvaluateLink, ct);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        return await LinkSpecimenCoreAsync(patientId, orderId, request, ct);
    }

    private async Task<OperationResult<PatientOrderDto>> LinkSpecimenCoreAsync(
        long patientId,
        long orderId,
        LinkOrderSpecimenRequest request,
        CancellationToken ct)
    {
        var order = await _orders.FirstOrDefaultAsync(o => o.Id == orderId && o.PatientId == patientId, ct);
        if (order is null)
        {
            return OperationResult<PatientOrderDto>.Fail("Order not found.");
        }

        var merged = await RejectMergedPatientMessageAsync(patientId, ct);
        if (merged is not null)
        {
            return OperationResult<PatientOrderDto>.Fail(merged);
        }

        var editRule = OrderValidator.ValidateEditable(order);
        if (editRule is not null)
        {
            return OperationResult<PatientOrderDto>.Fail(editRule.Message);
        }

        var specimen = await _specimens.GetByIdAsync(request.SpecimenId, ct);
        if (specimen is null)
        {
            return OperationResult<PatientOrderDto>.Fail("Specimen not found.");
        }

        if (specimen.PatientId != patientId)
        {
            return OperationResult<PatientOrderDto>.Fail("Specimen does not belong to this patient.");
        }

        if (specimen.Status != SpecimenStatus.Accepted)
        {
            return OperationResult<PatientOrderDto>.Fail(
                $"Only Accepted specimens can be linked. Specimen {specimen.AccessionNumber} is {specimen.Status}.");
        }

        if (specimen.ExpiresUtc.HasValue && specimen.ExpiresUtc.Value <= _clock.UtcNow)
        {
            return OperationResult<PatientOrderDto>.Fail(
                $"Specimen {specimen.AccessionNumber} has expired and cannot be linked.");
        }

        await SyncOrderSpecimenAsync(orderId, specimen.Id, ct);

        var updated = (await ListByPatientAsync(patientId, ct: ct)).FirstOrDefault(o => o.Id == orderId);
        return updated is null
            ? OperationResult<PatientOrderDto>.Fail("Order not found after linking specimen.")
            : OperationResult<PatientOrderDto>.Ok(updated);
    }

    public async Task AssociateCurrentSpecimenAsync(long orderId, long patientId, long encounterId, CancellationToken ct)
    {
        var specimen = await ResolveCurrentSpecimenAsync(patientId, encounterId, ct);
        if (specimen is null)
        {
            return;
        }

        await SyncOrderSpecimenAsync(orderId, specimen.Id, ct);
    }

    private async Task<OrderRuleOutcome> ApplyOrderRulesAsync(
        long patientId,
        Order order,
        IReadOnlyList<OrderLine> lines,
        string? specimenType,
        CancellationToken ct)
    {
        if (_ruleEngine is null)
        {
            return new OrderRuleOutcome(lines, Array.Empty<RuleResult>(), null, Array.Empty<RuleExecutionLog>());
        }

        return await _ruleEngine.ApplyOrderRulesAsync(patientId, order, lines, specimenType, ct);
    }

    /// <summary>
    /// Specimen type the order will end up linked to. The link itself is made after the
    /// order is saved, but rules need the value while the lines are still pending.
    /// </summary>
    private async Task<string?> PeekSpecimenTypeAsync(
        long patientId,
        long encounterId,
        long? specimenId,
        CancellationToken ct)
    {
        if (_ruleEngine is null)
        {
            return null;
        }

        var specimen = specimenId is > 0
            ? await _specimens.GetByIdAsync(specimenId.Value, ct)
            : await ResolveCurrentSpecimenAsync(patientId, encounterId, ct);

        return string.IsNullOrWhiteSpace(specimen?.SpecimenType) ? null : specimen!.SpecimenType;
    }

    private async Task<Specimen?> ResolveCurrentSpecimenAsync(long patientId, long encounterId, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var specimens = await _specimens.ListAsync(s =>
            s.PatientId == patientId
            && s.Status == SpecimenStatus.Accepted
            && (s.ExpiresUtc == null || s.ExpiresUtc > now), ct);

        return specimens
            .OrderByDescending(s => s.EncounterId == encounterId)
            .ThenByDescending(s => s.CollectedUtc)
            .FirstOrDefault();
    }

    private async Task SyncOrderSpecimenAsync(long orderId, long specimenId, CancellationToken ct)
    {
        var existing = await _orderSpecimens.ListAsync(os => os.OrderId == orderId, ct);
        var primary = existing.FirstOrDefault(l => l.IsPrimary) ?? existing.FirstOrDefault();
        if (primary is not null)
        {
            if (primary.SpecimenId == specimenId)
            {
                return;
            }

            var tracked = await _orderSpecimens.FirstOrDefaultAsync(os => os.Id == primary.Id, ct);
            if (tracked is not null)
            {
                tracked.SpecimenId = specimenId;
                _orderSpecimens.Update(tracked);
            }
        }
        else
        {
            await _orderSpecimens.AddAsync(new OrderSpecimen
            {
                OrderId = orderId,
                SpecimenId = specimenId,
                IsPrimary = true
            }, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<IReadOnlyList<OrderLine>> BuildLinesAsync(
        IReadOnlyList<OrderLineInputDto> inputs,
        IReadOnlyDictionary<long, ProductType> productTypes,
        CancellationToken ct)
    {
        var lines = new List<OrderLine>();
        var lineNumber = 1;

        foreach (var input in inputs)
        {
            if (input.LineCategory == OrderCategory.Test)
            {
                if (string.IsNullOrWhiteSpace(input.TestCode))
                {
                    continue;
                }

                var code = input.TestCode.Trim().ToUpperInvariant();
                lines.Add(new OrderLine
                {
                    LineNumber = lineNumber++,
                    LineCategory = OrderCategory.Test,
                    LineName = await ResolveTestNameAsync(code, ct),
                    TestCode = code,
                    OrderType = OrderLineBuilder.MapTestOrderType(code)
                });
            }
            else if (input.LineCategory == OrderCategory.Product && input.ProductTypeId is > 0)
            {
                if (!productTypes.TryGetValue(input.ProductTypeId.Value, out var productType))
                {
                    throw new InvalidOperationException($"Product type {input.ProductTypeId.Value} not found.");
                }

                lines.Add(new OrderLine
                {
                    LineNumber = lineNumber++,
                    LineCategory = OrderCategory.Product,
                    LineName = productType.Name,
                    ProductTypeId = productType.Id,
                    OrderType = OrderType.Other,
                    FulfillmentStatus = FulfillmentStatus.Ordered
                });
            }
        }

        return lines;
    }

    private async Task<IReadOnlyList<OrderLineInputDto>> ExpandGrouperLinesAsync(
        IReadOnlyList<OrderLineInputDto> inputs,
        CancellationToken ct)
    {
        var groupers = await _testGroupers.ListAsync(g => g.IsActive && !g.IsDraft, ct);
        var grouperByCode = groupers.ToDictionary(g => g.Code, StringComparer.OrdinalIgnoreCase);

        var expanded = new List<OrderLineInputDto>();
        foreach (var input in inputs)
        {
            if (input.LineCategory != OrderCategory.Test || string.IsNullOrWhiteSpace(input.TestCode))
            {
                expanded.Add(input);
                continue;
            }

            var code = input.TestCode.Trim().ToUpperInvariant();
            if (grouperByCode.TryGetValue(code, out var grouper))
            {
                foreach (var member in TestGrouperMembers.Parse(grouper.MemberTestsJson).OrderBy(m => m.SortOrder))
                {
                    expanded.Add(new OrderLineInputDto(OrderCategory.Test, member.TestCode, null));
                }
            }
            else
            {
                expanded.Add(input);
            }
        }

        var seenTests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return expanded.Where(line =>
        {
            if (line.LineCategory != OrderCategory.Test || string.IsNullOrWhiteSpace(line.TestCode))
            {
                return true;
            }

            return seenTests.Add(line.TestCode.Trim().ToUpperInvariant());
        }).ToList();
    }

    private async Task<string> ResolveTestNameAsync(string testCode, CancellationToken ct)
    {
        var definition = await _testDefinitions.FirstOrDefaultAsync(
            t => t.Code == testCode && t.IsActive, ct);
        return definition?.Name ?? testCode;
    }

    private async Task<Dictionary<long, ProductType>> LoadProductTypeMapAsync(CancellationToken ct) =>
        (await _productTypes.ListAsync(ct)).ToDictionary(p => p.Id);

    private async Task<(long? Id, string? Name)> ResolveOrderingProviderAsync(long? providerId, CancellationToken ct)
    {
        if (providerId is not > 0)
        {
            return (null, null);
        }

        var provider = await _providers.GetByIdAsync(providerId.Value, ct);
        if (provider is null || !provider.IsActive)
        {
            return (null, null);
        }

        return (provider.Id, provider.Name);
    }

    private async Task<string?> RejectMergedPatientMessageAsync(long patientId, CancellationToken ct)
    {
        var patient = await _patients.GetByIdAsync(patientId, ct);
        if (patient is null)
        {
            return null;
        }

        var clinical = PatientMergeRule.EvaluateClinicalUse(patient.Status);
        return clinical.Severity == RuleSeverity.HardStop ? clinical.Message : null;
    }

    private async Task<OperationResult<T>?> RejectUnauthorizedAsync<T>(
        Func<bool, RuleResult> evaluate, CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var userName = _currentUser?.UserName ?? string.Empty;
        var allowed = await _permissions.HasPermissionAsync(userName, PermissionCodes.PatientWrite, ct);
        var auth = evaluate(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? OperationResult<T>.Fail(auth.Message)
            : null;
    }
}
