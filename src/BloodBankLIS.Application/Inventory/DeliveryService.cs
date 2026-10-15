using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Inventory;

public sealed class DeliveryService
{
    private readonly IRepository<BloodDelivery> _deliveries;
    private readonly IRepository<BloodDeliveryLine> _lines;
    private readonly IRepository<BloodSupplier> _suppliers;
    private readonly IRepository<ProductType> _productTypes;
    private readonly IRepository<BloodUnit> _units;
    private readonly IDeliveryLineStore _lineStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IPermissionEvaluator? _permissions;

    public DeliveryService(
        IRepository<BloodDelivery> deliveries,
        IRepository<BloodDeliveryLine> lines,
        IRepository<BloodSupplier> suppliers,
        IRepository<ProductType> productTypes,
        IRepository<BloodUnit> units,
        IDeliveryLineStore lineStore,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IPermissionEvaluator? permissions = null)
    {
        _deliveries = deliveries;
        _lines = lines;
        _suppliers = suppliers;
        _productTypes = productTypes;
        _units = units;
        _lineStore = lineStore;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<OpenDeliveryDto>> ListOpenAsync(CancellationToken ct = default)
    {
        var open = await _deliveries.ListAsync(d => d.Status == DeliveryStatus.Open, ct);
        var results = new List<OpenDeliveryDto>();
        foreach (var delivery in open.OrderByDescending(d => d.ReceivedUtc).ThenByDescending(d => d.Id))
        {
            var detail = await BuildDetailAsync(delivery, ct);
            results.Add(new OpenDeliveryDto(
                detail.Id,
                detail.BloodSupplierId,
                detail.SupplierCode,
                detail.SupplierName,
                detail.PackingListNumber,
                detail.OrderedUtc,
                detail.ReceivedUtc,
                detail.Lines.Sum(l => l.ExpectedQuantity),
                detail.Lines.Sum(l => l.ReceivedQuantity)));
        }

        return results;
    }

    public async Task<DeliveryDetailDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var delivery = await _deliveries.GetByIdAsync(id, ct);
        return delivery is null ? null : await BuildDetailAsync(delivery, ct);
    }

    public async Task<EvaluationResult<DeliveryDetailDto>> CreateAsync(CreateDeliveryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync<DeliveryDetailDto>(ct);
        if (denied is not null)
        {
            return denied;
        }

        var receivedUtc = _clock.UtcNow;
        var lines = NormalizeLines(request.Lines);
        var supplier = request.BloodSupplierId > 0
            ? await _suppliers.GetByIdAsync(request.BloodSupplierId, ct)
            : null;
        var validation = BloodDeliveryValidator.Validate(
            request.OrderedUtc,
            receivedUtc,
            request.PackingListNumber,
            supplierMissing: supplier is null,
            supplierInactive: supplier is { IsActive: false },
            lines);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<DeliveryDetailDto>.Blocked(validation);
        }

        var delivery = new BloodDelivery
        {
            BloodSupplierId = supplier!.Id,
            PackingListNumber = NullIfEmpty(request.PackingListNumber),
            OrderedUtc = request.OrderedUtc,
            ReceivedUtc = receivedUtc,
            Status = DeliveryStatus.Open
        };
        await _deliveries.AddAsync(delivery, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        foreach (var line in lines)
        {
            await _lines.AddAsync(new BloodDeliveryLine
            {
                BloodDeliveryId = delivery.Id,
                ComponentClass = line.ComponentClass,
                ExpectedQuantity = line.ExpectedQuantity
            }, ct);
        }

        _audit.Record(
            AuditEventType.Create,
            nameof(BloodDelivery),
            delivery.Id,
            newValue: new { delivery.BloodSupplierId, delivery.PackingListNumber, delivery.OrderedUtc, delivery.ReceivedUtc },
            reason: "Delivery entered.");
        await _unitOfWork.SaveChangesAsync(ct);

        var detail = await BuildDetailAsync(delivery, ct);
        return EvaluationResult<DeliveryDetailDto>.Ok(detail, validation);
    }

