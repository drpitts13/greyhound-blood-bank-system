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
}
