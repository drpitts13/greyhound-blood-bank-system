using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// Store tubes' Due for discard as a pull list (Cycle 615): tubes grouped by the place they are in, so each rack or shelf
/// is visited once. The place holding the oldest tube comes first; within a place, oldest first. Places typed two ways
/// ("Rack 3" and "rack-3") are one place, shown under its most common spelling.
/// </summary>
public static class TubePullList
{
    public const string NoPlace = "No place recorded";

    public sealed record Place(string Name, IReadOnlyList<TubeInPlaceDto> Tubes);

    public static IReadOnlyList<Place> Group(IEnumerable<TubeInPlaceDto> due)
    {
        ArgumentNullException.ThrowIfNull(due);
        return due
            .GroupBy(t => SpecimenStorageRule.Key(t.Location))
            .Select(g =>
            {
                var tubes = g.OrderBy(t => t.KeepUntilUtc ?? DateTime.MaxValue).ToList();
                var name = tubes
                    .Select(t => t.Location?.Trim())
                    .Where(n => !string.IsNullOrEmpty(n))
                    .GroupBy(n => n!, StringComparer.Ordinal)
                    .OrderByDescending(s => s.Count())
                    .ThenBy(s => s.Key, StringComparer.Ordinal)
                    .Select(s => s.Key)
                    .FirstOrDefault() ?? NoPlace;
                return new Place(name, tubes);
            })
            .OrderBy(p => p.Tubes[0].KeepUntilUtc ?? DateTime.MaxValue)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