    public async Task<EvaluationResult<DeliveryDetailDto>> UpdateAsync(long id, UpdateDeliveryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync<DeliveryDetailDto>(ct);
        if (denied is not null)
        {
            return denied;
        }

        var delivery = await _deliveries.GetByIdAsync(id, ct);
        if (delivery is null)
        {
            return EvaluationResult<DeliveryDetailDto>.Fail("Delivery not found.");
        }

        if (delivery.Status != DeliveryStatus.Open)
        {
            return EvaluationResult<DeliveryDetailDto>.Fail("A completed delivery cannot be changed.");
        }

        var lines = NormalizeLines(request.Lines);
        var supplier = request.BloodSupplierId > 0
            ? await _suppliers.GetByIdAsync(request.BloodSupplierId, ct)
            : null;
        var receivedByClass = await ReceivedCountsAsync(id, ct);
        var supplierLocked = receivedByClass.Values.Sum() > 0;
        if (supplierLocked && request.BloodSupplierId != delivery.BloodSupplierId)
        {
            return EvaluationResult<DeliveryDetailDto>.Fail("The supplier cannot change after units are received on the delivery.");
        }

        var validation = BloodDeliveryValidator.Validate(
            request.OrderedUtc,
            delivery.ReceivedUtc,
            request.PackingListNumber,
            supplierMissing: supplier is null,
            supplierInactive: supplier is { IsActive: false } && request.BloodSupplierId != delivery.BloodSupplierId,
            lines);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<DeliveryDetailDto>.Blocked(validation);
        }

        var existing = await _lineStore.ListTrackedAsync(id, ct);
        var requestedClasses = lines.Select(l => l.ComponentClass).ToHashSet();
        foreach (var line in existing.Where(l => !requestedClasses.Contains(l.ComponentClass)))
        {
            if (receivedByClass.GetValueOrDefault(line.ComponentClass) > 0)
            {
                return EvaluationResult<DeliveryDetailDto>.Fail(
                    "A product that already has units on the delivery cannot be removed.");
            }

            _lineStore.Remove(line);
        }

        foreach (var requested in lines)
        {
            var match = existing.FirstOrDefault(l => l.ComponentClass == requested.ComponentClass);
            if (match is null)
            {
                await _lines.AddAsync(new BloodDeliveryLine
                {
                    BloodDeliveryId = id,
                    ComponentClass = requested.ComponentClass,
                    ExpectedQuantity = requested.ExpectedQuantity
                }, ct);
            }
            else if (match.ExpectedQuantity != requested.ExpectedQuantity)
            {
                match.ExpectedQuantity = requested.ExpectedQuantity;
                _lines.Update(match);
            }
        }

        delivery.BloodSupplierId = supplier!.Id;
        delivery.PackingListNumber = NullIfEmpty(request.PackingListNumber);
        delivery.OrderedUtc = request.OrderedUtc;
        _deliveries.Update(delivery);
        _audit.Record(
            AuditEventType.Update,
            nameof(BloodDelivery),
            delivery.Id,
            newValue: new { delivery.BloodSupplierId, delivery.PackingListNumber, delivery.OrderedUtc },
            reason: "Delivery updated.");
        await _unitOfWork.SaveChangesAsync(ct);

