using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Putting a tube away: a place is required (scanned rack or typed shelf, up to 100 characters); a cancelled tube is not
/// stored; storing it where it already is changes nothing. How long tubes are kept is facility policy (OCD-070).
/// </summary>
public static class SpecimenStorageRule
{
    public const string Code = "SPECIMEN-STORE";
    public const int MaxLocationLength = 100;

    public static RuleResult Evaluate(SpecimenStatus status, string? currentLocation, string? location)
    {
        var place = location?.Trim() ?? string.Empty;
        if (place.Length == 0)
        {
            return RuleResult.HardStop(Code, "Scan or type where the tube is stored.");
        }

        if (place.Length > MaxLocationLength)
        {
            return RuleResult.HardStop(Code, $"A storage place is at most {MaxLocationLength} characters.");
        }

        if (status == SpecimenStatus.Cancelled)
        {
            return RuleResult.HardStop(Code, "A cancelled tube is not stored.");
        }

        return currentLocation is not null && SamePlace(currentLocation, place)
            ? RuleResult.HardStop(Code, $"The tube is already stored in {currentLocation.Trim()}.")
            : RuleResult.Pass(Code);
    }

    /// <summary>
    /// A place's key for matching: letters and digits only, upper case, so "Rack 3", "rack3", and "RACK-3" are one place.
    /// </summary>
    public static string Key(string? place) =>
        new((place ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    public static bool SamePlace(string? first, string? second)
    {
        var a = Key(first);
        return a.Length > 0 && a == Key(second);
    }

    /// <summary>The spelling already in use for this place, or the place as entered (trimmed) when it is new.</summary>
    public static string Canonical(string place, IEnumerable<string> knownPlaces) =>
        knownPlaces.FirstOrDefault(k => SamePlace(k, place))?.Trim() ?? place.Trim();
}
