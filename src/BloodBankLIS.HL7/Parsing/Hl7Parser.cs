namespace BloodBankLIS.HL7.Parsing;

/// <summary>Thrown when a message cannot be parsed into a valid HL7 structure.</summary>
public sealed class Hl7ParseException(string message) : Exception(message);

/// <summary>
/// In-house HL7 v2.x parser. Reads encoding characters from the message itself,
/// tokenizes into segments/fields, and performs minimal structural validation
/// (MSH present, message type and control id available). Pure; no I/O.
/// </summary>
public static class Hl7Parser
{
    private static readonly char[] SegmentTerminators = { '\r', '\n' };

    public static Hl7Message Parse(string rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
        {
            throw new Hl7ParseException("Message is empty.");
        }

        var trimmed = rawMessage.TrimStart('\r', '\n', ' ');
        if (!trimmed.StartsWith("MSH", StringComparison.Ordinal))
        {
            throw new Hl7ParseException("Message does not begin with an MSH segment.");
        }

        var encoding = Hl7Encoding.FromMshHeader(trimmed);

        var segmentTexts = trimmed.Split(SegmentTerminators, StringSplitOptions.RemoveEmptyEntries);
        var segments = new List<Hl7Segment>(segmentTexts.Length);

        foreach (var text in segmentTexts)
        {
            if (text.Length < 3)
            {
                continue;
            }

            var name = text.Substring(0, 3);
            segments.Add(name == "MSH"
                ? ParseMshSegment(text, encoding)
                : ParseSegment(name, text, encoding));
        }

        var message = new Hl7Message(segments, encoding, rawMessage);

        if (string.IsNullOrEmpty(message.MessageType))
        {
            throw new Hl7ParseException("MSH-9 message type is missing.");
        }

        if (string.IsNullOrEmpty(message.MessageControlId))
        {
            throw new Hl7ParseException("MSH-10 message control id is missing.");
        }

        return message;
    }

    public static bool TryParse(string rawMessage, out Hl7Message? message, out string? error)
    {
        try
        {
            message = Parse(rawMessage);
            error = null;
            return true;
        }
        catch (Hl7ParseException ex)
        {
            message = null;
            error = ex.Message;
            return false;
        }
    }

    private static Hl7Segment ParseSegment(string name, string text, Hl7Encoding encoding)
    {
        // Tokens after the segment name are fields 1..n.
        var tokens = text.Split(encoding.FieldSeparator);
        return new Hl7Segment(name, tokens.Skip(1));
    }

    private static Hl7Segment ParseMshSegment(string text, Hl7Encoding encoding)
    {
        // For MSH, expose the field separator as field 1 and encoding chars as field 2,
        // matching HL7 numbering (MSH-3 is the first delimited field).
        var tokens = text.Split(encoding.FieldSeparator);
        var fields = new List<string> { encoding.FieldSeparator.ToString() };
        fields.AddRange(tokens.Skip(1));
        return new Hl7Segment("MSH", fields);
    }
}
