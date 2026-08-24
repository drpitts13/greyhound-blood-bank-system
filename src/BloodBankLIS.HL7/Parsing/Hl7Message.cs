using BloodBankLIS.Domain.Interfaces;

namespace BloodBankLIS.HL7.Parsing;

/// <summary>
/// A parsed HL7 v2.x message. Provides safe, location-path accessors (e.g.
/// <c>Get("PID-3-1")</c>) that return an empty string for anything missing rather
/// than throwing or returning null (see docs/hl7-design.md section 1.2).
/// </summary>
public sealed class Hl7Message : Hl7MessageReader
{
    private readonly List<Hl7Segment> _segments;

    public Hl7Message(IEnumerable<Hl7Segment> segments, Hl7Encoding encoding, string rawMessage)
    {
        _segments = segments.ToList();
        Encoding = encoding;
        RawMessage = rawMessage;
    }

    public Hl7Encoding Encoding { get; }

    public string RawMessage { get; }

    public IReadOnlyList<Hl7Segment> Segments => _segments;

    public Hl7Segment? Segment(string name) =>
        _segments.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.Ordinal));

    public IEnumerable<Hl7Segment> AllSegments(string name) =>
        _segments.Where(s => string.Equals(s.Name, name, StringComparison.Ordinal));

    public string MessageType => Get("MSH-9-1");
    public string TriggerEvent => Get("MSH-9-2");
    public string MessageControlId => Get("MSH-10");

    /// <summary>
    /// Reads a value by HL7 location path "SEG-field[-component[-subcomponent]]".
    /// All indices are 1-based; repetition is resolved to the first repetition.
    /// Returns the unescaped value, or empty string if not present.
    /// </summary>
    public string Get(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var parts = path.Split('-');
        if (parts.Length < 2 || !int.TryParse(parts[1], out var fieldNo))
        {
            return string.Empty;
        }

        var segment = Segment(parts[0]);
        if (segment is null)
        {
            return string.Empty;
        }

        var fieldValue = segment.Field(fieldNo);

        // MSH-1 and MSH-2 are literal control characters, not delimited content.
        if (segment.Name == "MSH" && fieldNo <= 2)
        {
            return fieldValue;
        }

        var repetition = fieldValue.Split(Encoding.RepetitionSeparator)[0];

        if (parts.Length < 3 || !int.TryParse(parts[2], out var componentNo))
        {
            return Encoding.Unescape(repetition);
        }

        var components = repetition.Split(Encoding.ComponentSeparator);
        var component = componentNo - 1 < components.Length ? components[componentNo - 1] : string.Empty;

        if (parts.Length < 4 || !int.TryParse(parts[3], out var subNo))
        {
            return Encoding.Unescape(component);
        }

        var subs = component.Split(Encoding.SubComponentSeparator);
        var sub = subNo - 1 < subs.Length ? subs[subNo - 1] : string.Empty;
        return Encoding.Unescape(sub);
    }
}
