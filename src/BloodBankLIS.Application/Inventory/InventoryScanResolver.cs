using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Isbt128;

namespace BloodBankLIS.Application.Inventory;

/// <summary>
/// Turns one scan (a DIN barcode, a typed unit number, or a component label) into the units it names. Shared by Move
/// units, Stock count, and the cooler check. A divided number can name several units.
/// </summary>
public static class InventoryScanResolver
{
    public static async Task<(string Key, List<BloodUnit> Units, string? Error)> ResolveAsync(IInventoryRepository repository, string? scan, CancellationToken ct)
    {
        var sanitized = ScannerInputSanitizer.Sanitize(scan).Sanitized.Trim();
        if (sanitized.Length == 0)
        {
            return (string.Empty, [], "Scan the unit number.");
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
}
