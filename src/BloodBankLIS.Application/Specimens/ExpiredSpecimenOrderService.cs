using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Specimens;

public sealed record ExpiredSpecimenOrderSweepResult(
    int SpecimensMarkedExpired,
    int CrossmatchOrdersUnlinked,
    int OrdersCancelled,
    int OrdersLeftOpen);

/// <summary>
/// When a specimen reaches its expiration, unlink it from pending crossmatch orders
/// and cancel every other open order that still uses it. Closed orders keep their
/// specimen link. A non-crossmatch order with reserved units is left open so the
/// reservation is not orphaned.
/// </summary>
public sealed class ExpiredSpecimenOrderService
{
    private readonly IRepository<Specimen> _specimens;
    private readonly IRepository<Order> _orders;
    private readonly IRepository<OrderLine> _orderLines;
    private readonly IRepository<OrderSpecimen> _orderSpecimens;
    private readonly IRepository<OrderAction> _orderActions;
    private readonly IRepository<TestResult> _results;
    private readonly IRepository<Allocation> _allocations;
    private readonly IRepository<ProductType> _productTypes;
    private readonly IRepository<TestDefinition> _testDefinitions;
    private readonly IRepository<OrderCancellationReason> _cancellationReasons;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter? _audit;

    public ExpiredSpecimenOrderService(
        IRepository<Specimen> specimens,
        IRepository<Order> orders,
        IRepository<OrderLine> orderLines,
        IRepository<OrderSpecimen> orderSpecimens,
        IRepository<OrderAction> orderActions,
        IRepository<TestResult> results,
        IRepository<Allocation> allocations,
        IRepository<ProductType> productTypes,
        IRepository<TestDefinition> testDefinitions,
        IRepository<OrderCancellationReason> cancellationReasons,
        IClock clock,
        IUnitOfWork unitOfWork,
        IAuditWriter? audit = null)
    {
        _specimens = specimens;
        _orders = orders;
        _orderLines = orderLines;
        _orderSpecimens = orderSpecimens;
        _orderActions = orderActions;
        _results = results;
        _allocations = allocations;
        _productTypes = productTypes;
        _testDefinitions = testDefinitions;
        _cancellationReasons = cancellationReasons;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task<ExpiredSpecimenOrderSweepResult> ProcessAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var expired = await _specimens.ListAsync(
            s => s.ExpiresUtc != null && s.ExpiresUtc <= now, ct);
        if (expired.Count == 0)
        {
            return new ExpiredSpecimenOrderSweepResult(0, 0, 0, 0);
        }

        var marked = await MarkExpiredAsync(expired, ct);
        var expiredById = expired.ToDictionary(s => s.Id);
        var expiredIds = expiredById.Keys.ToList();

        var openOrders = await _orders.ListAsync(
            o => o.Status != OrderStatus.Completed
                 && o.Status != OrderStatus.Cancelled
                 && o.Status != OrderStatus.Discontinued,
            ct);
        if (openOrders.Count == 0)
        {
            if (marked > 0)
            {
                await _unitOfWork.SaveChangesAsync(ct);
            }

            return new ExpiredSpecimenOrderSweepResult(marked, 0, 0, 0);
        }

        var openIds = openOrders.Select(o => o.Id).ToList();
        var links = await _orderSpecimens.ListAsync(
            os => openIds.Contains(os.OrderId) && expiredIds.Contains(os.SpecimenId), ct);
        if (links.Count == 0)
        {
            if (marked > 0)
            {
                await _unitOfWork.SaveChangesAsync(ct);
            }

            return new ExpiredSpecimenOrderSweepResult(marked, 0, 0, 0);
        }

        var crossmatchCodes = await LoadCrossmatchTestCodesAsync(ct);
        var unlinked = 0;
        var cancelled = 0;
        var leftOpen = 0;

        foreach (var group in links.GroupBy(l => l.OrderId))
        {
            var order = await _orders.FirstOrDefaultAsync(o => o.Id == group.Key, ct);
            if (order is null || !ExpiredSpecimenOrderDisposition.IsOpen(order.Status))
            {
                continue;
            }

            var lines = await _orderLines.ListAsync(l => l.OrderId == order.Id, ct);
            var requiresCrossmatch = await ProductRequiresCrossmatchAsync(lines, ct);
            var pendingCrossmatch = ExpiredSpecimenOrderDisposition.IsPendingCrossmatchOrder(
                order.Status,
                order.OrderType,
                lines.Select(l => new OrderLineDispositionFacts(
                    l.LineCategory, l.OrderType, l.TestCode, l.ProductTypeId, l.IsActive)),
                crossmatchCodes,
                id => requiresCrossmatch.TryGetValue(id, out var required) && required);

            if (pendingCrossmatch)
            {
                if (await UnlinkAsync(order, group.ToList(), expiredById, now, ct) > 0)
                {
                    unlinked++;
                }

                continue;
            }

            if (await _allocations.AnyAsync(
                    a => a.OrderId == order.Id && a.Status == AllocationStatus.Reserved, ct))
            {
                leftOpen++;
                continue;
            }

            await CancelAsync(order, lines, ct);
            cancelled++;
        }

        if (marked > 0 || unlinked > 0 || cancelled > 0)
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return new ExpiredSpecimenOrderSweepResult(marked, unlinked, cancelled, leftOpen);
    }

