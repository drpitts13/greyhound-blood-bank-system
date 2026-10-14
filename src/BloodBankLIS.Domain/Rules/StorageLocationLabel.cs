namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Staff-facing label for a unit's storage location. Prefer the location name.
/// A missing or blank name stays the location id so two places are not shown as the same.
/// </summary>
public static class StorageLocationLabel
{
    public const string None = "No location";

    public static string Format(long? locationId, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Trim();
        }

        return locationId is long id ? $"Location {id}" : None;
    }

    public static string Format(long? locationId, IReadOnlyDictionary<long, string?>? namesById)
    {
        string? name = null;
        if (locationId is long id && namesById is not null && namesById.TryGetValue(id, out var stored))
        {
            name = stored;
        }

        return Format(locationId, name);
    }

    /// <summary>
    /// Label for the main inventory search list. Prefer the catalog name.
    /// A location code is not a substitute when the name is blank or the row is missing.
    /// </summary>
    public readonly record struct CatalogLocation(long Id, string? Name, string? Code);

    public static string ForInventoryList(long? locationId, IEnumerable<CatalogLocation>? catalog)
    {
        if (locationId is not long id)
        {
            return Format(null, name: null);
        }

        string? name = null;
        if (catalog is not null)
        {
            foreach (var location in catalog)
            {
                if (location.Id != id)
                {
                    continue;
                }

                name = location.Name;
                break;
            }
        }

        return Format(id, name);
    }

    /// <summary>
    /// Label for the unit chart. Same rule as the inventory search list:
    /// the catalog name, never the location code as a fallback.
    /// </summary>
    public static string ForUnitChart(long? locationId, IEnumerable<CatalogLocation>? catalog) =>
        ForInventoryList(locationId, catalog);
}
