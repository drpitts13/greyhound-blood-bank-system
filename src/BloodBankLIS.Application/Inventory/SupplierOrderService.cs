using System.Globalization;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Reporting;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Inventory;

public sealed record SupplierOrderLineDto(long Id, long ProductTypeId, string ProductCode, string ProductName, AboGroup? Abo, int Quantity);

public sealed record SupplierOrderDto(
    long Id,
    string OrderNumber,
    long SupplierId,
    string SupplierName,
    SupplierOrderStatus Status,
    string? Notes,
    DateTime CreatedUtc,
    string CreatedBy,
    string? SubmittedBy,
    DateTime? SubmittedUtc,
    long? DeliveryId,
    DateTime? ReceivedUtc,
    string? CancelReason,
    int UnitsOrdered,
    IReadOnlyList<SupplierOrderLineDto> Lines);

public sealed record CreateSupplierOrderRequest(long SupplierId);

public sealed record SupplierOrderLineInput(long ProductTypeId, AboGroup? Abo, int Quantity);

public sealed record UpdateSupplierOrderRequest(IReadOnlyList<SupplierOrderLineInput> Lines, string? Notes);

public sealed record SubmitSupplierOrderRequest(long SignatureId);

public sealed record ReceiveSupplierOrderRequest(long DeliveryId);

public sealed record CancelSupplierOrderRequest(string Reason);

public sealed record SupplierOrderFile(string FileName, string ContentType, byte[] Bytes);

/// <summary>
/// Supplier orders built from the par shortfall. The order is printed or exported and
/// sent outside the system; no EDI or supplier interface is implied.
/// </summary>
public sealed class SupplierOrderService
{
    private readonly IRepository<SupplierOrder> _orders;
    private readonly IRepository<SupplierOrderLine> _lines;
    private readonly IRepository<BloodSupplier> _suppliers;
    private readonly IRepository<BloodDelivery> _deliveries;
    private readonly IRepository<ProductType> _products;
    private readonly StockParService _par;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly ISignatureService? _signatures;
    private readonly IReportDocumentRenderer? _renderer;

    public SupplierOrderService(
        IRepository<SupplierOrder> orders,
        IRepository<SupplierOrderLine> lines,
        IRepository<BloodSupplier> suppliers,
        IRepository<BloodDelivery> deliveries,
        IRepository<ProductType> products,
        StockParService par,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        ISignatureService? signatures = null,
        IReportDocumentRenderer? renderer = null)
    {
        _orders = orders;
        _lines = lines;
        _suppliers = suppliers;
        _deliveries = deliveries;
        _products = products;
        _par = par;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _signatures = signatures;
        _renderer = renderer;
    }

    public async Task<IReadOnlyList<SupplierOrderDto>> ListAsync(CancellationToken ct = default)
    {
        var orders = await _orders.ListAsync(ct);
        var result = new List<SupplierOrderDto>();
        foreach (var order in orders.OrderByDescending(o => o.CreatedUtc).ThenByDescending(o => o.Id))
        {
            result.Add(await ToDtoAsync(order, ct));
        }

        return result;
    }

