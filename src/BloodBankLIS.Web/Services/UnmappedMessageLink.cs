namespace BloodBankLIS.Web.Services;

/// <summary>
/// Where an unmapped value's last message opens (end-user testing pass 276: the Unmapped values list named the message,
/// "UAT-ORM-CA-3" or "Specimen QC-ABSC-POS", as plain text, so checking what the EHR or analyzer sent meant searching for
/// it by hand). An EHR message opens the HL7 message log searched for its control ID; an instrument message opens
/// Instrument Interfaces searched for its sample ID. Null when there is no reference.
/// </summary>
public static class UnmappedMessageLink
{
    private const string SpecimenPrefix = "Specimen ";

    public static string? Href(string? reference, bool fromInstrument)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var value = reference.Trim();
        if (fromInstrument)
        {
            var sample = value.StartsWith(SpecimenPrefix, StringComparison.OrdinalIgnoreCase) ? value[SpecimenPrefix.Length..].Trim() : value;
            return $"/instruments?search={Uri.EscapeDataString(sample)}";
        }

        return $"/hl7?search={Uri.EscapeDataString(value)}";
    }
}
