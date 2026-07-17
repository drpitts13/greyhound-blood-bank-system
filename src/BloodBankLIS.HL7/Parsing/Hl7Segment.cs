namespace BloodBankLIS.HL7.Parsing;

/// <summary>
/// A parsed HL7 segment. <see cref="Fields"/> is 1-based by HL7 convention: index 0
/// holds field 1. For MSH the field separator is treated as MSH-1 and the encoding
/// characters as MSH-2, matching how HL7 numbers the header.
/// </summary>
public sealed class Hl7Segment
{
    private readonly List<string> _fields;

    public Hl7Segment(string name, IEnumerable<string> fields)
    {
        Name = name;
        _fields = fields.ToList();
    }

    public string Name { get; }

    public IReadOnlyList<string> Fields => _fields;

    /// <summary>Raw field value by 1-based HL7 field number, or empty when absent.</summary>
    public string Field(int oneBasedIndex)
    {
        var i = oneBasedIndex - 1;
        return i >= 0 && i < _fields.Count ? _fields[i] : string.Empty;
    }
}
