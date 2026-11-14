namespace BloodBankLIS.Application.Inventory;

/// <summary>
/// Equipment used for the tests on the worklist that is out of service or has a check needing action (end-user testing
/// pass 74: an out-of-service heat block and an overdue centrifuge said nothing at result entry). Information only:
/// whether results should be blocked is the facility's procedure; nothing here blocks.
/// </summary>
public static class EquipmentTestNotice
{
    public sealed record Line(string EquipmentCode, string EquipmentName, IReadOnlyList<string> Tests, string Problem);

    /// <summary>Codes upper-cased, trimmed, without duplicates, comma-separated; null when none.</summary>
    public static string? Normalize(string? codes)
    {
        var list = Codes(codes);
        return list.Count == 0 ? null : string.Join(", ", list);
    }

    public static IReadOnlyList<string> Codes(string? codes) =>
        (codes ?? string.Empty).Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(c => c.ToUpperInvariant())
            .Distinct()
            .ToList();

    public static IReadOnlyList<Line> For(IEnumerable<EquipmentDto> equipment, IEnumerable<string> worklistTestCodes)
    {
        var onList = new HashSet<string>(worklistTestCodes.Select(c => c.Trim().ToUpperInvariant()));
        var lines = new List<Line>();
        foreach (var e in equipment.Where(e => e.IsActive))
        {
            var tests = Codes(e.TestCodes).Where(onList.Contains).ToList();
            if (tests.Count == 0)
            {
                continue;
            }

            if (e.OutOfServiceUtc is not null)
            {
                lines.Add(new Line(e.Code, e.Name, tests, $"out of service{(string.IsNullOrWhiteSpace(e.OutOfServiceReason) ? "" : $": {e.OutOfServiceReason.Trim()}")}"));
            }
            else if (e.Checks.Where(c => c.IsActive && c.NeedsAction).ToList() is { Count: > 0 } due)
            {
                lines.Add(new Line(e.Code, e.Name, tests, string.Join("; ", due.Select(c => $"{c.Name}: {c.StatusText}"))));
            }
        }

        return lines.OrderBy(l => l.EquipmentCode).ToList();
    }
}
