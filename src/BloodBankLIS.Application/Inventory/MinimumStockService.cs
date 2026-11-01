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
    int Shortfall);

public sealed record SaveMinimumStockRequest(long ProductTypeId, AboGroup? Abo, int MinimumAvailable);

public sealed class MinimumStockService
{
    private readonly IRepository<MinimumStockLevel> _levels;
    private readonly IRepository<ProductType> _products;
    private readonly IInventoryRepository _inventory;
    private readonly IUnitOfWork _unitOfWork;

    public MinimumStockService(
        IRepository<MinimumStockLevel> levels,
        IRepository<ProductType> products,
        IInventoryRepository inventory,
        IUnitOfWork unitOfWork)
    {
        _levels = levels;
        _products = products;
        _inventory = inventory;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<MinimumStockRowDto>> ListAsync(CancellationToken ct = default)
    {
        var levels = await _levels.ListAsync(ct);
        var products = (await _products.ListAsync(ct)).ToDictionary(product => product.Id);
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
                MinimumStockRule.Shortfall(onHand, level.MinimumAvailable)));
        }

        return rows;
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

    private async Task<int> CountAvailableAsync(long productTypeId, AboGroup? abo, CancellationToken ct)
    {
        var units = await _inventory.SearchAsync(
            new InventorySearchCriteria(Status: UnitStatus.Available, Abo: abo, ProductTypeId: productTypeId), ct);
        return units.Count;
    }
}
