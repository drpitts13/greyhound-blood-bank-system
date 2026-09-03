namespace BloodBankLIS.Domain.Interfaces;

/// <summary>
/// Folder and file conventions for <see cref="Enums.InterfaceTransport.File"/> endpoints.
/// SoftBank/SafeTrace and hospital interface engines drop HL7 into a share; the LIS
/// polls the inbox, writes an ACK, and archives the original.
/// </summary>
public static class Hl7FileDropLayout
{
    public const string ProcessedFolder = "processed";
    public const string ErrorFolder = "error";
    public const string AckFolder = "ack";

    private static readonly HashSet<string> InboxExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".hl7", ".txt", ".adt", ".orm", ".oml", ".oru", ".dft", ".ras", ".bps"
    };

    public static bool HasPath(string? path) => !string.IsNullOrWhiteSpace(path);

    public static bool IsInboxFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var name = fileName.Trim();
        if (name.StartsWith('.'))
        {
            return false;
        }

        var ext = Path.GetExtension(name);
        return InboxExtensions.Contains(ext);
    }

    public static string OutboundFileName(string? controlId, DateTime utcNow)
    {
        var stamp = utcNow.ToUniversalTime().ToString("yyyyMMddHHmmssfff");
        var safe = SanitizeFileToken(controlId);
        return string.IsNullOrEmpty(safe) ? $"{stamp}.hl7" : $"{stamp}_{safe}.hl7";
    }

    public static string AckFileName(string sourceFileName)
    {
        var stem = Path.GetFileNameWithoutExtension(sourceFileName);
        return string.IsNullOrWhiteSpace(stem) ? "message.ack" : $"{stem}.ack";
    }

    public static string SanitizeFileToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value.Trim().Select(c =>
            char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray();
        return new string(chars).Trim('_');
    }
}
