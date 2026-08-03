using System.Text.RegularExpressions;

namespace BloodBankLIS.Domain.ValueObjects;

/// <summary>Unit of an <see cref="ExpirationOffsetCode"/> amount.</summary>
public enum ExpirationOffsetUnit
{
    Hours = 0,
    Days = 1
}

/// <summary>
/// A short admin-entered expiration offset such as "24H" or "5D", used by
/// <c>ModificationRule</c> to compute a result unit's new expiration relative to the
/// modification date/time (see <c>ModificationExpirationRule</c>).
/// </summary>
public readonly record struct ExpirationOffsetCode(int Amount, ExpirationOffsetUnit Unit)
{
    private static readonly Regex Pattern = new(@"^\s*(\d+)\s*([HhDd])\s*$", RegexOptions.Compiled);

    public TimeSpan ToTimeSpan() => Unit switch
    {
        ExpirationOffsetUnit.Hours => TimeSpan.FromHours(Amount),
        ExpirationOffsetUnit.Days => TimeSpan.FromDays(Amount),
        _ => throw new InvalidOperationException($"Unhandled {nameof(ExpirationOffsetUnit)}: {Unit}")
    };

    public static bool TryParse(string? code, out ExpirationOffsetCode result)
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

        var unit = match.Groups[2].Value is "H" or "h" ? ExpirationOffsetUnit.Hours : ExpirationOffsetUnit.Days;
        result = new ExpirationOffsetCode(amount, unit);
        return true;
    }

    public override string ToString() => Unit == ExpirationOffsetUnit.Hours ? $"{Amount}H" : $"{Amount}D";
}
