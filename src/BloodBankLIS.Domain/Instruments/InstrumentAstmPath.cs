namespace BloodBankLIS.Domain.Instruments;

/// <summary>Parsed ASTM locator such as P.3 or R.4.1 (1-based field and component).</summary>
public readonly record struct InstrumentAstmPath(char RecordType, int Field, int? Component)
{
    public static bool TryParse(string? text, out InstrumentAstmPath path)
    {
        path = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.Length < 3 || trimmed[1] != '.')
        {
            return false;
        }

        var record = char.ToUpperInvariant(trimmed[0]);
        if (record is not ('H' or 'P' or 'O' or 'R' or 'Q' or 'C' or 'L'))
        {
            return false;
        }

        var rest = trimmed[2..];
        var parts = rest.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is < 1 or > 2 || !int.TryParse(parts[0], out var field) || field < 1)
        {
            return false;
        }

        int? component = null;
        if (parts.Length == 2)
        {
            if (!int.TryParse(parts[1], out var c) || c < 1)
            {
                return false;
            }

            component = c;
        }

        path = new InstrumentAstmPath(record, field, component);
        return true;
    }

    public override string ToString() =>
        Component is int c ? $"{RecordType}.{Field}.{c}" : $"{RecordType}.{Field}";
}