    private async Task<int> MarkExpiredAsync(IReadOnlyList<Specimen> expired, CancellationToken ct)
    {
        var marked = 0;
        foreach (var specimen in expired)
        {
            if (specimen.Status is not (SpecimenStatus.Collected or SpecimenStatus.Received or SpecimenStatus.Accepted))
            {
                continue;
            }

            var tracked = await _specimens.FirstOrDefaultAsync(s => s.Id == specimen.Id, ct);
            if (tracked is null
                || tracked.Status is not (SpecimenStatus.Collected or SpecimenStatus.Received or SpecimenStatus.Accepted))
            {
                continue;
            }

            tracked.Status = SpecimenStatus.Expired;
            _specimens.Update(tracked);
            marked++;
        }

        return marked;
    }

    private async Task<HashSet<string>> LoadCrossmatchTestCodesAsync(CancellationToken ct)
    {
        var definitions = await _testDefinitions.ListAsync(
            t => t.ResultValueType == ResultValueType.Crossmatch
                 || t.ResultValueType == ResultValueType.ComplexCrossmatch,
            ct);
        return definitions.Select(t => t.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<long, bool>> ProductRequiresCrossmatchAsync(
        IReadOnlyList<OrderLine> lines,
        CancellationToken ct)
    {
        var productIds = lines
            .Where(l => l.IsActive && l.LineCategory == OrderCategory.Product && l.ProductTypeId is > 0)
            .Select(l => l.ProductTypeId!.Value)
            .Distinct()
            .ToList();
        if (productIds.Count == 0)
        {
            return new Dictionary<long, bool>();
        }

        var products = await _productTypes.ListAsync(p => productIds.Contains(p.Id), ct);
        return products.ToDictionary(p => p.Id, p => p.RequiresCrossmatch);
    }

    private async Task<int> UnlinkAsync(
        Order order,
        IReadOnlyList<OrderSpecimen> expiredLinks,
        IReadOnlyDictionary<long, Specimen> specimens,
        DateTime now,
        CancellationToken ct)
    {
        var all = await _orderSpecimens.ListAsync(os => os.OrderId == order.Id, ct);
        var expiredIds = expiredLinks.Select(l => l.Id).ToHashSet();
        var count = 0;

        foreach (var link in expiredLinks)
        {
            var tracked = await _orderSpecimens.FirstOrDefaultAsync(os => os.Id == link.Id, ct);
            if (tracked is null || tracked.UnlinkedUtc is not null)
            {
                continue;
            }

            tracked.UnlinkedUtc = now;
            tracked.UnlinkReason = ExpiredSpecimenOrderDisposition.UnlinkReason;
            tracked.IsPrimary = false;
            _orderSpecimens.Update(tracked);
            count++;

            specimens.TryGetValue(tracked.SpecimenId, out var specimen);
            _audit?.Record(
                AuditEventType.OrderChange,
                nameof(Order),
                order.Id,
                oldValue: new { SpecimenId = tracked.SpecimenId, Link = "Active" },
                newValue: new
                {
                    order.OrderNumber,
                    SpecimenId = tracked.SpecimenId,
                    AccessionNumber = specimen?.AccessionNumber,
                    Link = "Unlinked"
                },
                reason: ExpiredSpecimenOrderDisposition.UnlinkReason);
        }

        if (count == 0)
        {
            return 0;
        }

        var survivors = all.Where(l => !expiredIds.Contains(l.Id)).ToList();
        if (survivors.Count > 0 && survivors.All(l => !l.IsPrimary))
        {
            var promoteId = survivors.Min(l => l.Id);
            var promote = await _orderSpecimens.FirstOrDefaultAsync(os => os.Id == promoteId, ct);
            if (promote is not null && promote.UnlinkedUtc is null)
            {
                promote.IsPrimary = true;
                _orderSpecimens.Update(promote);
            }
        }

        return count;
    }

    private async Task CancelAsync(Order order, IReadOnlyList<OrderLine> lines, CancellationToken ct)
    {
        var previousStatus = order.Status;
        order.Status = OrderStatus.Cancelled;
        order.CancellationReasonCode = ExpiredSpecimenOrderDisposition.CancellationReasonCode;
        order.CancellationReason = await ResolveCancellationTextAsync(ct);
        _orders.Update(order);

        await CancelPendingTestLinesAsync(order.Id, lines, ct);

        var pendingActions = await _orderActions.ListAsync(
            a => a.OrderId == order.Id && a.Status == OrderActionStatus.Pending, ct);
        foreach (var action in pendingActions)
        {
            var tracked = await _orderActions.FirstOrDefaultAsync(a => a.Id == action.Id, ct);
            if (tracked is null)
            {
                continue;
            }

            tracked.Status = OrderActionStatus.Cancelled;
            _orderActions.Update(tracked);
        }

        _audit?.Record(
            AuditEventType.OrderChange,
            nameof(Order),
            order.Id,
            oldValue: new { Status = previousStatus },
            newValue: new { order.OrderNumber, order.Status, order.CancellationReasonCode, order.CancellationReason },
            reason: order.CancellationReason);
    }

    private async Task<string> ResolveCancellationTextAsync(CancellationToken ct)
    {
        var reason = await _cancellationReasons.FirstOrDefaultAsync(
            r => r.IsActive && r.Code == ExpiredSpecimenOrderDisposition.CancellationReasonCode, ct);
        return string.IsNullOrWhiteSpace(reason?.Text)
            ? ExpiredSpecimenOrderDisposition.CancellationReason
            : reason.Text;
    }

    /// <summary>
    /// Pending test lines leave the worklist when the order is cancelled. A verified
    /// result stays so completed testing remains on the chart.
    /// </summary>
    private async Task CancelPendingTestLinesAsync(
        long orderId,
        IReadOnlyList<OrderLine> activeLines,
        CancellationToken ct)
    {
        var verified = await _results.ListAsync(
            r => r.OrderId == orderId && r.Status == ResultStatus.Verified && r.SupersededByResultId == null,
            ct);
        var verifiedCodes = verified.Select(r => r.TestCode).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var line in activeLines.Where(l => l.IsActive && l.LineCategory == OrderCategory.Test))
        {
            var alreadyVerified = line.ResultStatus == ResultStatus.Verified
                || (line.TestCode is not null && verifiedCodes.Contains(line.TestCode));
            if (alreadyVerified)
            {
                continue;
            }

            var tracked = await _orderLines.FirstOrDefaultAsync(l => l.Id == line.Id, ct);
            if (tracked is null)
            {
                continue;
            }

            tracked.IsActive = false;
            tracked.FulfillmentStatus = FulfillmentStatus.Cancelled;
            _orderLines.Update(tracked);
        }
    }
}
