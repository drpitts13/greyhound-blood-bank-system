namespace BloodBankLIS.Web.Services;

/// <summary>
/// An HL7 or ASTM message as shown on screen, one segment or record per line (end-user testing pass 203: both end each
/// segment with a carriage return, which a browser does not show as a line break, so MSH, PID, ORC and OBR, or H, P, O and
/// R, ran together).
/// </summary>
public static class MessageRawText
{
    public static string ForDisplay(string? raw) =>
        string.IsNullOrEmpty(raw) ? string.Empty : raw.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').TrimEnd('\n');
}
