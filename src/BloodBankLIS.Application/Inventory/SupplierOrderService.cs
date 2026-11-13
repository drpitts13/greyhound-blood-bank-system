using System.Globalization;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Reporting;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Inventory;

public sealed record SupplierOrderLineDto(long Id, long ProductTypeId, string ProductCode, string ProductName, AboGroup? Abo, int Quantity)
{
    public RhType? RhD { get; init; }
    public string? Requirements { get; init; }
    public long? PatientId { get; init; }
    public string? PatientMrn { get; init; }
    public string? PatientName { get; init; }

    /// <summary>Units on the linked delivery counted against this line; null until the order is received.</summary>
    public int? Received { get; init; }

    public int Short => Received is int r ? Math.Max(0, Quantity - r) : 0;
}

public sealed record SupplierOrderFollowUpRequest(string Note, DateTime? PromisedArrivalUtc = null);

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
    IReadOnlyList<SupplierOrderLineDto> Lines,
    // Submitted orders: when the blood is due (Inventory.ExpectedArrivalDueHours after submit), and whether that has passed.
    DateTime? ArrivalDueUtc = null,
    bool ArrivalOverdue = false)
{
    /// <summary>The last follow-up with the supplier: note, who and when; the promised arrival is in ArrivalDueUtc.</summary>
    public string? FollowUpNote { get; init; }

    public string? FollowUpBy { get; init; }

    public DateTime? FollowUpUtc { get; init; }

    public bool ArrivalPromised { get; init; }

    /// <summary>Units ordered that the linked delivery did not bring (0 before receipt).</summary>
    public int UnitsShort => Lines.Sum(l => l.Short);
}

/// <summary>With <paramref name="FromShortfall"/> false, an empty draft for a special order (a patient's units).</summary>
public sealed record CreateSupplierOrderRequest(long SupplierId, bool FromShortfall = true);

/// <summary>
/// One order line. Rh, the requirements the supplier reads, and the patient (by MRN) make a patient-specific line
/// (end-user testing, 2026-11-12: a patient needing irradiated, CMV-negative red cells could not be ordered for).
/// </summary>
public sealed record SupplierOrderLineInput(long ProductTypeId, AboGroup? Abo, int Quantity, RhType? RhD = null, string? Requirements = null, string? PatientMrn = null);

public sealed record UpdateSupplierOrderRequest(IReadOnlyList<SupplierOrderLineInput> Lines, string? Notes);

public sealed record SubmitSupplierOrderRequest(long SignatureId);

public sealed record ReceiveSupplierOrderRequest(long DeliveryId);

public sealed record CancelSupplierOrderRequest(string Reason);

public sealed record SupplierOrderFile(string FileName, string ContentType, byte[] Bytes);

/// <summary>
/// Supplier orders built from the minimum stock shortfall. The order is printed or exported and
/// sent outside the system; no EDI or supplier interface is implied.
/// </summary>
public sealed class SupplierOrderService
{
    private readonly IRepository<SupplierOrder> _orders;
    private readonly IRepository<SupplierOrderLine> _lines;
    private readonly IRepository<BloodSupplier> _suppliers;
    private readonly IRepository<BloodDelivery> _deliveries;
    private readonly IRepository<ProductType> _products;
    private readonly MinimumStockService _minimumStock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly ISignatureService? _signatures;
    private readonly IReportDocumentRenderer? _renderer;
    private readonly IRepository<BloodUnit>? _units;
    private readonly FacilityPolicyService? _policy;
    private readonly IRepository<Patient>? _patients;

