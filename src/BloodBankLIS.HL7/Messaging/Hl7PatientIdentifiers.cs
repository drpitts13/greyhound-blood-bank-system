using BloodBankLIS.Domain.Interfaces;
using BloodBankLIS.HL7.Parsing;

namespace BloodBankLIS.HL7.Messaging;

/// <summary>One PID-3 repetition (CX): the id, its assigning authority (CX.4), and identifier type code (CX.5).</summary>
public sealed record Hl7PatientIdentifier(string Id, string? AssigningAuthority, string? TypeCode);

/// <summary>
/// PID-3 may carry several identifiers (MRN, enterprise id, other facilities' numbers). The MRN is the repetition whose
/// identifier type is MR; with no typed MR repetition the first repetition is used, as before. The other repetitions are
/// kept as alternate identifiers.
/// </summary>
public static class Hl7PatientIdentifiers
{
    public const string MrnTypeCode = "MR";
    private const string DefaultMrnPath = "PID-3-1";

    public static IReadOnlyList<Hl7PatientIdentifier> Read(Hl7Message message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var pid = message.Segment("PID");
        if (pid is null)
        {
            return [];
        }

        var encoding = message.Encoding;
        return pid.Field(3)
            .Split(encoding.RepetitionSeparator)
            .Select(rep =>
            {
                var c = rep.Split(encoding.ComponentSeparator);
                string Part(int i) => i < c.Length ? encoding.Unescape(c[i]).Trim() : string.Empty;
                // CX.4 is a hierarchic designator; its namespace id (first subcomponent) names the authority.
                var authority = c.Length > 3
                    ? encoding.Unescape(c[3].Split(encoding.SubComponentSeparator)[0]).Trim()
                    : string.Empty;
                return new Hl7PatientIdentifier(Part(0), NullIfEmpty(authority), NullIfEmpty(Part(4)));
            })
            .Where(cx => cx.Id.Length > 0)
            .ToList();
    }

    /// <summary>The MRN repetition and the others (distinct by id and type), in message order.</summary>
    public static (Hl7PatientIdentifier? Mrn, IReadOnlyList<Hl7PatientIdentifier> Others) Select(IReadOnlyList<Hl7PatientIdentifier> identifiers)
    {
        if (identifiers.Count == 0)
        {
            return (null, []);
        }

        var mrn = identifiers.FirstOrDefault(cx => string.Equals(cx.TypeCode, MrnTypeCode, StringComparison.OrdinalIgnoreCase))
                  ?? identifiers[0];
        var others = identifiers
            .Where(cx => !ReferenceEquals(cx, mrn) && !string.Equals(cx.Id, mrn.Id, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(cx => (cx.Id.ToUpperInvariant(), cx.TypeCode?.ToUpperInvariant()))
            .ToList();
        return (mrn, others);
    }

    /// <summary>
    /// The MRN for an inbound message: the typed MR repetition when the interface reads the default PID-3-1; a custom
    /// mapped path is read as configured.
    /// </summary>
    public static string Mrn(Hl7Message message, Hl7FieldMap map)
    {
        if (!string.Equals(map.Path(InterfaceDataItemKeys.PatientMrn), DefaultMrnPath, StringComparison.OrdinalIgnoreCase))
        {
            return map.Get(message, InterfaceDataItemKeys.PatientMrn);
        }

        var (mrn, _) = Select(Read(message));
        return mrn?.Id ?? map.Get(message, InterfaceDataItemKeys.PatientMrn);
    }

    /// <summary>The non-MRN repetitions, when the interface reads the default path; none otherwise.</summary>
    public static IReadOnlyList<Hl7PatientIdentifier> Others(Hl7Message message, Hl7FieldMap map) =>
        string.Equals(map.Path(InterfaceDataItemKeys.PatientMrn), DefaultMrnPath, StringComparison.OrdinalIgnoreCase)
            ? Select(Read(message)).Others
            : [];

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;
}
