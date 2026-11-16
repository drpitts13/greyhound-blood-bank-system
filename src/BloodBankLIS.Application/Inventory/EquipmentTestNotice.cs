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

    /// <summary>
    /// What is recorded as used for a manual result of a test (end-user testing pass 131: an out-of-service heat block was
    /// recorded as used for every new crossmatch, a false trace). Equipment out of service is left out; for each kind of
    /// equipment named for the test with none in service, a warning asks for what was actually used.
    /// </summary>
    public static (IReadOnlyList<EquipmentDto> Used, IReadOnlyList<string> NoneInService) ForResult(IEnumerable<EquipmentDto> equipment, string testCode)
    {
        var code = testCode.Trim().ToUpperInvariant();
        var named = equipment.Where(e => e.IsActive && Codes(e.TestCodes).Contains(code)).ToList();
        var used = named.Where(e => e.InService).ToList();
        var none = named.Where(e => !e.InService)
            .GroupBy(e => string.IsNullOrWhiteSpace(e.Kind) ? e.Code : e.Kind.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => !used.Any(u => string.Equals(string.IsNullOrWhiteSpace(u.Kind) ? u.Code : u.Kind.Trim(), g.Key, StringComparison.OrdinalIgnoreCase)))
            .Select(g => $"No {g.Key.ToLowerInvariant()} for {code} is in service ({string.Join(", ", g.Select(e => $"{e.Code} is out of service"))}): note in the result comment which {g.Key.ToLowerInvariant()} was used.")
            .ToList();
        return (used, none);
    }

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

    /// <summary>
    /// The checks needing action on the device named for a modification (end-user testing pass 81: a cell washer whose
    /// daily check was overdue said nothing when a unit was washed with it). Null when the code is not registered, the
    /// equipment is out of service (refused when saved) or nothing is due. Information only.
    /// </summary>
    public static string? ForDevice(IEnumerable<EquipmentDto> equipment, string? deviceCode)
    {
        if (string.IsNullOrWhiteSpace(deviceCode))
        {
            return null;
        }

        var code = deviceCode.Trim().ToUpperInvariant();
        var device = equipment.FirstOrDefault(e => e.InService && e.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        var due = device?.Checks.Where(c => c.IsActive && c.NeedsAction).ToList();
        return due is { Count: > 0 } ? string.Join("; ", due.Select(c => $"{c.Name}: {c.StatusText}")) : null;
    }
}
