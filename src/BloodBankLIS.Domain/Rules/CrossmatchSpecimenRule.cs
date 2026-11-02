namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A crossmatch may not be recorded on a specimen that has already expired: it would
/// be out of date the moment it is saved. Result entry refuses an expired specimen for
/// every test, including retrospective crossmatches after emergency release; this rule
/// applies the same refusal to crossmatches recorded directly and to the automatic
/// electronic crossmatch (OCD-050 asks whether retrospective crossmatches need an exception).
/// A specimen with no expiration keeps the default crossmatch validity.
/// </summary>
public static class CrossmatchSpecimenRule
{
    public const string Code = "XM-SPEC-EXPIRED";

    public static RuleResult Evaluate(string? accessionNumber, DateTime? specimenExpiresUtc, DateTime nowUtc)
    {
        if (specimenExpiresUtc is not DateTime expires || nowUtc < expires)
        {
            return RuleResult.Pass(Code);
        }

        var name = string.IsNullOrWhiteSpace(accessionNumber) ? "The specimen" : $"Specimen {accessionNumber}";
        return RuleResult.HardStop(
            Code,
            $"{name} expired at {expires:yyyy-MM-dd HH:mm} UTC. Collect a new specimen and crossmatch on it.");
    }
}
