using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Isbt128.Parsing;
using System.Text.Json;

namespace BloodBankLIS.Application.Isbt128;

/// <summary>
/// Loads versioned ISBT lookup tables for parsers.
/// PLACEHOLDER rows are seeded for demonstration — ICCBBA_VALIDATION_REQUIRED.
/// </summary>
public sealed class IsbtLookupCatalog
{
    private readonly IRepository<IsbtAboRhdCode> _abo;
    private readonly IRepository<IsbtProductCode> _products;

    public IsbtLookupCatalog(IRepository<IsbtAboRhdCode> abo, IRepository<IsbtProductCode> products)
    {
        _abo = abo;
        _products = products;
    }

    public async Task<IReadOnlyDictionary<string, AboRhdParser.LookupRow>> GetAboLookupAsync(CancellationToken ct = default)
    {
        var rows = await _abo.ListAsync(_ => true, ct);
        return PreferLicensed(rows, r => r.Code, r => r.IsPlaceholder, r => r.RetiredDate)
            .ToDictionary(
                r => r.Code,
                r => new AboRhdParser.LookupRow(
                    r.Code, r.Abo, r.RhD, r.CollectionType, r.SpecialMessage, r.AdditionalPhenotype,
                    r.EffectiveDate, r.RetiredDate, r.StandardVersion),
                StringComparer.Ordinal);
    }

    public async Task<IReadOnlyDictionary<string, ProductParser.LookupRow>> GetProductLookupAsync(CancellationToken ct = default)
    {
        var rows = await _products.ListAsync(_ => true, ct);
        return PreferLicensed(rows, r => r.ProductDescriptionCode, r => r.IsPlaceholder, r => r.RetiredDate)
            .ToDictionary(
                r => r.ProductDescriptionCode,
                r =>
                {
                    var attrs = Array.Empty<string>();
                    try { attrs = JsonSerializer.Deserialize<string[]>(r.AttributesJson) ?? Array.Empty<string>(); }
                    catch { /* keep empty */ }

                    return new ProductParser.LookupRow(
                        r.ProductDescriptionCode, r.Description, r.ComponentClass, r.Modifier, attrs,
                        r.StorageRequirements, r.RequiresExtendedDivision, r.EffectiveDate, r.RetiredDate, r.StandardVersion);
                },
                StringComparer.Ordinal);
    }

    /// <summary>
    /// When the same code exists under more than one standard version, prefer a
    /// licensed (non-placeholder) row, then a non-retired row.
    /// </summary>
    public static IReadOnlyList<T> PreferLicensed<T>(
        IEnumerable<T> rows,
        Func<T, string> key,
        Func<T, bool> isPlaceholder,
        Func<T, DateOnly?> retiredDate)
    {
        return rows
            .GroupBy(key, StringComparer.Ordinal)
            .Select(g => g
                .OrderBy(isPlaceholder)
                .ThenBy(r => retiredDate(r).HasValue)
                .First())
            .ToList();
    }
}