    public async Task<SupplierOrderDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var order = await _orders.GetByIdAsync(id, ct);
        return order is null ? null : await ToDtoAsync(order, ct);
    }

    /// <summary>Draft order with one line per par row that is short, for the shortfall quantity.</summary>
    public async Task<OperationResult<SupplierOrderDto>> CreateFromParAsync(CreateSupplierOrderRequest request, CancellationToken ct = default)
    {
        var supplier = await _suppliers.GetByIdAsync(request.SupplierId, ct);
        if (supplier is null || !supplier.IsActive)
        {
            return OperationResult<SupplierOrderDto>.Fail("Supplier not found.");
        }

        var shortRows = (await _par.ListAsync(ct)).Where(row => row.Shortfall > 0).ToList();
        if (shortRows.Count == 0)
        {
            return OperationResult<SupplierOrderDto>.Fail("No product is below par, so there is nothing to order.");
        }

        var order = new SupplierOrder
        {
            OrderNumber = await NextOrderNumberAsync(ct),
            BloodSupplierId = supplier.Id,
            Status = SupplierOrderStatus.Draft
        };
        await _orders.AddAsync(order, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        foreach (var row in shortRows)
        {
            await _lines.AddAsync(new SupplierOrderLine
            {
                SupplierOrderId = order.Id,
                ProductTypeId = row.ProductTypeId,
                Abo = row.Abo,
                Quantity = Math.Min(row.Shortfall, SupplierOrderRule.MaxLineQuantity)
            }, ct);
        }

        _audit.Record(AuditEventType.Create, nameof(SupplierOrder), order.Id, null,
            new { order.OrderNumber, Supplier = supplier.Code, Lines = shortRows.Count, Units = shortRows.Sum(r => r.Shortfall) },
            "Supplier order drafted from par shortfall.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<SupplierOrderDto>.Ok(await ToDtoAsync(order, ct));
    }

    /// <summary>Replaces the draft's lines; a zero quantity removes the line.</summary>
    public async Task<OperationResult<SupplierOrderDto>> UpdateAsync(long id, UpdateSupplierOrderRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var order = await _orders.GetByIdAsync(id, ct);
        if (order is null)
        {
            return OperationResult<SupplierOrderDto>.Fail("Order not found.");
        }

        var edit = SupplierOrderRule.EvaluateEdit(order.Status);
        if (edit.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<SupplierOrderDto>.Fail(edit.Message);
        }

        var inputs = request.Lines ?? [];
        foreach (var input in inputs)
        {
            var quantity = SupplierOrderRule.EvaluateQuantity(input.Quantity);
            if (quantity.Severity == RuleSeverity.HardStop)
            {
                return OperationResult<SupplierOrderDto>.Fail(quantity.Message);
            }

            if (input.Quantity > 0 && await _products.GetByIdAsync(input.ProductTypeId, ct) is null)
            {
                return OperationResult<SupplierOrderDto>.Fail($"Product {input.ProductTypeId} not found.");
            }
        }

        var merged = inputs
            .Where(i => i.Quantity > 0)
            .GroupBy(i => (i.ProductTypeId, i.Abo))
            .Select(g => new SupplierOrderLineInput(g.Key.ProductTypeId, g.Key.Abo, Math.Min(g.Sum(i => i.Quantity), SupplierOrderRule.MaxLineQuantity)))
            .ToList();

        var existing = await _lines.ListAsync(l => l.SupplierOrderId == order.Id, ct);
        var before = existing.Select(l => new { l.ProductTypeId, l.Abo, l.Quantity }).ToList();
        foreach (var line in existing)
        {
            var tracked = await _lines.GetByIdAsync(line.Id, ct);
            var replacement = merged.FirstOrDefault(m => m.ProductTypeId == line.ProductTypeId && m.Abo == line.Abo);
            if (tracked is null)
            {
                continue;
            }

            // Lines are never hard-deleted; a removed line is kept at zero and hidden from the order.
            tracked.Quantity = replacement?.Quantity ?? 0;
            _lines.Update(tracked);
        }

        foreach (var input in merged.Where(m => !existing.Any(l => l.ProductTypeId == m.ProductTypeId && l.Abo == m.Abo)))
        {
            await _lines.AddAsync(new SupplierOrderLine
            {
                SupplierOrderId = order.Id,
                ProductTypeId = input.ProductTypeId,
                Abo = input.Abo,
                Quantity = input.Quantity
            }, ct);
        }

        order.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        _orders.Update(order);
        _audit.Record(AuditEventType.Update, nameof(SupplierOrder), order.Id,
            new { Lines = before },
            new { Lines = merged, order.Notes },
            "Supplier order edited.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<SupplierOrderDto>.Ok(await ToDtoAsync(order, ct));
    }

    public async Task<OperationResult<SupplierOrderDto>> SubmitAsync(long id, SubmitSupplierOrderRequest request, CancellationToken ct = default)
    {
        var order = await _orders.GetByIdAsync(id, ct);
        if (order is null)
        {
            return OperationResult<SupplierOrderDto>.Fail("Order not found.");
        }

        var units = (await _lines.ListAsync(l => l.SupplierOrderId == order.Id, ct)).Sum(l => l.Quantity);
        var signatureValid = request.SignatureId > 0
            && _signatures is not null
            && await _signatures.IsValidForCurrentUserAsync(request.SignatureId, SupplierOrderRule.SignatureAction, ct);
        var submit = SupplierOrderRule.EvaluateSubmit(order.Status, units, signatureValid);
        if (submit.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<SupplierOrderDto>.Fail($"{submit.Code}: {submit.Message}");
        }

        await _signatures!.ConsumeAsync(request.SignatureId, ct);
        order.Status = SupplierOrderStatus.Submitted;
        order.SubmittedBy = _currentUser.UserName;
        order.SubmittedUtc = _clock.UtcNow;
        order.SubmitSignatureId = request.SignatureId;
        _orders.Update(order);
        _audit.Record(AuditEventType.Update, nameof(SupplierOrder), order.Id,
            new { Status = SupplierOrderStatus.Draft },
            new { order.Status, order.OrderNumber, Units = units },
            "Supplier order submitted.",
            request.SignatureId);
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<SupplierOrderDto>.Ok(await ToDtoAsync(order, ct));
    }

    public async Task<OperationResult<SupplierOrderDto>> ReceiveAsync(long id, ReceiveSupplierOrderRequest request, CancellationToken ct = default)
    {
        var order = await _orders.GetByIdAsync(id, ct);
        if (order is null)
        {
            return OperationResult<SupplierOrderDto>.Fail("Order not found.");
        }

        var delivery = await _deliveries.GetByIdAsync(request.DeliveryId, ct);
        if (delivery is null)
        {
            return OperationResult<SupplierOrderDto>.Fail("Delivery not found.");
        }

        var receive = SupplierOrderRule.EvaluateReceive(order.Status, order.BloodSupplierId, delivery.BloodSupplierId);
        if (receive.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<SupplierOrderDto>.Fail($"{receive.Code}: {receive.Message}");
        }

        order.Status = SupplierOrderStatus.Received;
        order.BloodDeliveryId = delivery.Id;
        order.ReceivedUtc = _clock.UtcNow;
        _orders.Update(order);
        _audit.Record(AuditEventType.Update, nameof(SupplierOrder), order.Id,
            new { Status = SupplierOrderStatus.Submitted },
            new { order.Status, DeliveryId = delivery.Id, delivery.PackingListNumber },
            "Supplier order received against a delivery.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<SupplierOrderDto>.Ok(await ToDtoAsync(order, ct));
    }

    public async Task<OperationResult<SupplierOrderDto>> CancelAsync(long id, CancelSupplierOrderRequest request, CancellationToken ct = default)
    {
        var order = await _orders.GetByIdAsync(id, ct);
        if (order is null)
        {
            return OperationResult<SupplierOrderDto>.Fail("Order not found.");
        }

        var cancel = SupplierOrderRule.EvaluateCancel(order.Status, request?.Reason);
        if (cancel.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<SupplierOrderDto>.Fail(cancel.Message);
        }

        var from = order.Status;
        order.Status = SupplierOrderStatus.Cancelled;
        order.CancelledBy = _currentUser.UserName;
        order.CancelReason = request!.Reason.Trim();
        _orders.Update(order);
        _audit.Record(AuditEventType.Update, nameof(SupplierOrder), order.Id,
            new { Status = from }, new { order.Status, order.CancelReason }, "Supplier order cancelled.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<SupplierOrderDto>.Ok(await ToDtoAsync(order, ct));
    }

    /// <summary>The order as a PDF or CSV to send to the supplier. Exporting is audited.</summary>
    public async Task<OperationResult<SupplierOrderFile>> ExportAsync(long id, string format, CancellationToken ct = default)
    {
        var dto = await GetAsync(id, ct);
        if (dto is null)
        {
            return OperationResult<SupplierOrderFile>.Fail("Order not found.");
        }

        var columns = new[] { "product_code", "product", "abo", "quantity" };
        var rows = dto.Lines
            .Select(l => (IReadOnlyList<string?>)[l.ProductCode, l.ProductName, l.Abo?.ToString() ?? "Any", l.Quantity.ToString(CultureInfo.InvariantCulture)])
            .ToList();

        SupplierOrderFile file;
        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            file = new SupplierOrderFile($"{dto.OrderNumber}.csv", CsvFormat.ContentType, CsvFormat.Write(columns, rows));
        }
        else if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase) && _renderer is not null)
        {
            var title = $"Blood order {dto.OrderNumber} to {dto.SupplierName} ({dto.Status}, {dto.UnitsOrdered} units)";
            file = new SupplierOrderFile($"{dto.OrderNumber}.pdf", "application/pdf", _renderer.ToPdf(title, columns, rows, false));
        }
        else
        {
            return OperationResult<SupplierOrderFile>.Fail("Export format must be pdf or csv.");
        }

        _audit.Record(AuditEventType.Export, nameof(SupplierOrder), dto.Id, null,
            new { dto.OrderNumber, Format = format.ToLowerInvariant(), dto.UnitsOrdered }, "Supplier order exported.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<SupplierOrderFile>.Ok(file);
    }

    private async Task<string> NextOrderNumberAsync(CancellationToken ct)
    {
        var prefix = $"SO-{_clock.UtcNow:yyyyMMdd}-";
        var today = await _orders.ListAsync(o => o.OrderNumber.StartsWith(prefix), ct);
        return prefix + (today.Count + 1).ToString("000", CultureInfo.InvariantCulture);
    }

    private async Task<SupplierOrderDto> ToDtoAsync(SupplierOrder order, CancellationToken ct)
    {
        var supplier = await _suppliers.GetByIdAsync(order.BloodSupplierId, ct);
        var lines = (await _lines.ListAsync(l => l.SupplierOrderId == order.Id && l.Quantity > 0, ct)).ToList();
        var productIds = lines.Select(l => l.ProductTypeId).Distinct().ToList();
        var products = (await _products.ListAsync(p => productIds.Contains(p.Id), ct)).ToDictionary(p => p.Id);
        var lineDtos = lines
            .Select(l =>
            {
                products.TryGetValue(l.ProductTypeId, out var product);
                return new SupplierOrderLineDto(l.Id, l.ProductTypeId, product?.ProductCode ?? string.Empty, product?.Name ?? string.Empty, l.Abo, l.Quantity);
            })
            .OrderBy(l => l.ProductCode)
            .ThenBy(l => l.Abo)
            .ToList();
        return new SupplierOrderDto(
            order.Id, order.OrderNumber, order.BloodSupplierId, supplier?.Name ?? string.Empty, order.Status, order.Notes,
            order.CreatedUtc, order.CreatedBy, order.SubmittedBy, order.SubmittedUtc, order.BloodDeliveryId, order.ReceivedUtc,
            order.CancelReason, lineDtos.Sum(l => l.Quantity), lineDtos);
    }
}
