namespace BloodBankLIS.Application.Search;

/// <summary>
/// The stored record (entity) type for a record the top-bar lookup found: the name audit rows and deviation links use
/// ("Patient", "BloodUnit", "Specimen", "Order").
/// </summary>
public static class LookupRecordType
{
    public static string For(LookupHitKind kind) => kind switch
    {
        LookupHitKind.Patient => "Patient",
        LookupHitKind.Unit => "BloodUnit",
        LookupHitKind.Specimen => "Specimen",
        LookupHitKind.Order => "Order",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No record type for this lookup kind.")
    };
}
