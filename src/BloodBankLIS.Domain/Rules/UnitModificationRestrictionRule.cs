using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Keeps an autologous or directed reservation on a modification result.
/// A pool proceeds only when every source shares the same restriction and,
/// when one is required, the same reserved patient.
/// </summary>
public static class UnitModificationRestrictionRule
{
    public const string DiscordantCode = "MOD-RESTRICTION-DISCORDANT";
    public const string RecipientCode = "MOD-RESTRICTION-RECIPIENT";

    public readonly record struct RestrictionFact(DonationRestriction Restriction, long? ReservedPatientId);

    public sealed record Inheritance(
        RuleEvaluation Evaluation,
        DonationRestriction Restriction,
        long? ReservedPatientId);

    public static Inheritance Evaluate(IReadOnlyList<RestrictionFact> sources)
    {
        if (sources.Count == 0)
        {
            return Stop(DiscordantCode, "A modification needs at least one source unit.");
        }

        var stops = new List<RuleResult>();
        foreach (var source in sources)
        {
            if (AutologousDirectedRule.RequiresRecipient(source.Restriction)
                && source.ReservedPatientId is null or <= 0)
            {
                stops.Add(RuleResult.HardStop(
                    RecipientCode,
                    $"This {source.Restriction.ToString().ToLowerInvariant()} unit has no intended recipient, so it cannot be modified."));
            }
        }

        if (stops.Count > 0)
        {
            return new Inheritance(new RuleEvaluation(stops), DonationRestriction.Allogeneic, null);
        }

        var first = sources[0];
        var restriction = first.Restriction;
        var patientId = AutologousDirectedRule.RequiresRecipient(restriction) ? first.ReservedPatientId : null;
        var agrees = sources.All(source =>
            source.Restriction == restriction
            && (patientId is null || source.ReservedPatientId == patientId));

        if (!agrees)
        {
            return Stop(
                DiscordantCode,
                "These units do not share the same autologous or directed reservation. Do not pool them.");
        }

        return new Inheritance(
            new RuleEvaluation(new[] { RuleResult.Pass(DiscordantCode) }),
            restriction,
            patientId);
    }

    private static Inheritance Stop(string code, string message) =>
        new(
            new RuleEvaluation(new[] { RuleResult.HardStop(code, message) }),
            DonationRestriction.Allogeneic,
            null);
}
