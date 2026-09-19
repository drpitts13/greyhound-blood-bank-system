using System.Text.RegularExpressions;

namespace BloodBankLIS.Domain.ValueObjects;

/// <summary>Unit of a <see cref="SpecimenExpirationCode"/> amount relative to collection.</summary>
public enum SpecimenExpirationUnit
{
    Hours = 0,
    Days = 1,
    Weeks = 2,
    Months = 3
}

/// <summary>
/// Admin-entered specimen expiration offset such as "72H", "3D", "2W", or "3M",
/// applied from the collection date/time.
/// </summary>
public readonly record struct SpecimenExpirationCode(int Amount, SpecimenExpirationUnit Unit)
{
    private static readonly Regex Pattern = new(@"^\s*(\d+)\s*([HhDdWwMm])\s*$", RegexOptions.Compiled);

    public static bool TryParse(string? code, out SpecimenExpirationCode result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var match = Pattern.Match(code);
        if (!match.Success)
        {
            return false;
        }

        if (!int.TryParse(match.Groups[1].Value, out var amount) || amount <= 0)
        {
            return false;
        }

        var unit = match.Groups[2].Value.ToUpperInvariant() switch
        {
            "H" => SpecimenExpirationUnit.Hours,
            "D" => SpecimenExpirationUnit.Days,
            "W" => SpecimenExpirationUnit.Weeks,
            "M" => SpecimenExpirationUnit.Months,
            _ => (SpecimenExpirationUnit?)null
        };
        if (unit is null)
        {
            return false;
        }

        result = new SpecimenExpirationCode(amount, unit.Value);
        return true;
    }

    public override string ToString() => Unit switch
    {
        SpecimenExpirationUnit.Hours => $"{Amount}H",
        SpecimenExpirationUnit.Days => $"{Amount}D",
        SpecimenExpirationUnit.Weeks => $"{Amount}W",
        SpecimenExpirationUnit.Months => $"{Amount}M",
        _ => throw new InvalidOperationException($"Unhandled {nameof(SpecimenExpirationUnit)}: {Unit}")
    };
}