        var detail = await BuildDetailAsync(delivery, ct);
        return EvaluationResult<DeliveryDetailDto>.Ok(detail, validation);
    }

    public async Task<EvaluationResult<DeliveryDetailDto>> CompleteAsync(long id, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync<DeliveryDetailDto>(ct);
        if (denied is not null)
        {
            return denied;
        }

        var delivery = await _deliveries.GetByIdAsync(id, ct);
        if (delivery is null)
        {
            return EvaluationResult<DeliveryDetailDto>.Fail("Delivery not found.");
        }

        if (delivery.Status == DeliveryStatus.Completed)
        {
            return EvaluationResult<DeliveryDetailDto>.Fail("Delivery is already completed.");
        }

        delivery.Status = DeliveryStatus.Completed;
        delivery.CompletedUtc = _clock.UtcNow;
        _deliveries.Update(delivery);
        _audit.Record(
            AuditEventType.Update,
            nameof(BloodDelivery),
            delivery.Id,
            newValue: new { delivery.Status, delivery.CompletedUtc },
            reason: "Delivery completed.");
        await _unitOfWork.SaveChangesAsync(ct);
        var detail = await BuildDetailAsync(delivery, ct);
        return EvaluationResult<DeliveryDetailDto>.Ok(detail);
    }

    public async Task<EvaluationResult<DeliveryReceiveContext>> ResolveForReceiveAsync(
        long? deliveryId,
        long productTypeId,
        CancellationToken ct = default)
    {
        if (deliveryId is null or <= 0)
        {
            return EvaluationResult<DeliveryReceiveContext>.Fail("A delivery is required to receive a unit.");
        }

        var delivery = await _deliveries.GetByIdAsync(deliveryId.Value, ct);
        if (delivery is null)
        {
            return EvaluationResult<DeliveryReceiveContext>.Fail("Delivery not found.");
        }

        if (delivery.Status != DeliveryStatus.Open)
        {
            return EvaluationResult<DeliveryReceiveContext>.Fail("Units can only be added to an open delivery.");
        }

        var product = await _productTypes.GetByIdAsync(productTypeId, ct);
        if (product is null)
        {
            return EvaluationResult<DeliveryReceiveContext>.Fail("Product type was not found.");
        }

        var onDelivery = await _lines.AnyAsync(
            l => l.BloodDeliveryId == delivery.Id && l.ComponentClass == product.ComponentClass, ct);
        if (!onDelivery)
        {
            return EvaluationResult<DeliveryReceiveContext>.Fail("That product is not on this delivery.");
        }

        var supplier = await _suppliers.GetByIdAsync(delivery.BloodSupplierId, ct);
        if (supplier is null)
        {
            return EvaluationResult<DeliveryReceiveContext>.Fail("The delivery's blood supplier was not found.");
        }

        return EvaluationResult<DeliveryReceiveContext>.Ok(
            new DeliveryReceiveContext(delivery.Id, supplier.Name, delivery.PackingListNumber));
    }

    private async Task<DeliveryDetailDto> BuildDetailAsync(BloodDelivery delivery, CancellationToken ct)
    {
        var supplier = delivery.BloodSupplier ?? await _suppliers.GetByIdAsync(delivery.BloodSupplierId, ct);
        var lines = await _lines.ListAsync(l => l.BloodDeliveryId == delivery.Id, ct);
        var received = await ReceivedCountsAsync(delivery.Id, ct);
        var units = await _units.ListAsync(u => u.DeliveryId == delivery.Id, ct);

        return new DeliveryDetailDto(
            delivery.Id,
            delivery.BloodSupplierId,
            supplier?.Code ?? string.Empty,
            supplier?.Name ?? string.Empty,
            delivery.PackingListNumber,
            delivery.OrderedUtc,
            delivery.ReceivedUtc,
            delivery.Status,
            SupplierLocked: received.Values.Sum() > 0,
            lines.OrderBy(l => l.ComponentClass).Select(l => new DeliveryLineDto(
                l.Id,
                l.ComponentClass,
                ComponentClassLabels.Label(l.ComponentClass),
                l.ExpectedQuantity,
                received.GetValueOrDefault(l.ComponentClass))).ToList(),
            units.OrderBy(u => u.UnitNumber).Select(BloodUnitDto.From).ToList());
    }

    private async Task<Dictionary<ComponentClass, int>> ReceivedCountsAsync(long deliveryId, CancellationToken ct)
    {
        var units = await _units.ListAsync(u => u.DeliveryId == deliveryId, ct);
        if (units.Count == 0)
        {
            return new Dictionary<ComponentClass, int>();
        }

        var productIds = units.Select(u => u.ProductTypeId).Distinct().ToList();
        var products = await _productTypes.ListAsync(p => productIds.Contains(p.Id), ct);
        var classByProduct = products.ToDictionary(p => p.Id, p => p.ComponentClass);
        return units
            .Where(u => classByProduct.ContainsKey(u.ProductTypeId))
            .GroupBy(u => classByProduct[u.ProductTypeId])
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private static List<(ComponentClass ComponentClass, int ExpectedQuantity)> NormalizeLines(
        IReadOnlyList<SaveDeliveryLineRequest>? lines) =>
        (lines ?? Array.Empty<SaveDeliveryLineRequest>())
            .Select(l => (l.ComponentClass, l.ExpectedQuantity))
            .ToList();

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<EvaluationResult<T>?> RejectUnauthorizedAsync<T>(CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(
            _currentUser.UserName, PermissionCodes.InventoryReceive, ct);
        var auth = InventoryAuthorizationRule.EvaluateReceive(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? EvaluationResult<T>.Blocked(new RuleEvaluation([auth]))
            : null;
    }
}
