using BloodBankLIS.Application.Search;

namespace BloodBankLIS.Application.Compliance;

/// <summary>The record types a deviation can be linked to, with the older spellings stored before the pick list.</summary>
public static class DeviationRecordTypes
{
    public const string Patient = "Patient";
    public const string BloodUnit = "BloodUnit";
    public const string Specimen = "Specimen";
    public const string Order = "Order";

    /// <summary>The record type a deviation stores for a record the lookup found by scan or identifier.</summary>
    public static string FromLookup(LookupHitKind kind) => kind switch
    {
        LookupHitKind.Patient => Patient,
        LookupHitKind.Unit => BloodUnit,
        LookupHitKind.Specimen => Specimen,
        LookupHitKind.Order => Order,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No deviation record type for this lookup kind.")
    };

    public static IReadOnlyList<string> Aliases(string contextType) => contextType switch
    {
        BloodUnit or "Unit" => [BloodUnit, "Unit"],
        "Hl7" or "Hl7Message" => ["Hl7", "Hl7Message"],
        _ => [contextType]
    };
}
