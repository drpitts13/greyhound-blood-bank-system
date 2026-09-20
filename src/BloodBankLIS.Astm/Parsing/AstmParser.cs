namespace BloodBankLIS.Astm.Parsing;

public sealed class AstmParseException(string message) : Exception(message);

/// <summary>Parses LIS2-A2 record text using delimiters from the header.</summary>
public static class AstmParser
{
    public static AstmMessage Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new AstmParseException("Message is empty.");
        }

        var normalized = raw.Replace("\r\n", "\r", StringComparison.Ordinal).Replace('\n', '\r').Trim();
        var lines = normalized.Split('\r', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0 || !lines[0].StartsWith('H'))
        {
            throw new AstmParseException("Message does not begin with a header (H) record.");
        }

        var records = new List<AstmRecord>(lines.Length);
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                continue;
            }

            var fields = line.Split('|');
            records.Add(new AstmRecord(char.ToUpperInvariant(fields[0][0]), fields));
        }

        if (records.All(r => r.Type != 'L'))
        {
            throw new AstmParseException("Message is missing a terminator (L) record.");
        }

        return new AstmMessage(records, string.Join("\r", lines) + "\r");
    }

    public static bool TryParse(string raw, out AstmMessage? message, out string? error)
    {
        try
        {
            message = Parse(raw);
            error = null;
            return true;
        }
        catch (AstmParseException ex)
        {
            message = null;
            error = ex.Message;
            return false;
        }
    }
}
