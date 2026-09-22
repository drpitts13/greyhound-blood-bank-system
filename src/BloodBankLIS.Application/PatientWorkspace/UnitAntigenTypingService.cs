using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Billing;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.PatientWorkspace;

/// <summary>
/// Opens unit antigen typing on a unit assigned to a patient, then completes the
/// action and captures its charge once the result is verified.
/// </summary>
public sealed class UnitAntigenTypingService
{
    public const string ActionCode = "UAGTYPE";
    public const string TestCode = "UAGTYPE";

    private readonly IRepository<Allocation> _allocations;
    private readonly IRepository<BloodUnit> _units;
    private readonly IRepository<Order> _orders;
    private readonly IRepository<OrderLine> _orderLines;
    private readonly IRepository<OrderSpecimen> _orderSpecimens;
    private readonly IRepository<OrderAction> _orderActions;
    private readonly IRepository<ActionDefinition> _actionDefinitions;
    private readonly IRepository<TestDefinition> _testDefinitions;
    private readonly IRepository<Specimen> _specimens;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter? _audit;
    private readonly IPermissionEvaluator? _permissions;
    private readonly BillingService? _billing;

    public UnitAntigenTypingService(
        IRepository<Allocation> allocations,
        IRepository<BloodUnit> units,
        IRepository<Order> orders,
        IRepository<OrderLine> orderLines,
        IRepository<OrderSpecimen> orderSpecimens,
        IRepository<OrderAction> orderActions,
        IRepository<ActionDefinition> actionDefinitions,
        IRepository<TestDefinition> testDefinitions,
        IRepository<Specimen> specimens,
        IClock clock,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IAuditWriter? audit = null,
        IPermissionEvaluator? permissions = null,
        BillingService? billing = null)
    {
        _allocations = allocations;
        _units = units;
        _orders = orders;
        _orderLines = orderLines;
        _orderSpecimens = orderSpecimens;
        _orderActions = orderActions;
        _actionDefinitions = actionDefinitions;
        _testDefinitions = testDefinitions;
        _specimens = specimens;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _permissions = permissions;
        _billing = billing;
    }

