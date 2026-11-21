using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Domain.Isbt128.Parsing;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// The Retype board's scan (Cycle 609): a scanned DIN barcode or a typed unit number picks the pending unit to retype,
/// so a box of new units is worked by scanning each label instead of finding its row. The scan is read as Move units and
/// Stock count read it (scanner characters removed, an ISBT DIN taken from its barcode).
/// </summary>
public static class RetypeBoardScan
{
    public sealed record Outcome(ProductRetypeWorkItemDto? Row, string? Message);

    public static Outcome Find(string? scan, IReadOnlyList<ProductRetypeWorkItemDto> pending)
    {
        var sanitized = ScannerInputSanitizer.Sanitize(scan).Sanitized.Trim();
        if (sanitized.Length == 0)
        {
            return new(null, null);
        }

        var din = DinParser.Parse(sanitized);
        if (!din.Success && sanitized.StartsWith('='))
        {
            return new(null, "That scan is not a readable unit number (DIN). Scan the unit number barcode again.");
        }

        var typed = sanitized.Replace(" ", string.Empty).ToUpperInvariant();
        var key = din.Success ? din.Value!.Din.ToUpperInvariant() : typed;
        // In this order, the first that finds any: the unit number exactly as typed, the DIN read from a barcode, then
        // that DIN with its check character (a typed 14-character number also parses as a DIN, so exact wins).
        string Number(ProductRetypeWorkItemDto r) => r.UnitNumber.Trim().ToUpperInvariant();
        var matches = pending.Where(r => Number(r) == typed).ToList();
        if (matches.Count == 0)
        {
            matches = pending.Where(r => Number(r) == key).ToList();
        }

        if (matches.Count == 0)
        {
            matches = pending.Where(r => Number(r).Length == key.Length + 1 && Number(r).StartsWith(key, StringComparison.Ordinal)).ToList();
        }
        // Say what was scanned: the DIN from a barcode, or the number as typed.
        var shown = sanitized.StartsWith('=') ? key : typed;
        return matches.Count switch
        {
            1 => new(matches[0], null),
            0 => new(null, $"{shown} is not waiting for a retype. Open it from Search to see its status."),
            _ => new(null, $"{shown} names {matches.Count} pending units (a divided number). Choose its row below.")
        };
    }
}
