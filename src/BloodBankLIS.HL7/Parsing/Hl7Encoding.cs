namespace BloodBankLIS.HL7.Parsing;

/// <summary>
/// HL7 encoding characters, taken from MSH-1 (field separator) and MSH-2 (component,
/// repetition, escape, subcomponent). Never hard-coded; the message declares them
/// (see docs/hl7-design.md section 1.1). Also handles escape sequence encode/decode.
/// </summary>
public sealed class Hl7Encoding
{
    public Hl7Encoding(
        char fieldSeparator = '|',
        char componentSeparator = '^',
        char repetitionSeparator = '~',
        char escapeCharacter = '\\',
        char subComponentSeparator = '&')
    {
        FieldSeparator = fieldSeparator;
        ComponentSeparator = componentSeparator;
        RepetitionSeparator = repetitionSeparator;
        EscapeCharacter = escapeCharacter;
        SubComponentSeparator = subComponentSeparator;
    }

    public char FieldSeparator { get; }
    public char ComponentSeparator { get; }
    public char RepetitionSeparator { get; }
    public char EscapeCharacter { get; }
    public char SubComponentSeparator { get; }

    public static Hl7Encoding Default { get; } = new();

    /// <summary>The "^~\&amp;" string that becomes MSH-2 for outbound messages.</summary>
    public string EncodingCharacters =>
        $"{ComponentSeparator}{RepetitionSeparator}{EscapeCharacter}{SubComponentSeparator}";

    /// <summary>
    /// Reads encoding characters from a raw message's MSH segment. Falls back to the
    /// HL7 defaults for any character the message does not declare.
    /// </summary>
    public static Hl7Encoding FromMshHeader(string rawMessage)
    {
        if (string.IsNullOrEmpty(rawMessage) || rawMessage.Length < 4 || !rawMessage.StartsWith("MSH", StringComparison.Ordinal))
        {
            return Default;
        }

        var fieldSep = rawMessage[3];
        var component = '^';
        var repetition = '~';
        var escape = '\\';
        var subComponent = '&';

        // MSH-2 is the run of encoding characters immediately after the field separator.
        var encoding = string.Empty;
        for (var i = 4; i < rawMessage.Length && rawMessage[i] != fieldSep; i++)
        {
            encoding += rawMessage[i];
        }

        if (encoding.Length > 0) component = encoding[0];
        if (encoding.Length > 1) repetition = encoding[1];
        if (encoding.Length > 2) escape = encoding[2];
        if (encoding.Length > 3) subComponent = encoding[3];

        return new Hl7Encoding(fieldSep, component, repetition, escape, subComponent);
    }

    /// <summary>Escapes separator characters in a value for outbound serialization.</summary>
    public string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sb = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (ch == EscapeCharacter) sb.Append(EscapeCharacter).Append('E').Append(EscapeCharacter);
            else if (ch == FieldSeparator) sb.Append(EscapeCharacter).Append('F').Append(EscapeCharacter);
            else if (ch == ComponentSeparator) sb.Append(EscapeCharacter).Append('S').Append(EscapeCharacter);
            else if (ch == RepetitionSeparator) sb.Append(EscapeCharacter).Append('R').Append(EscapeCharacter);
            else if (ch == SubComponentSeparator) sb.Append(EscapeCharacter).Append('T').Append(EscapeCharacter);
            else sb.Append(ch);
        }

        return sb.ToString();
    }

    /// <summary>Decodes HL7 escape sequences (\F\ \S\ \R\ \T\ \E\) back to literals.</summary>
    public string Unescape(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.IndexOf(EscapeCharacter) < 0)
        {
            return value ?? string.Empty;
        }

        var sb = new System.Text.StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != EscapeCharacter)
            {
                sb.Append(value[i]);
                continue;
            }

            var end = value.IndexOf(EscapeCharacter, i + 1);
            if (end < 0)
            {
                sb.Append(value[i]);
                continue;
            }

            var code = value.Substring(i + 1, end - i - 1);
            sb.Append(code switch
            {
                "F" => FieldSeparator,
                "S" => ComponentSeparator,
                "R" => RepetitionSeparator,
                "T" => SubComponentSeparator,
                "E" => EscapeCharacter,
                _ => '\0'
            } is var c && c != '\0' ? c.ToString() : code);
            i = end;
        }

        return sb.ToString();
    }
}