    public async Task<OperationResult<UnitAntigenTypingSessionDto>> StartAsync(
        long patientId,
        long allocationId,
        CancellationToken ct = default)
    {
        var unauthorized = await RejectEnterAsync(ct);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        var allocation = await _allocations.GetByIdAsync(allocationId, ct);
        if (allocation is null || allocation.PatientId != patientId)
        {
            return OperationResult<UnitAntigenTypingSessionDto>.Fail("This unit is not assigned to the patient.");
        }

        if (allocation.Status != AllocationStatus.Reserved || allocation.OrderId is not long productOrderId)
        {
            return OperationResult<UnitAntigenTypingSessionDto>.Fail("This unit is not assigned to the patient.");
        }

        var unit = await _units.GetByIdAsync(allocation.BloodProductId, ct);
        if (unit is null || !IsAssignedStatus(unit.Status))
        {
            return OperationResult<UnitAntigenTypingSessionDto>.Fail("This unit is not assigned to the patient.");
        }

        var productOrder = await _orders.FirstOrDefaultAsync(
            o => o.Id == productOrderId && o.PatientId == patientId, ct);
        if (productOrder is null)
        {
            return OperationResult<UnitAntigenTypingSessionDto>.Fail("This unit is not assigned to the patient.");
        }

        var productLines = await _orderLines.ListAsync(l => l.OrderId == productOrder.Id && l.IsActive, ct);
        if (!productLines.Any(l => l.LineCategory == OrderCategory.Product))
        {
            return OperationResult<UnitAntigenTypingSessionDto>.Fail("Unit antigen typing requires a product order.");
        }

        if (allocation.SpecimenId is not long specimenId)
        {
            return OperationResult<UnitAntigenTypingSessionDto>.Fail("The assignment has no specimen for result entry.");
        }

        var specimen = await _specimens.GetByIdAsync(specimenId, ct);
        if (specimen is null || specimen.PatientId != patientId || specimen.Status != SpecimenStatus.Accepted)
        {
            return OperationResult<UnitAntigenTypingSessionDto>.Fail("An accepted specimen is required to type this unit.");
        }

        var definition = await _actionDefinitions.FirstOrDefaultAsync(
            a => a.Code == ActionCode && a.IsActive, ct);
        var test = await _testDefinitions.FirstOrDefaultAsync(
            t => t.IsActive && !t.IsDraft && t.Code == TestCode, ct);
        if (definition is null || definition.Category != ActionCategory.UnitAntigenTyping)
        {
            return OperationResult<UnitAntigenTypingSessionDto>.Fail("Unit antigen typing is not configured.");
        }

        if (test is null || !test.ContributesToUnitBloodAttributes)
        {
            return OperationResult<UnitAntigenTypingSessionDto>.Fail("The unit antigen typing test is not configured.");
        }

        var existing = await _orderActions.ListAsync(
            a => a.OrderId == productOrder.Id
                 && a.ActionDefinitionId == definition.Id
                 && a.SourceBloodUnitId == unit.Id
                 && a.Status != OrderActionStatus.Cancelled,
            ct);
        var pendingForUnit = existing.FirstOrDefault(a => a.Status == OrderActionStatus.Pending);
        var completedForUnit = existing
            .Where(a => a.Status == OrderActionStatus.Completed)
            .OrderByDescending(a => a.Id)
            .FirstOrDefault();

        var testOrder = await EnsureTestOrderAsync(allocation, productOrder, specimen.Id, test, ct);
        if (testOrder.Line is null)
        {
            return OperationResult<UnitAntigenTypingSessionDto>.Fail(testOrder.Error ?? "The unit antigen typing test could not be opened.");
        }

        OrderAction action;
        if (pendingForUnit is not null)
        {
            action = await _orderActions.GetByIdAsync(pendingForUnit.Id, ct) ?? pendingForUnit;
            action.OrderLineId = testOrder.Line.Id;
            _orderActions.Update(action);
        }
        else if (completedForUnit is not null)
        {
            action = completedForUnit;
        }
        else
        {
            var validation = OrderActionValidator.ValidateAttach(
                hasProductLine: true, definition, duplicatePending: false);
            if (validation.IsHardStopped)
            {
                return OperationResult<UnitAntigenTypingSessionDto>.Fail(validation.HardStops.First().Message);
            }

            action = new OrderAction
            {
                OrderId = productOrder.Id,
                OrderLineId = testOrder.Line.Id,
                ActionDefinitionId = definition.Id,
                SourceBloodUnitId = unit.Id,
                Status = OrderActionStatus.Pending
            };
            await _orderActions.AddAsync(action, ct);
            _audit?.Record(
                AuditEventType.OrderChange,
                nameof(OrderAction),
                0,
                newValue: new { productOrder.Id, definition.Code, unit.UnitNumber },
                reason: "Unit antigen typing started on an assigned unit.");
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<UnitAntigenTypingSessionDto>.Ok(ToSession(
            allocation, unit, patientId, productOrder.Id, testOrder.Order!, testOrder.Line, specimen.Id, action));
    }

    /// <summary>
    /// Marks the action complete after a verified unit antigen result and captures
    /// the action charge, which queues the patient billing message.
    /// </summary>
    public async Task<OperationResult<OrderActionDto>> CompleteAfterVerifiedResultAsync(
        long orderActionId,
        TestResult result,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Status != ResultStatus.Verified
            || !string.Equals(result.TestCode, TestCode, StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<OrderActionDto>.Fail("Unit antigen typing is billed only after the result is verified.");
        }

        var action = await _orderActions.GetByIdAsync(orderActionId, ct);
        if (action is null)
        {
            return OperationResult<OrderActionDto>.Fail("Order action not found.");
        }

        var definition = await _actionDefinitions.GetByIdAsync(action.ActionDefinitionId, ct);
        if (definition is null || definition.Category != ActionCategory.UnitAntigenTyping)
        {
            return OperationResult<OrderActionDto>.Fail("The order action is not unit antigen typing.");
        }

        var typedUnit = ParseUnitNumber(result.Interpretation);
        if (action.SourceBloodUnitId is long sourceId)
        {
            var unit = await _units.GetByIdAsync(sourceId, ct);
            if (unit is null
                || !string.Equals(unit.UnitNumber, typedUnit, StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult<OrderActionDto>.Fail("The verified result does not match the assigned unit.");
            }
        }

        if (action.Status == OrderActionStatus.Cancelled)
        {
            return OperationResult<OrderActionDto>.Fail("A cancelled unit antigen typing action cannot be completed.");
        }

        if (action.Status == OrderActionStatus.Pending)
        {
            action.Status = OrderActionStatus.Completed;
            action.CompletedUtc = result.VerifiedUtc ?? _clock.UtcNow;
            action.CompletedBy = string.IsNullOrWhiteSpace(result.VerifiedBy)
                ? _currentUser.UserName
                : result.VerifiedBy;
            _orderActions.Update(action);
            _audit?.Record(
                AuditEventType.OrderChange,
                nameof(OrderAction),
                action.Id,
                newValue: new { action.Status, action.SourceBloodUnitId, result.Id },
                reason: "Unit antigen typing verified.");
            await _unitOfWork.SaveChangesAsync(ct);
        }

        if (_billing is not null)
        {
            var billed = await _billing.CaptureForActionAsync(action.Id, ct);
            if (!billed.Succeeded)
            {
                return OperationResult<OrderActionDto>.Fail(billed.Error ?? "Unit antigen typing could not be billed.");
            }
        }

        return OperationResult<OrderActionDto>.Ok(OrderActionDto.From(action, definition));
    }

    private async Task<(Order? Order, OrderLine? Line, string? Error)> EnsureTestOrderAsync(
        Allocation allocation,
        Order productOrder,
        long specimenId,
        TestDefinition test,
        CancellationToken ct)
    {
        var orderNumber = $"UAG-{allocation.Id}";
        var order = await _orders.FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, ct);
        if (order is null)
        {
            order = new Order
            {
                OrderNumber = orderNumber,
                PatientId = allocation.PatientId,
                EncounterId = allocation.EncounterId ?? productOrder.EncounterId,
                OrderingLocationId = productOrder.OrderingLocationId,
                OrderCategory = OrderCategory.Test,
                OrderName = test.Name,
                OrderType = OrderType.AntigenTyping,
                TestCode = TestCode,
                Priority = productOrder.Priority,
                Status = OrderStatus.InProcess,
                Source = OrderSource.Manual,
                OrderedUtc = _clock.UtcNow,
                OrderingProviderId = productOrder.OrderingProviderId,
                OrderingProvider = productOrder.OrderingProvider,
                ResultStatus = ResultStatus.Pending
            };
            await _orders.AddAsync(order, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        var line = await _orderLines.FirstOrDefaultAsync(
            l => l.OrderId == order.Id && l.IsActive && l.TestCode == TestCode, ct);
        if (line is null)
        {
            var count = await _orderLines.ListAsync(l => l.OrderId == order.Id, ct);
            line = new OrderLine
            {
                OrderId = order.Id,
                LineNumber = count.Count + 1,
                LineCategory = OrderCategory.Test,
                LineName = test.Name,
                TestCode = TestCode,
                OrderType = OrderType.AntigenTyping,
                ResultStatus = ResultStatus.Pending,
                IsActive = true
            };
            await _orderLines.AddAsync(line, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        var linked = await _orderSpecimens.AnyAsync(
            s => s.OrderId == order.Id && s.SpecimenId == specimenId, ct);
        if (!linked)
        {
            await _orderSpecimens.AddAsync(new OrderSpecimen
            {
                OrderId = order.Id,
                SpecimenId = specimenId,
                IsPrimary = true
            }, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return (order, line, null);
    }

    private static UnitAntigenTypingSessionDto ToSession(
        Allocation allocation,
        BloodUnit unit,
        long patientId,
        long productOrderId,
        Order testOrder,
        OrderLine line,
        long specimenId,
        OrderAction action) => new(
        allocation.Id,
        unit.Id,
        unit.UnitNumber,
        patientId,
        productOrderId,
        testOrder.Id,
        line.Id,
        specimenId,
        action.Id,
        TestCode,
        action.Status);

    private async Task<OperationResult<UnitAntigenTypingSessionDto>?> RejectEnterAsync(CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.ResultEnter, ct);
        var gate = ResultAuthorizationRule.EvaluateEnter(allowed);
        return gate.Severity == RuleSeverity.HardStop
            ? OperationResult<UnitAntigenTypingSessionDto>.Fail(gate.Message)
            : null;
    }

    private static bool IsAssignedStatus(UnitStatus status) =>
        status is UnitStatus.Assigned or UnitStatus.Allocated or UnitStatus.Crossmatched;

    private static string? ParseUnitNumber(string? interpretation)
    {
        if (string.IsNullOrWhiteSpace(interpretation))
        {
            return null;
        }

        const string prefix = "Unit: ";
        return interpretation.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? interpretation[prefix.Length..].Trim()
            : null;
    }
}
