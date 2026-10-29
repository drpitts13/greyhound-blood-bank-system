using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Inventory;

public sealed record StockParBoardRowDto(
    long ParId,
    long ProductTypeId,
    string ProductCode,
    AboGroup? Abo,
    int OnHand,
    int MinimumAvailable,
    int Shortfall);

public sealed record SaveStockParRequest(long ProductTypeId, AboGroup? Abo, int MinimumAvailable);

public sealed class StockParService
{
    private readonly IRepository<StockParLevel> _pars;
    private readonly IRepository<ProductType> _products;
    private readonly IInventoryRepository _inventory;
    private readonly IUnitOfWork _unitOfWork;

    public StockParService(
        IRepository<StockParLevel> pars,
        IRepository<ProductType> products,
        IInventoryRepository inventory,
        IUnitOfWork unitOfWork)
    {
        _pars = pars;
        _products = products;
        _inventory = inventory;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<StockParBoardRowDto>> ListAsync(CancellationToken ct = default)
    {
        var pars = await _pars.ListAsync(ct);
        var products = (await _products.ListAsync(ct)).ToDictionary(product => product.Id);
        var rows = new List<StockParBoardRowDto>();
        foreach (var par in pars.OrderBy(row => row.ProductTypeId).ThenBy(row => row.Abo))
        {
            products.TryGetValue(par.ProductTypeId, out var product);
            var onHand = await CountAvailableAsync(par.ProductTypeId, par.Abo, ct);
            rows.Add(new StockParBoardRowDto(
                par.Id,
                par.ProductTypeId,
                product?.ProductCode ?? string.Empty,
                par.Abo,
                onHand,
                par.MinimumAvailable,
                StockParRule.Shortfall(onHand, par.MinimumAvailable)));
        }

        return rows;
    }

    public async Task<OperationResult<StockParBoardRowDto>> SaveAsync(SaveStockParRequest request, CancellationToken ct = default)
    {
        if (request.ProductTypeId <= 0 || request.MinimumAvailable < 0)
        {
            return OperationResult<StockParBoardRowDto>.Fail("Product and a minimum of zero or more are required.");
        }

        var product = await _products.GetByIdAsync(request.ProductTypeId, ct);
        if (product is null)
        {
            return OperationResult<StockParBoardRowDto>.Fail("Product not found.");
        }

        var existing = await _pars.FirstOrDefaultAsync(
            row => row.ProductTypeId == request.ProductTypeId && row.Abo == request.Abo, ct);
        var par = existing ?? new StockParLevel { ProductTypeId = request.ProductTypeId, Abo = request.Abo };
        par.MinimumAvailable = request.MinimumAvailable;
        if (existing is null)
        {
            await _pars.AddAsync(par, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        var onHand = await CountAvailableAsync(par.ProductTypeId, par.Abo, ct);
        return OperationResult<StockParBoardRowDto>.Ok(new StockParBoardRowDto(
            par.Id,
            par.ProductTypeId,
            product.ProductCode,
            par.Abo,
            onHand,
            par.MinimumAvailable,
            StockParRule.Shortfall(onHand, par.MinimumAvailable)));
    }

    private async Task<int> CountAvailableAsync(long productTypeId, AboGroup? abo, CancellationToken ct)
    {
        var units = await _inventory.SearchAsync(
            new InventorySearchCriteria(Status: UnitStatus.Available, Abo: abo, ProductTypeId: productTypeId), ct);
        return units.Count;
    }
}
