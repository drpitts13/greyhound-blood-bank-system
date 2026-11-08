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

        return string.Equals(currentLocation?.Trim(), place, StringComparison.OrdinalIgnoreCase)
            ? RuleResult.HardStop(Code, $"The tube is already stored in {currentLocation!.Trim()}.")
            : RuleResult.Pass(Code);
    }
}
