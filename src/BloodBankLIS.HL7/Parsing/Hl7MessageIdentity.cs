namespace BloodBankLIS.HL7.Parsing;

/// <summary>
/// Reads patient identity from a stored inbound message so worklists can show
/// MRN and name without opening the raw payload.
/// </summary>
public static class Hl7MessageIdentity
{
    public static bool TryRead(string? rawMessage, out string? medicalRecordNumber, out string? displayName)
    {
        medicalRecordNumber = null;
        displayName = null;
        if (!Hl7Parser.TryParse(rawMessage ?? string.Empty, out var message, out _) || message is null)
        {
            return false;
        }

        medicalRecordNumber = FirstNonEmpty(message.Get("PID-3-1"), message.Get("PID-3"));
        var last = message.Get("PID-5-1");
        var first = message.Get("PID-5-2");
        if (!string.IsNullOrWhiteSpace(last) || !string.IsNullOrWhiteSpace(first))
        {
            displayName = $"{last}, {first}".Trim(' ', ',');
        }

        return medicalRecordNumber is not null || displayName is not null;
    }

    private static string? FirstNonEmpty(params string[] values) =>
        values.Select(v => v?.Trim()).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
