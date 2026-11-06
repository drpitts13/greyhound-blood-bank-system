using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Inventory;

public sealed record MinimumStockRowDto(
    long MinimumStockId,
    long ProductTypeId,
    string ProductCode,
    AboGroup? Abo,
    int OnHand,
    int MinimumAvailable,
    int Shortfall,
    // Open product order lines for this product (any ABO) still waiting to be filled.
    int OpenOrders = 0,
    // Units on submitted supplier orders not yet received, for this product and ABO (all ABO for an any-ABO level).
    int OnOrder = 0)
{
    public bool OpenOrdersExceedOnHand => MinimumStockRule.OpenOrdersExceedOnHand(OnHand, OpenOrders);

    public int StillToOrder => MinimumStockRule.StillToOrder(Shortfall, OnOrder);
}

public sealed record SaveMinimumStockRequest(long ProductTypeId, AboGroup? Abo, int MinimumAvailable);

public sealed class MinimumStockService
{
    private readonly IRepository<MinimumStockLevel> _levels;
    private readonly IRepository<ProductType> _products;
    private readonly IInventoryRepository _inventory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepository<OrderLine>? _orderLines;
    private readonly IRepository<Order>? _orders;
    private readonly IRepository<SupplierOrder>? _supplierOrders;
    private readonly IRepository<SupplierOrderLine>? _supplierOrderLines;

    public MinimumStockService(
        IRepository<MinimumStockLevel> levels,
        IRepository<ProductType> products,
        IInventoryRepository inventory,
        IUnitOfWork unitOfWork,
        IRepository<OrderLine>? orderLines = null,
        IRepository<Order>? orders = null,
        IRepository<SupplierOrder>? supplierOrders = null,
        IRepository<SupplierOrderLine>? supplierOrderLines = null)
    {
        _supplierOrders = supplierOrders;
        _supplierOrderLines = supplierOrderLines;
        _orderLines = orderLines;
        _orders = orders;
        _levels = levels;
        _products = products;
        _inventory = inventory;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<MinimumStockRowDto>> ListAsync(CancellationToken ct = default)
    {
        var levels = await _levels.ListAsync(ct);
        var products = (await _products.ListAsync(ct)).ToDictionary(product => product.Id);
        var openOrders = await OpenOrderLinesByProductAsync(ct);
        var onOrder = await SubmittedSupplierLinesAsync(ct);
        var rows = new List<MinimumStockRowDto>();
        foreach (var level in levels.OrderBy(row => row.ProductTypeId).ThenBy(row => row.Abo))
        {
            products.TryGetValue(level.ProductTypeId, out var product);
            var onHand = await CountAvailableAsync(level.ProductTypeId, level.Abo, ct);
            rows.Add(new MinimumStockRowDto(
                level.Id,
                level.ProductTypeId,
                product?.ProductCode ?? string.Empty,
                level.Abo,
                onHand,
                level.MinimumAvailable,
                MinimumStockRule.Shortfall(onHand, level.MinimumAvailable),
                openOrders.GetValueOrDefault(level.ProductTypeId),
                onOrder.Where(l => l.ProductTypeId == level.ProductTypeId && (level.Abo is null || l.Abo == level.Abo)).Sum(l => l.Quantity)));
        }

        return rows;
    }

    /// <summary>Lines of supplier orders that were submitted (sent) and not yet received or cancelled.</summary>
    private async Task<IReadOnlyList<SupplierOrderLine>> SubmittedSupplierLinesAsync(CancellationToken ct)
    {
        if (_supplierOrders is null || _supplierOrderLines is null)
        {
            return [];
        }

        var submitted = (await _supplierOrders.ListAsync(o => o.Status == SupplierOrderStatus.Submitted, ct)).Select(o => o.Id).ToList();
        return submitted.Count == 0
            ? []
            : await _supplierOrderLines.ListAsync(l => submitted.Contains(l.SupplierOrderId) && l.Quantity > 0, ct);
    }

    public async Task<OperationResult<MinimumStockRowDto>> SaveAsync(SaveMinimumStockRequest request, CancellationToken ct = default)
    {
        if (request.ProductTypeId <= 0 || request.MinimumAvailable < 0)
        {
            return OperationResult<MinimumStockRowDto>.Fail("Product and a minimum of zero or more are required.");
        }

        var product = await _products.GetByIdAsync(request.ProductTypeId, ct);
        if (product is null)
        {
            return OperationResult<MinimumStockRowDto>.Fail("Product not found.");
        }

        var existing = await _levels.FirstOrDefaultAsync(
            row => row.ProductTypeId == request.ProductTypeId && row.Abo == request.Abo, ct);
        var level = existing ?? new MinimumStockLevel { ProductTypeId = request.ProductTypeId, Abo = request.Abo };
        level.MinimumAvailable = request.MinimumAvailable;
        if (existing is null)
        {
            await _levels.AddAsync(level, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        var onHand = await CountAvailableAsync(level.ProductTypeId, level.Abo, ct);
        return OperationResult<MinimumStockRowDto>.Ok(new MinimumStockRowDto(
            level.Id,
            level.ProductTypeId,
            product.ProductCode,
            level.Abo,
            onHand,
            level.MinimumAvailable,
            MinimumStockRule.Shortfall(onHand, level.MinimumAvailable)));
    }

    /// <summary>Active product order lines still Ordered or partly filled, on orders that are not closed, by product.</summary>
    private async Task<Dictionary<long, int>> OpenOrderLinesByProductAsync(CancellationToken ct)
    {
        if (_orderLines is null || _orders is null)
        {
            return [];
        }

        var lines = await _orderLines.ListAsync(l =>
            l.IsActive
            && l.LineCategory == OrderCategory.Product
            && l.ProductTypeId != null
            && (l.FulfillmentStatus == FulfillmentStatus.Ordered || l.FulfillmentStatus == FulfillmentStatus.PartiallyFulfilled), ct);
        if (lines.Count == 0)
        {
            return [];
        }

        var orderIds = lines.Select(l => l.OrderId).Distinct().ToList();
        var open = (await _orders.ListAsync(o => orderIds.Contains(o.Id)
                && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Discontinued, ct))
            .Select(o => o.Id)
            .ToHashSet();
        return lines.Where(l => open.Contains(l.OrderId))
            .GroupBy(l => l.ProductTypeId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private async Task<int> CountAvailableAsync(long productTypeId, AboGroup? abo, CancellationToken ct)
    {
        var units = await _inventory.SearchAsync(
            new InventorySearchCriteria(Status: UnitStatus.Available, Abo: abo, ProductTypeId: productTypeId), ct);
        return units.Count;
    }
}
