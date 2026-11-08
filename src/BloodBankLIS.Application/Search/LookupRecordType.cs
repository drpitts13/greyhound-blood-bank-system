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
        // The lot hit carries the first evaluation that recorded the lot given; its audit rows are RhigEvaluation.
        LookupHitKind.RhigLot => "RhigEvaluation",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No record type for this lookup kind.")
    };
}
