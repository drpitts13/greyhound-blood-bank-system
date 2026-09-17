namespace BloodBankLIS.HL7.Parsing;

/// <summary>
/// Reads identity and order context from a stored message so worklists can
/// show MRN, name, placer, test, and unit without opening the raw payload.
/// </summary>
public sealed record Hl7MessageIdentityInfo(
    string? MedicalRecordNumber,
    string? DisplayName,
    string? PlacerOrderNumber = null,
    string? TestCode = null,
    string? UnitNumber = null);

public static class Hl7MessageIdentity
{
    public static bool TryRead(string? rawMessage, out string? medicalRecordNumber, out string? displayName)
    {
        var found = TryRead(rawMessage, out var info);
        medicalRecordNumber = info.MedicalRecordNumber;
        displayName = info.DisplayName;
        return found;
    }

    public static bool TryRead(string? rawMessage, out Hl7MessageIdentityInfo info)
    {
        info = new Hl7MessageIdentityInfo(null, null);
        if (!Hl7Parser.TryParse(rawMessage ?? string.Empty, out var message, out _) || message is null)
        {
            return false;
        }

        var medicalRecordNumber = FirstNonEmpty(message.Get("PID-3-1"), message.Get("PID-3"));
        var last = message.Get("PID-5-1");
        var first = message.Get("PID-5-2");
        string? displayName = null;
        if (!string.IsNullOrWhiteSpace(last) || !string.IsNullOrWhiteSpace(first))
        {
            displayName = $"{last}, {first}".Trim(' ', ',');
        }

        info = new Hl7MessageIdentityInfo(
            medicalRecordNumber,
            displayName,
            FirstNonEmpty(message.Get("ORC-2-1"), message.Get("ORC-2"), message.Get("OBR-2-1"), message.Get("OBR-2")),
            FirstNonEmpty(message.Get("OBR-4-1"), message.Get("OBX-3-1"), message.Get("OBR-4"), message.Get("OBX-3")),
            FirstNonEmpty(message.Get("RXA-15"), message.Get("RXA-15-1"), message.Get("RXA-10-1")));

        return info.MedicalRecordNumber is not null
            || info.DisplayName is not null
            || info.PlacerOrderNumber is not null
            || info.TestCode is not null
            || info.UnitNumber is not null;
    }

    private static string? FirstNonEmpty(params string[] values) =>
        values.Select(v => v?.Trim()).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
