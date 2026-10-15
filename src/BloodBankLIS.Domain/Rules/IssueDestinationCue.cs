namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Place phrase for an issue on the unit timeline.
/// A blank place is omitted. A value that is exactly one storage-location code
/// uses that location's name. A blank name stays the location id. A code is not
/// shown in place of a missing name. Any other text stays as typed.
/// </summary>
public static class IssueDestinationCue
{
    public static string? Format(string? issuedToLocation, IReadOnlyList<StorageLocationLabel.CatalogLocation>? catalog)
    {
        if (string.IsNullOrWhiteSpace(issuedToLocation))
        {
            return null;
        }

        var typed = issuedToLocation.Trim();
        if (catalog is null || catalog.Count == 0)
        {
            return typed;
        }

        var codeMatches = catalog
            .Where(location => !string.IsNullOrWhiteSpace(location.Code)
                && string.Equals(location.Code.Trim(), typed, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (codeMatches.Count != 1)
        {
            return typed;
        }

        var match = codeMatches[0];
        var alsoAName = catalog.Any(location =>
            location.Id != match.Id
            && !string.IsNullOrWhiteSpace(location.Name)
            && string.Equals(location.Name.Trim(), typed, StringComparison.OrdinalIgnoreCase));
        if (alsoAName)
        {
            return typed;
        }

        return StorageLocationLabel.Format(match.Id, match.Name);
    }
}
