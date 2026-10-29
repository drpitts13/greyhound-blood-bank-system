using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Compatibility;

/// <summary>
/// Loads a product ABO/Rh table. Products with no rows are omitted so callers fall back
/// to the built-in component-class rule.
/// </summary>
public static class ProductAboRhPolicyLoader
{
    public static ProductAboRhPolicy From(
        IEnumerable<ProductAboCompatibility> abo,
        IEnumerable<ProductRhCompatibility> rh) =>
        new(
            abo.Select(a => new ProductAboRhPolicy.AboCell(a.PatientAbo, a.UnitAbo, a.IsAllowed)),
            rh.Select(r => new ProductAboRhPolicy.RhCell(r.SexGroup, r.PatientRh, r.UnitRh, r.IsAllowed)));

    public static async Task<ProductAboRhPolicy?> ForProductAsync(
        IRepository<ProductAboCompatibility>? abo,
        IRepository<ProductRhCompatibility>? rh,
        long productTypeId,
        CancellationToken ct)
    {
        var map = await ForProductsAsync(abo, rh, [productTypeId], ct);
        return map.TryGetValue(productTypeId, out var policy) ? policy : null;
    }

    public static async Task<IReadOnlyDictionary<long, ProductAboRhPolicy>> ForProductsAsync(
        IRepository<ProductAboCompatibility>? aboRepo,
        IRepository<ProductRhCompatibility>? rhRepo,
        IReadOnlyCollection<long> productTypeIds,
        CancellationToken ct)
    {
        if (aboRepo is null || rhRepo is null || productTypeIds.Count == 0)
        {
            return new Dictionary<long, ProductAboRhPolicy>();
        }

        var ids = productTypeIds.Distinct().ToArray();
        var abo = await aboRepo.ListAsync(a => ids.Contains(a.ProductTypeId), ct);
        var rh = await rhRepo.ListAsync(r => ids.Contains(r.ProductTypeId), ct);
        var result = new Dictionary<long, ProductAboRhPolicy>();
        foreach (var id in ids)
        {
            var aboRows = abo.Where(a => a.ProductTypeId == id).ToList();
            var rhRows = rh.Where(r => r.ProductTypeId == id).ToList();
            if (aboRows.Count == 0 && rhRows.Count == 0)
            {
                continue;
            }

            result[id] = From(aboRows, rhRows);
        }

        return result;
    }
}
