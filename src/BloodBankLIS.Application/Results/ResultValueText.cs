using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Results;

/// <summary>
/// A stored result value as staff read it: a titer as its endpoint ("1:32") rather than its stored readings, and a
/// number with its units ("0.45 %"). Other values are shown as stored.
/// </summary>
public static class ResultValueText
{
    public static string For(string? value, string? units = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "—";
        }

        if (TiterResultValue.TryParse(value, out var titer))
        {
            return titer!.EndpointLabel;
        }

        return string.IsNullOrWhiteSpace(units) ? value : $"{value} {units.Trim()}";
    }
}
