using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;
using static BloodBankLIS.Domain.Rules.IsbtReadinessRule;

namespace BloodBankLIS.Application.Admin;

public sealed record IsbtCatalogCountDto(int Total, int Placeholder, int Licensed);

/// <summary>One thing in use that is not backed by a licensed code.</summary>
public sealed record IsbtReadinessItemDto(string Kind, long Id, string Label, string? Code, string Problem, string Href);

public sealed record IsbtReadinessDto(
    bool Ready,
    string Summary,
    IReadOnlyList<string> LicensedVersions,
    IsbtCatalogCountDto ProductCodes,
    IsbtCatalogCountDto AboRhdCodes,
    IsbtCatalogCountDto CollectionTypes,
    int ProductTypesChecked,
    int UnitsChecked,
    int UnitsWithoutIsbtCodes,
    int PlaceholderInUse,
    int UnknownInUse,
    IReadOnlyList<IsbtReadinessItemDto> Problems,
    bool ProblemsTruncated);

/// <summary>
/// OCD-004 readiness report: which ISBT codes the catalog holds, which are licensed, and which product types and
/// on-hand units still rely on placeholder or unknown codes. Read-only; it never changes a code.
/// </summary>
public sealed class IsbtReadinessService
{
    public const int MaxProblems = 200;

    private readonly IRepository<IsbtProductCode> _productCodes;
    private readonly IRepository<IsbtAboRhdCode> _aboCodes;
    private readonly IRepository<IsbtCollectionType> _collectionTypes;
    private readonly IRepository<ProductType> _productTypes;
    private readonly IRepository<BloodUnit> _units;

    public IsbtReadinessService(
        IRepository<IsbtProductCode> productCodes,
        IRepository<IsbtAboRhdCode> aboCodes,
        IRepository<IsbtCollectionType> collectionTypes,
        IRepository<ProductType> productTypes,
        IRepository<BloodUnit> units)
    {
        _productCodes = productCodes;
        _aboCodes = aboCodes;
        _collectionTypes = collectionTypes;
        _productTypes = productTypes;
        _units = units;
    }

    public async Task<IsbtReadinessDto> GetAsync(CancellationToken ct = default)
    {
        var productCodes = await _productCodes.ListAsync(ct);
        var aboCodes = await _aboCodes.ListAsync(ct);
        var collectionTypes = await _collectionTypes.ListAsync(ct);

        var productCatalog = Catalog(productCodes.Select(c => (c.ProductDescriptionCode, c.IsPlaceholder)));
        var aboCatalog = Catalog(aboCodes.Select(c => (c.Code, c.IsPlaceholder)));
        var licensedVersions = productCodes.Where(c => !c.IsPlaceholder).Select(c => c.StandardVersion)
            .Concat(aboCodes.Where(c => !c.IsPlaceholder).Select(c => c.StandardVersion))
            .Concat(collectionTypes.Where(c => !c.IsPlaceholder).Select(c => c.StandardVersion))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var problems = new List<IsbtReadinessItemDto>();
        var placeholder = 0;
        var unknown = 0;

        void Note(CodeState state, string kind, long id, string label, string? code, string field, string href)
        {
            switch (state)
            {
                case CodeState.Placeholder:
                    placeholder++;
                    problems.Add(new(kind, id, label, code, $"{field} {code} is a placeholder catalog row", href));
                    break;
                case CodeState.Unknown:
                    unknown++;
                    problems.Add(new(kind, id, label, code, $"{field} {code} is not in the catalog", href));
                    break;
            }
        }

        var productTypes = (await _productTypes.ListAsync(p => p.IsActive, ct)).OrderBy(p => p.ProductCode).ToList();
        foreach (var p in productTypes)
        {
            var state = Classify(p.Isbt128ProductCode, productCatalog);
            if (state == CodeState.None)
            {
                problems.Add(new("Product", p.Id, $"{p.ProductCode} {p.Name}", null, "No ISBT product code mapped", "/admin/products"));
                unknown++;
                continue;
            }

            Note(state, "Product", p.Id, $"{p.ProductCode} {p.Name}", p.Isbt128ProductCode, "ISBT product code", "/admin/products");
        }

        var onHand = InventoryUnitScopes.OnHand;
        var units = (await _units.ListAsync(u => onHand.Contains(u.Status), ct)).OrderBy(u => u.UnitNumber).ToList();
        var withoutCodes = 0;
        foreach (var u in units)
        {
            var product = ProductDescriptionCode(u.ProductDescriptionCode, u.ProductCodeData);
            var productState = Classify(product, productCatalog);
            var aboState = Classify(u.AboRhdCode, aboCatalog);
            if (productState == CodeState.None && aboState == CodeState.None)
            {
                withoutCodes++;
                continue;
            }

            var href = $"/inventory/units/{u.Id}";
            Note(productState, "Unit", u.Id, u.UnitNumber, product, "Product code", href);
            Note(aboState, "Unit", u.Id, u.UnitNumber, u.AboRhdCode, "ABO/RhD code", href);
        }

        return new IsbtReadinessDto(
            IsReady(licensedVersions.Count, placeholder, unknown),
            Summary(licensedVersions.Count, placeholder, unknown),
            licensedVersions,
            Count(productCodes.Select(c => c.IsPlaceholder)),
            Count(aboCodes.Select(c => c.IsPlaceholder)),
            Count(collectionTypes.Select(c => c.IsPlaceholder)),
            productTypes.Count,
            units.Count,
            withoutCodes,
            placeholder,
            unknown,
            problems.Take(MaxProblems).ToList(),
            problems.Count > MaxProblems);
    }

    private static Dictionary<string, bool> Catalog(IEnumerable<(string Code, bool Placeholder)> rows)
    {
        var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var (code, isPlaceholder) in rows)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            // A licensed row wins over a placeholder row for the same code.
            map[code.Trim()] = map.TryGetValue(code.Trim(), out var existing) ? existing && isPlaceholder : isPlaceholder;
        }

        return map;
    }

    private static IsbtCatalogCountDto Count(IEnumerable<bool> placeholders)
    {
        var list = placeholders.ToList();
        var placeholder = list.Count(p => p);
        return new IsbtCatalogCountDto(list.Count, placeholder, list.Count - placeholder);
    }
}
