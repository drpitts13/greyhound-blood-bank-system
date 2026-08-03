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
        return rows.ToDictionary(
            r => r.Code,
            r => new AboRhdParser.LookupRow(
                r.Code, r.Abo, r.RhD, r.CollectionType, r.SpecialMessage, r.AdditionalPhenotype,
                r.EffectiveDate, r.RetiredDate, r.StandardVersion),
            StringComparer.Ordinal);
    }

    public async Task<IReadOnlyDictionary<string, ProductParser.LookupRow>> GetProductLookupAsync(CancellationToken ct = default)
    {
        var rows = await _products.ListAsync(_ => true, ct);
        return rows.ToDictionary(
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
}
