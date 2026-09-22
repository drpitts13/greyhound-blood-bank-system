namespace BloodBankLIS.Web.Services;

/// <summary>Canonical inventory unit routes for chart, desk, and deep links.</summary>
public static class InventoryUrls
{
    public static string Unit(long id, string? tab = null) =>
        string.IsNullOrWhiteSpace(tab)
            ? $"/inventory/units/{id}"
            : $"/inventory/units/{id}?tab={Uri.EscapeDataString(tab)}";
}
