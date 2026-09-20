namespace BloodBankLIS.Astm.Parsing;

/// <summary>One LIS2 record (H, P, O, R, Q, C, or L) with 1-based fields and components.</summary>
public sealed class AstmRecord
{
    public AstmRecord(char type, IReadOnlyList<string> fields)
    {
        Type = type;
        Fields = fields;
    }

    public char Type { get; }

    /// <summary>Field 1 is the record type. Subsequent entries are fields 2..n.</summary>
    public IReadOnlyList<string> Fields { get; }

    public string GetField(int field)
    {
        if (field < 1 || field > Fields.Count)
        {
            return string.Empty;
        }

        return Fields[field - 1];
    }

    public string GetComponent(int field, int component)
    {
        var raw = GetField(field);
        if (string.IsNullOrEmpty(raw) || component < 1)
        {
            return string.Empty;
        }

        var parts = raw.Split('^');
        return component <= parts.Length ? parts[component - 1] : string.Empty;
    }

    public string Raw => string.Join('|', Fields);
}
