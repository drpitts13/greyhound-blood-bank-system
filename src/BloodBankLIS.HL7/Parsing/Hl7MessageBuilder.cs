using System.Text;

namespace BloodBankLIS.HL7.Parsing;

/// <summary>
/// Builds outbound HL7 messages using the configured encoding characters and escape
/// rules. Leaf values are escaped; component composition is explicit so intended
/// separators are preserved (see docs/hl7-design.md section 1.2).
/// </summary>
public sealed class Hl7MessageBuilder
{
    private readonly Hl7Encoding _encoding;
    private readonly List<string> _segments = new();

    public Hl7MessageBuilder(Hl7Encoding? encoding = null) => _encoding = encoding ?? Hl7Encoding.Default;

    public const string Hl7TimestampFormat = "yyyyMMddHHmmss";

    public Hl7MessageBuilder AppendMsh(
        string sendingApp,
        string sendingFacility,
        string receivingApp,
        string receivingFacility,
        DateTime messageDateTimeUtc,
        string messageType,
        string controlId,
        string processingId = "P",
        string version = "2.5")
    {
        var f = _encoding.FieldSeparator;
        var ts = messageDateTimeUtc.ToString(Hl7TimestampFormat);
        var msh = new StringBuilder("MSH")
            .Append(f).Append(_encoding.EncodingCharacters)
            .Append(f).Append(_encoding.Escape(sendingApp))
            .Append(f).Append(_encoding.Escape(sendingFacility))
            .Append(f).Append(_encoding.Escape(receivingApp))
            .Append(f).Append(_encoding.Escape(receivingFacility))
            .Append(f).Append(ts)
            .Append(f) // MSH-8 security
            .Append(f).Append(messageType)
            .Append(f).Append(_encoding.Escape(controlId))
            .Append(f).Append(processingId)
            .Append(f).Append(version);
        _segments.Add(msh.ToString());
        return this;
    }

    /// <summary>Appends a segment whose fields are already composed (e.g. via <see cref="Field"/>).</summary>
    public Hl7MessageBuilder AppendSegment(string name, params string[] fields)
    {
        var sb = new StringBuilder(name);
        foreach (var field in fields)
        {
            sb.Append(_encoding.FieldSeparator).Append(field);
        }

        _segments.Add(sb.ToString());
        return this;
    }

    /// <summary>Composes a field from components, escaping each leaf value.</summary>
    public string Field(params string?[] components) =>
        string.Join(_encoding.ComponentSeparator, components.Select(c => _encoding.Escape(c)));

    public string Build() => string.Join("\r", _segments);
}
