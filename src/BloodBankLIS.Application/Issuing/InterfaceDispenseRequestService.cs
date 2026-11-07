using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Issuing;

/// <summary>What an EHR "send this unit now" message (HL7 v2.5 OMB^O27) asks for, after the patient is resolved.</summary>
public sealed record InterfaceDispenseRequest(
    long PatientId,
    bool Cancel,
    string? OrderNumber,
    string? Din,
    string? Destination,
    string? Requester,
    string MessageControlId);

/// <summary>
/// OCD-063 (decided 2026-11-06): the EHR's request to send a reserved unit now becomes the same issue request a ward
/// makes in the LIS (Cycle 439): the reservation leads Ready to issue with its destination filled in. It never issues
/// anything; issue still runs every check. The unit must be identified without guessing: by the DIN when the facility
/// maps one, otherwise by the placer order number when exactly one unit is reserved on that order. Anything else is
/// refused with the reason, which the HL7 layer puts on the error queue for a person to resolve. A cancel (ORC-1 CA,
/// OC, or DC) withdraws an open request. Interface messages are trusted at the transport (OCD-034), so no user
/// permission is checked here.
/// </summary>
public sealed class InterfaceDispenseRequestService
{
    private readonly IRepository<Allocation> _allocations;
    private readonly IRepository<Order> _orders;
    private readonly IRepository<BloodUnit> _units;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;
    private readonly IUnitOfWork _unitOfWork;

    public InterfaceDispenseRequestService(
        IRepository<Allocation> allocations,
        IRepository<Order> orders,
        IRepository<BloodUnit> units,
        IClock clock,
        IAuditWriter audit,
        IUnitOfWork unitOfWork)
    {
        _allocations = allocations;
        _orders = orders;
        _units = units;
        _clock = clock;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    /// <summary>Returns the acknowledgement text; throws <see cref="InvalidOperationException"/> with the reason when refused.</summary>
    public async Task<string> HandleAsync(InterfaceDispenseRequest request, CancellationToken ct = default)
    {
        var (found, unitNumber) = await FindReservationAsync(request, ct);
        // The search reads without tracking; change the tracked reservation, as a ward request does.
        var allocation = await _allocations.GetByIdAsync(found.Id, ct)
            ?? throw new InvalidOperationException($"The reservation for {unitNumber} is no longer there.");
        var requester = string.IsNullOrWhiteSpace(request.Requester) ? "EHR (HL7)" : $"EHR (HL7): {request.Requester.Trim()}";

        if (request.Cancel)
        {
            if (allocation.IssueRequestedUtc is null)
            {
                return $"No open issue request for {unitNumber}; nothing to cancel.";
            }

            var before = new { allocation.IssueRequestedUtc, allocation.IssueRequestedBy, allocation.IssueRequestedTo };
            allocation.IssueRequestedUtc = null;
            allocation.IssueRequestedBy = null;
            allocation.IssueRequestedTo = null;
            _allocations.Update(allocation);
            _audit.Record(
                AuditEventType.Assignment,
                nameof(Allocation),
                allocation.Id,
                oldValue: before,
                newValue: new { allocation.PatientId, allocation.BloodProductId, CancelledBy = requester, request.MessageControlId },
                reason: $"Issue request cancelled by the EHR (HL7 {request.MessageControlId}).");
            await _unitOfWork.SaveChangesAsync(ct);
            return $"Issue request for {unitNumber} cancelled.";
        }

        var destination = request.Destination?.Trim();
        if (string.IsNullOrEmpty(destination))
        {
            throw new InvalidOperationException($"The request for {unitNumber} names no destination (BPO-14 or PV1-3).");
        }

        if (allocation.IssueRequestedUtc is DateTime already)
        {
            if (string.Equals(allocation.IssueRequestedTo, destination, StringComparison.OrdinalIgnoreCase))
            {
                return $"{unitNumber} was already requested to {destination}.";
            }

            throw new InvalidOperationException(
                $"{unitNumber} was already requested at {already:yyyy-MM-dd HH:mm} UTC for {allocation.IssueRequestedTo}; cancel that request before asking for {destination}.");
        }

        allocation.IssueRequestedUtc = _clock.UtcNow;
        allocation.IssueRequestedBy = requester;
        allocation.IssueRequestedTo = destination;
        _allocations.Update(allocation);
        _audit.Record(
            AuditEventType.Assignment,
            nameof(Allocation),
            allocation.Id,
            newValue: new
            {
                allocation.PatientId,
                allocation.BloodProductId,
                allocation.IssueRequestedUtc,
                allocation.IssueRequestedBy,
                allocation.IssueRequestedTo,
                request.MessageControlId
            },
            reason: $"Issue requested to {destination} by the EHR (HL7 {request.MessageControlId}).");
        await _unitOfWork.SaveChangesAsync(ct);
        return $"Issue of {unitNumber} requested to {destination}.";
    }

    private async Task<(Allocation Allocation, string UnitNumber)> FindReservationAsync(InterfaceDispenseRequest request, CancellationToken ct)
    {
        var reserved = await _allocations.ListAsync(a => a.PatientId == request.PatientId && a.Status == AllocationStatus.Reserved, ct);
        var unitIds = reserved.Select(a => a.BloodProductId).Distinct().ToList();
        var units = unitIds.Count == 0
            ? new Dictionary<long, BloodUnit>()
            : (await _units.ListAsync(u => unitIds.Contains(u.Id), ct)).ToDictionary(u => u.Id);
        string NumberOf(Allocation a) => units.TryGetValue(a.BloodProductId, out var u) ? u.UnitNumber : $"unit {a.BloodProductId}";

        var din = request.Din?.Trim();
        if (!string.IsNullOrEmpty(din))
        {
            var byDin = reserved.Where(a => units.TryGetValue(a.BloodProductId, out var u)
                    && (string.Equals(u.UnitNumber, din, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(u.Din, din, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            return byDin.Count == 1
                ? (byDin[0], NumberOf(byDin[0]))
                : throw new InvalidOperationException(byDin.Count == 0
                    ? $"No unit {din} is reserved for this patient."
                    : $"{byDin.Count} reserved units match {din}; resolve it on the issue desk.");
        }

        var orderNumber = request.OrderNumber?.Trim();
        if (string.IsNullOrEmpty(orderNumber))
        {
            throw new InvalidOperationException("The request names neither a unit nor an order (ORC-2).");
        }

        var order = await _orders.FirstOrDefaultAsync(o => o.PatientId == request.PatientId && o.OrderNumber == orderNumber, ct)
            ?? throw new InvalidOperationException($"Order {orderNumber} is not on this patient.");
        var onOrder = reserved.Where(a => a.OrderId == order.Id).ToList();
        return onOrder.Count == 1
            ? (onOrder[0], NumberOf(onOrder[0]))
            : throw new InvalidOperationException(onOrder.Count == 0
                ? $"No unit is reserved on order {orderNumber}."
                : $"Order {orderNumber} has {onOrder.Count} reserved units and the request names none; request the unit in the LIS or send its DIN.");
    }
}
