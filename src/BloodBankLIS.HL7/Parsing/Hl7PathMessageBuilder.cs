using System.Text;

namespace BloodBankLIS.HL7.Parsing;

/// <summary>
/// Builds an HL7 message by assigning values to location paths (e.g. PID-3-1, OBX-5).
/// Empty intermediate fields are preserved so field numbers stay stable.
/// </summary>
public sealed class Hl7PathMessageBuilder
{
    private readonly Hl7Encoding _encoding;
    private readonly List<string> _segmentOrder = new();
    private readonly Dictionary<string, Dictionary<int, FieldBox>> _segments = new(StringComparer.OrdinalIgnoreCase);
    private string? _msh;

    public Hl7PathMessageBuilder(Hl7Encoding? encoding = null) =>
        _encoding = encoding ?? Hl7Encoding.Default;

    public Hl7PathMessageBuilder AppendMsh(
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
        _msh = new Hl7MessageBuilder(_encoding)
            .AppendMsh(sendingApp, sendingFacility, receivingApp, receivingFacility, messageDateTimeUtc, messageType, controlId, processingId, version)
            .Build();
        return this;
    }

    public Hl7PathMessageBuilder EnsureSegment(string name)
    {
        if (string.Equals(name, "MSH", StringComparison.OrdinalIgnoreCase))
        {
            return this;
        }

        if (!_segments.ContainsKey(name))
        {
            _segments[name] = new Dictionary<int, FieldBox>();
            _segmentOrder.Add(name);
        }

        return this;
    }

    public Hl7PathMessageBuilder Set(string path, string? value)
    {
        if (string.IsNullOrWhiteSpace(path) || value is null)
        {
            return this;
        }

        var parts = path.Trim().Split('-');
        if (parts.Length < 2 || !int.TryParse(parts[1], out var fieldNo) || fieldNo < 1)
        {
            return this;
        }

        var segment = parts[0].ToUpperInvariant();
        EnsureSegment(segment);
        if (!_segments.TryGetValue(segment, out var fields))
        {
            return this;
        }

        if (!fields.TryGetValue(fieldNo, out var box))
        {
            box = new FieldBox();
            fields[fieldNo] = box;
        }

        if (parts.Length < 3 || !int.TryParse(parts[2], out var componentNo) || componentNo < 1)
        {
            box.Whole = _encoding.Escape(value);
            return this;
        }

        box.Components[componentNo] = _encoding.Escape(value);
        return this;
    }

    public string Build()
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(_msh))
        {
            parts.Add(_msh);
        }

        foreach (var name in _segmentOrder)
        {
            parts.Add(RenderSegment(name));
        }

        return string.Join("\r", parts);
    }

    private string RenderSegment(string name)
    {
        var fields = _segments[name];
        var max = fields.Count == 0 ? 0 : fields.Keys.Max();
        var sb = new StringBuilder(name);
        for (var i = 1; i <= max; i++)
        {
            sb.Append(_encoding.FieldSeparator);
            if (!fields.TryGetValue(i, out var box))
            {
                continue;
            }

            if (box.Components.Count == 0)
            {
                sb.Append(box.Whole ?? string.Empty);
                continue;
            }

            var maxComp = box.Components.Keys.Max();
            var comps = new string[maxComp];
            for (var c = 1; c <= maxComp; c++)
            {
                comps[c - 1] = box.Components.TryGetValue(c, out var v) ? v : string.Empty;
            }

            sb.Append(string.Join(_encoding.ComponentSeparator, comps));
        }

        return sb.ToString();
    }

    private sealed class FieldBox
    {
        public string? Whole { get; set; }

        public Dictionary<int, string> Components { get; } = new();
    }
}