    public SupplierOrderService(
        IRepository<SupplierOrder> orders,
        IRepository<SupplierOrderLine> lines,
        IRepository<BloodSupplier> suppliers,
        IRepository<BloodDelivery> deliveries,
        IRepository<ProductType> products,
        MinimumStockService minimumStock,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        ISignatureService? signatures = null,
        IReportDocumentRenderer? renderer = null,
        IRepository<BloodUnit>? units = null,
        FacilityPolicyService? policy = null,
        IRepository<Patient>? patients = null)
    {
        _patients = patients;
        _policy = policy;
        _units = units;
        _orders = orders;
        _lines = lines;
        _suppliers = suppliers;
        _deliveries = deliveries;
        _products = products;
        _minimumStock = minimumStock;
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

    /// <summary>
    /// Draft order with one line per minimum stock row that is short, for what is still to order: the shortfall less units
    /// already on submitted orders that have not arrived, so a shortage is not ordered twice.
    /// </summary>
    public async Task<OperationResult<SupplierOrderDto>> CreateFromShortfallAsync(CreateSupplierOrderRequest request, CancellationToken ct = default)
    {
        var supplier = await _suppliers.GetByIdAsync(request.SupplierId, ct);
        if (supplier is null || !supplier.IsActive)
        {
            return OperationResult<SupplierOrderDto>.Fail("Supplier not found.");
        }

        if (!request.FromShortfall)
        {
            var blank = new SupplierOrder
            {
                OrderNumber = await NextOrderNumberAsync(ct),
                BloodSupplierId = supplier.Id,
                Status = SupplierOrderStatus.Draft
            };
            await _orders.AddAsync(blank, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            _audit.Record(AuditEventType.Create, nameof(SupplierOrder), blank.Id, null,
                new { blank.OrderNumber, Supplier = supplier.Code, Special = true }, "Special supplier order drafted.");
            await _unitOfWork.SaveChangesAsync(ct);
            return OperationResult<SupplierOrderDto>.Ok(await ToDtoAsync(blank, ct));
        }

        var rows = await _minimumStock.ListAsync(ct);
        var shortRows = rows.Where(row => row.StillToOrder > 0).ToList();
        if (shortRows.Count == 0)
        {
            return OperationResult<SupplierOrderDto>.Fail(rows.Any(row => row.Shortfall > 0)
                ? "Every shortage is already on a submitted order that has not arrived, so there is nothing more to order. Receive those orders, or cancel one to order again."
                : "No product is below its minimum stock, so there is nothing to order.");
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
                Quantity = Math.Min(row.StillToOrder, SupplierOrderRule.MaxLineQuantity)
            }, ct);
        }

        _audit.Record(AuditEventType.Create, nameof(SupplierOrder), order.Id, null,
            new { order.OrderNumber, Supplier = supplier.Code, Lines = shortRows.Count, Units = shortRows.Sum(r => r.StillToOrder), AlreadyOnOrder = shortRows.Sum(r => Math.Min(r.Shortfall, r.OnOrder)) },
            "Supplier order drafted from minimum stock shortfall.");
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

        // A line for a patient names a registered, active patient by MRN.
        var patientIds = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var mrn in inputs.Where(i => i.Quantity > 0 && !string.IsNullOrWhiteSpace(i.PatientMrn)).Select(i => i.PatientMrn!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var patient = _patients is null ? null : (await _patients.ListAsync(p => p.MedicalRecordNumber.ToUpper() == mrn.ToUpper(), ct)).FirstOrDefault();
            if (patient is null || patient.Status == PatientStatus.Merged)
            {
                return OperationResult<SupplierOrderDto>.Fail($"No active patient has the MRN {mrn}.");
            }

            patientIds[mrn] = patient.Id;
        }

        static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        var keyed = inputs
            .Where(i => i.Quantity > 0)
            .Select(i => (Key: (i.ProductTypeId, i.Abo, i.RhD, Requirements: Clean(i.Requirements),
                    PatientId: Clean(i.PatientMrn) is { } m ? patientIds[m] : (long?)null), i.Quantity))
            .GroupBy(x => x.Key)
            .Select(g => (g.Key, Quantity: Math.Min(g.Sum(x => x.Quantity), SupplierOrderRule.MaxLineQuantity)))
            .ToList();

        static (long, AboGroup?, RhType?, string?, long?) KeyOf(SupplierOrderLine l) => (l.ProductTypeId, l.Abo, l.RhD, l.Requirements, l.PatientId);
        var existing = await _lines.ListAsync(l => l.SupplierOrderId == order.Id, ct);
        var before = existing.Select(l => new { l.ProductTypeId, l.Abo, l.RhD, l.Requirements, l.PatientId, l.Quantity }).ToList();
        foreach (var line in existing)
        {
            var tracked = await _lines.GetByIdAsync(line.Id, ct);
            var replacement = keyed.Where(k => k.Key == KeyOf(line)).Select(k => (int?)k.Quantity).FirstOrDefault();
            if (tracked is null)
            {
                continue;
            }

            // Lines are never hard-deleted; a removed line is kept at zero and hidden from the order.
            tracked.Quantity = replacement ?? 0;
            _lines.Update(tracked);
        }

        foreach (var input in keyed.Where(k => !existing.Any(l => KeyOf(l) == k.Key)))
        {
            await _lines.AddAsync(new SupplierOrderLine
            {
                SupplierOrderId = order.Id,
                ProductTypeId = input.Key.ProductTypeId,
                Abo = input.Key.Abo,
                RhD = input.Key.RhD,
                Requirements = input.Key.Requirements,
                PatientId = input.Key.PatientId,
                Quantity = input.Quantity
            }, ct);
        }

        order.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        _orders.Update(order);
        _audit.Record(AuditEventType.Update, nameof(SupplierOrder), order.Id,
            new { Lines = before },
            new { Lines = keyed.Select(k => new { k.Key.ProductTypeId, k.Key.Abo, k.Key.RhD, k.Key.Requirements, k.Key.PatientId, k.Quantity }), order.Notes },
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
        var dto = await ToDtoAsync(order, ct);
        var shortLines = dto.Lines.Where(l => l.Short > 0)
            .Select(l => $"{l.ProductCode} {(l.Abo?.ToString() ?? "any ABO")}: {l.Short} of {l.Quantity} short")
            .ToList();
        _audit.Record(AuditEventType.Update, nameof(SupplierOrder), order.Id,
            new { Status = SupplierOrderStatus.Submitted },
            new { order.Status, DeliveryId = delivery.Id, delivery.PackingListNumber, dto.UnitsOrdered, dto.UnitsShort, Short = shortLines },
            dto.UnitsShort == 0
                ? "Supplier order received against a delivery."
                : $"Supplier order received against a delivery, {dto.UnitsShort} unit(s) short: {string.Join("; ", shortLines)}.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<SupplierOrderDto>.Ok(dto);
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

    /// <summary>
    /// Records a follow-up with the supplier on a submitted order (what was said, and the arrival they now promise, if
    /// any); the latest replaces the last on the order and each is audited.
    /// </summary>
    public async Task<OperationResult<SupplierOrderDto>> RecordFollowUpAsync(long id, SupplierOrderFollowUpRequest request, CancellationToken ct = default)
    {
        var order = await _orders.GetByIdAsync(id, ct);
        if (order is null)
        {
            return OperationResult<SupplierOrderDto>.Fail("Order not found.");
        }

        var promised = request?.PromisedArrivalUtc is DateTime p ? DateTime.SpecifyKind(p, DateTimeKind.Utc) : (DateTime?)null;
        var check = SupplierOrderRule.EvaluateFollowUp(order.Status, request?.Note, promised, _clock.UtcNow);
        if (check.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<SupplierOrderDto>.Fail(check.Message);
        }

        var before = new { order.FollowUpNote, order.PromisedArrivalUtc };
        var note = request!.Note.Trim();
        order.FollowUpNote = note.Length <= 500 ? note : note[..500];
        order.FollowUpBy = _currentUser.UserName;
        order.FollowUpUtc = _clock.UtcNow;
        if (promised is not null)
        {
            order.PromisedArrivalUtc = promised;
        }

        _orders.Update(order);
        _audit.Record(AuditEventType.Update, nameof(SupplierOrder), order.Id, before,
            new { order.FollowUpNote, order.PromisedArrivalUtc }, $"Supplier followed up: {order.FollowUpNote}");
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

        var columns = new[] { "product_code", "product", "abo", "rh", "requirements", "for_patient", "quantity" };
        var rows = dto.Lines
            .Select(l => (IReadOnlyList<string?>)[l.ProductCode, l.ProductName, l.Abo?.ToString() ?? "Any",
                l.RhD is RhType rh ? (rh == RhType.Positive ? "Pos" : "Neg") : "Any",
                l.Requirements, l.PatientMrn is null ? null : $"{l.PatientName} ({l.PatientMrn})",
                l.Quantity.ToString(CultureInfo.InvariantCulture)])
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

    /// <summary>
    /// Counts the delivery's units against the order lines: lines for a named ABO group take matching units first, then
    /// any-ABO lines take what is left of that product, so no unit is counted twice. Extra units are not counted.
    /// </summary>
    public static List<SupplierOrderLineDto> Reconcile(IReadOnlyList<SupplierOrderLineDto> lines, IEnumerable<BloodUnit> delivered)
    {
        var left = delivered.GroupBy(u => (u.ProductTypeId, u.Abo)).ToDictionary(g => g.Key, g => g.Count());
        var received = new Dictionary<long, int>();
        foreach (var line in lines.Where(l => l.Abo is not null))
        {
            var key = (line.ProductTypeId, line.Abo!.Value);
            var take = Math.Min(line.Quantity, left.GetValueOrDefault(key));
            left[key] = left.GetValueOrDefault(key) - take;
            received[line.Id] = take;
        }

        foreach (var line in lines.Where(l => l.Abo is null))
        {
            var take = 0;
            foreach (var key in left.Keys.Where(k => k.ProductTypeId == line.ProductTypeId).ToList())
            {
                var more = Math.Min(line.Quantity - take, left[key]);
                left[key] -= more;
                take += more;
            }

            received[line.Id] = take;
        }

        return lines.Select(l => l with { Received = received.GetValueOrDefault(l.Id) }).ToList();
    }

    private async Task<SupplierOrderDto> ToDtoAsync(SupplierOrder order, CancellationToken ct)
    {
        var supplier = await _suppliers.GetByIdAsync(order.BloodSupplierId, ct);
        var lines = (await _lines.ListAsync(l => l.SupplierOrderId == order.Id && l.Quantity > 0, ct)).ToList();
        var productIds = lines.Select(l => l.ProductTypeId).Distinct().ToList();
        var products = (await _products.ListAsync(p => productIds.Contains(p.Id), ct)).ToDictionary(p => p.Id);
        var linePatientIds = lines.Where(l => l.PatientId is not null).Select(l => l.PatientId!.Value).Distinct().ToList();
        var linePatients = _patients is null || linePatientIds.Count == 0
            ? new Dictionary<long, Patient>()
            : (await _patients.ListAsync(p => linePatientIds.Contains(p.Id), ct)).ToDictionary(p => p.Id);
        var lineDtos = lines
            .Select(l =>
            {
                products.TryGetValue(l.ProductTypeId, out var product);
                var patient = l.PatientId is long pid ? linePatients.GetValueOrDefault(pid) : null;
                return new SupplierOrderLineDto(l.Id, l.ProductTypeId, product?.ProductCode ?? string.Empty, product?.Name ?? string.Empty, l.Abo, l.Quantity)
                {
                    RhD = l.RhD,
                    Requirements = l.Requirements,
                    PatientId = l.PatientId,
                    PatientMrn = patient?.MedicalRecordNumber,
                    PatientName = patient is null ? null : $"{patient.LastName}, {patient.FirstName}"
                };
            })
            .OrderBy(l => l.ProductCode)
            .ThenBy(l => l.Abo)
            .ToList();
        if (order.BloodDeliveryId is long deliveryId && _units is not null)
        {
            var delivered = await _units.ListAsync(u => u.DeliveryId == deliveryId, ct);
            lineDtos = Reconcile(lineDtos, delivered);
        }

        var dueHours = _policy is null ? 24 : await _policy.GetExpectedArrivalDueHoursAsync(ct);
        return new SupplierOrderDto(
            order.Id, order.OrderNumber, order.BloodSupplierId, supplier?.Name ?? string.Empty, order.Status, order.Notes,
            order.CreatedUtc, order.CreatedBy, order.SubmittedBy, order.SubmittedUtc, order.BloodDeliveryId, order.ReceivedUtc,
            order.CancelReason, lineDtos.Sum(l => l.Quantity), lineDtos,
            order.Status == SupplierOrderStatus.Submitted ? order.PromisedArrivalUtc ?? SupplierOrderRule.ArrivalDueUtc(order.SubmittedUtc, dueHours) : null,
            SupplierOrderRule.IsArrivalOverdue(order.Status, order.SubmittedUtc, dueHours, _clock.UtcNow, order.PromisedArrivalUtc))
        {
            FollowUpNote = order.FollowUpNote,
            FollowUpBy = order.FollowUpBy,
            FollowUpUtc = order.FollowUpUtc,
            ArrivalPromised = order.PromisedArrivalUtc is not null
        };
    }
}
