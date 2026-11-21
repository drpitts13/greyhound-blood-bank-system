namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// OCD-093 (decided 2026-11-17, product owner, end-user testing pass 226): a result entered while its specimen was in
/// date may be verified after the specimen expires. The specimen stays expired for anything new (no new result, no
/// crossmatch); only the testing already done on it is completed. Entering a result after expiry still follows OCD-091.
/// </summary>
public static class ExpiredSpecimenVerificationRule
{
    /// <summary>
    /// True when the result was entered (and, when recorded, tested) before the specimen expired. A specimen with no
    /// expiration, or a result with no entry time, does not qualify.
    /// </summary>
    public static bool EnteredWhileInDate(DateTime? enteredUtc, DateTime? testedUtc, DateTime? specimenExpiresUtc) =>
        specimenExpiresUtc is DateTime expires
        && enteredUtc is DateTime entered
        && entered < expires
        && (testedUtc is not DateTime tested || tested < expires);
}
