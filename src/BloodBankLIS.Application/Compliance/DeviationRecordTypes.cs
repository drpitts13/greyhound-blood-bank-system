namespace BloodBankLIS.Application.Compliance;

/// <summary>The record types a deviation can be linked to, with the older spellings stored before the pick list.</summary>
public static class DeviationRecordTypes
{
    public const string Patient = "Patient";
    public const string BloodUnit = "BloodUnit";

    public static IReadOnlyList<string> Aliases(string contextType) => contextType switch
    {
        BloodUnit or "Unit" => [BloodUnit, "Unit"],
        "Hl7" or "Hl7Message" => ["Hl7", "Hl7Message"],
        _ => [contextType]
    };
}
