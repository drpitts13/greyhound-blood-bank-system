using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Isbt128;

namespace BloodBankLIS.Application.Inventory;

/// <summary>
/// Turns one scan (a DIN barcode, a typed unit number, or a component label) into the units it names. Shared by Move
/// units, Stock count, and the cooler check. A divided number can name several units; the unit number followed by its
/// product code barcode (=&lt;…, scanned together or one after the other) names one division.
/// </summary>
public static class InventoryScanResolver
{
    private const string ProductCodeIdentifier = "=<";

    public static async Task<(string Key, List<BloodUnit> Units, string? Error)> ResolveAsync(IInventoryRepository repository, string? scan, CancellationToken ct)
    {
        var sanitized = ScannerInputSanitizer.Sanitize(scan).Sanitized.Trim();
        if (sanitized.Length == 0)
        {
            return (string.Empty, [], "Scan the unit number.");
        }

        // End-user testing, 2026-11-12: a divided unit (one DIN, several divisions) could not be counted or moved by
        // scan; its DIN named every division and the product code label that tells them apart was not read.
        var productAt = sanitized.IndexOf(ProductCodeIdentifier, StringComparison.Ordinal);
        if (productAt == 0)
        {
            return (sanitized, [], "That is the product code label. Scan the unit number (DIN) first; a product code scanned right after it picks the division.");
        }

        if (productAt > 0)
        {
            var productData = sanitized[(productAt + ProductCodeIdentifier.Length)..].Trim();
            if (productData.Length < 8)
            {
                return (sanitized, [], "That product code is not readable. Scan the product code barcode again.");
            }

            productData = productData[..8].ToUpperInvariant();
            var (dinKey, byDin, dinError) = await ResolveAsync(repository, sanitized[..productAt], ct);
            if (dinError is not null)
            {
                return (dinKey, [], dinError);
            }

            var matching = byDin.Where(u => HasProductCode(u, dinKey, productData)).ToList();
            return matching.Count > 0
                ? ($"{dinKey} {productData}", matching, null)
                : (dinKey, [], $"{dinKey} has no unit with product code {productData} (on record: {string.Join(", ", byDin.Select(u => u.UnitNumber))}).");
        }

        var din = Domain.Isbt128.Parsing.DinParser.Parse(sanitized);
        if (!din.Success && sanitized.StartsWith('='))
        {
            return (sanitized, [], "That scan is not a readable unit number (DIN). Scan the unit number barcode again.");
        }

        var key = (din.Success ? din.Value!.Din : sanitized.Replace(" ", string.Empty)).ToUpperInvariant();
        var units = (await repository.ListByUnitNumberOrDinAsync(key, ct)).ToList();
        if (units.Count == 0 && await repository.GetByComponentIdentityAsync(sanitized, ct) is { } byLabel)
        {
            units.Add(byLabel);
        }

        return units.Count == 0 ? (key, units, $"No unit has the number {sanitized}.") : (key, units, null);
    }

    /// <summary>
    /// A product code label scanned right after a unit number belongs to it: returns the two as one scan, or null when
    /// <paramref name="scan"/> is not a lone product code or the previous scan is missing or already has one.
    /// </summary>
    public static string? PairWithPrevious(string? previous, string? scan)
    {
        var current = ScannerInputSanitizer.Sanitize(scan).Sanitized.Trim();
        if (!current.StartsWith(ProductCodeIdentifier, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(previous))
        {
            return null;
        }

        var before = ScannerInputSanitizer.Sanitize(previous).Sanitized.Trim();
        return before.Length == 0 || before.Contains(ProductCodeIdentifier, StringComparison.Ordinal) ? null : before + current;
    }

    private static bool HasProductCode(BloodUnit unit, string din, string productData)
    {
        if (string.Equals(unit.ProductCodeData, productData, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var prefix = din + ComponentIdentityBuilder.Separator + productData;
        return (unit.ComponentIdentity ?? string.Empty).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || unit.UnitNumber.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
