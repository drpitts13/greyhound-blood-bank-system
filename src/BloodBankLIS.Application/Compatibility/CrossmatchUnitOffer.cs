namespace BloodBankLIS.Application.Compatibility;

/// <summary>
/// The units the Record crossmatch picker offers once a patient is chosen: the units already reserved for that patient
/// (what is normally crossmatched), then the compatible units in stock (type, special requirements, antigens). When the
/// compatible list cannot be read, every unit stays offered, saying why. Recording still checks the chosen unit.
/// </summary>
public static class CrossmatchUnitOffer
{
    public sealed record Offer(IReadOnlySet<long>? Ids, string Note);

    public static Offer Build(IEnumerable<long> reserved, IEnumerable<long>? compatible, string? compatibleError = null)
    {
        var reservedIds = reserved.ToHashSet();
        if (compatible is null)
        {
            var why = (compatibleError ?? "the compatible list could not be loaded").Trim().TrimEnd('.');
            return new Offer(null, $"Showing every unit: {why}. Recording still checks the unit.");
        }

        var more = compatible.Where(id => !reservedIds.Contains(id)).ToHashSet();
        var ids = reservedIds.Union(more).ToHashSet();
        var note = $"{Count(reservedIds.Count, "unit", "units")} reserved for this patient"
            + (more.Count > 0 ? $", and {Count(more.Count, "more compatible unit", "more compatible units")} in stock." : ".");
        return new Offer(ids, note);
    }

    private static string Count(int n, string one, string many) => n == 1 ? $"1 {one}" : $"{n} {many}";
}
