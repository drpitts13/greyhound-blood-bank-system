using System.Globalization;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Infrastructure.Reporting;

internal static class ReportValueFormat
{
    public static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string? Date(DateTime? value)
    {
        if (value is null)
            return null;

        var utc = value.Value.Kind == DateTimeKind.Local
            ? value.Value.ToUniversalTime()
            : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
        return utc.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    public static string Name(string? last, string? first)
    {
        if (string.IsNullOrWhiteSpace(last))
            return first?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(first))
            return last.Trim();
        return last.Trim() + ", " + first.Trim();
    }

    public static string YesNo(bool value) => value ? "Yes" : "No";

    public static Dictionary<string, string?> Row(params (string Key, string? Value)[] cells)
    {
        var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in cells)
            row[key] = value;
        return row;
    }
}

internal static class ReportInventoryScope
{
    public static readonly UnitStatus[] OnHand =
    [
        UnitStatus.Available,
        UnitStatus.Quarantine,
        UnitStatus.Allocated,
        UnitStatus.Returned,
        UnitStatus.Expired,
        UnitStatus.Received,
        UnitStatus.Selected,
        UnitStatus.Assigned,
        UnitStatus.Crossmatched,
        UnitStatus.OnHold,
        UnitStatus.Damaged
    ];
}
