using BloodBankLIS.Domain.Enums;

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

    /// <summary>
    /// A unit that has left the blood bank (issued, transfused, shipped, discarded, missing, used in a modification)
    /// keeps the location it was last stored in; shown bare, it read as where the bag is (end-user testing pass 126:
    /// a unit in transit to the trauma bay showed "Main Blood Bank Refrigerator"). Such a unit reads "Not in storage
    /// (issued) · last stored at …"; a unit in the bank reads its location as before.
    /// </summary>
    public static string WithStatus(UnitStatus status, string storedLabel) =>
        LeftStorage(status)
            ? $"Not in storage ({StatusWord(status)}) · last stored at {storedLabel}"
            : storedLabel;

    public static bool LeftStorage(UnitStatus status) => status is UnitStatus.Issued or UnitStatus.TransfusionStarted
        or UnitStatus.TransfusionStopped or UnitStatus.Transfused or UnitStatus.Transferred or UnitStatus.ShippedOut
        or UnitStatus.ReturnedToSupplier or UnitStatus.Discarded or UnitStatus.Missing or UnitStatus.Modified;

    private static string StatusWord(UnitStatus status) => status switch
    {
        UnitStatus.TransfusionStarted => "transfusing",
        UnitStatus.TransfusionStopped => "transfusion stopped",
        UnitStatus.ShippedOut => "shipped out",
        UnitStatus.ReturnedToSupplier => "returned to supplier",
        UnitStatus.Modified => "modified into new units",
        _ => status.ToString().ToLowerInvariant()
    };

    /// <summary>
    /// Choice label for a transfer or delivery location.
    /// The code stays so staff can select the site. A blank name stays the location id
    /// and is not replaced by the code.
    /// </summary>
    public static string ForPicker(long locationId, string? code, string? name)
    {
        var place = Format(locationId, name);
        if (string.IsNullOrWhiteSpace(code))
        {
            return place;
        }

        return $"{code.Trim()} — {place}";
    }
}
