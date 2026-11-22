using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

/// <summary>OCD-091 (decided 2026-11-14): results read on paper and entered after the specimen expired.</summary>
public class ExpiredSpecimenBackEntryRuleTests
{
    private static readonly DateTime Expires = new(2026, 11, 13, 6, 0, 0, DateTimeKind.Utc);
    private static readonly DowntimeEpisode Outage = new()
    {
        StartedUtc = new DateTime(2026, 11, 12, 3, 0, 0, DateTimeKind.Utc),
        EndedUtc = new DateTime(2026, 11, 12, 5, 0, 0, DateTimeKind.Utc),
        Description = "LIS down"
    };

    // TEST-BB-1100: a patient test (option C) needs the time tested before expiry, within a recorded outage, and a
    // reason; a crossmatch (option B) needs the time before expiry and a reason, outage or not.
    [Fact]
    public void Evaluate_PatientTestsNeedAnOutage_CrossmatchesDoNot()
    {
        var inOutage = new DateTime(2026, 11, 12, 4, 0, 0, DateTimeKind.Utc);
        var noOutage = new DateTime(2026, 11, 12, 20, 0, 0, DateTimeKind.Utc);
        const ExpiredSpecimenBackEntryRule.Kind test = ExpiredSpecimenBackEntryRule.Kind.PatientTest;
        const ExpiredSpecimenBackEntryRule.Kind xm = ExpiredSpecimenBackEntryRule.Kind.Crossmatch;

        Assert.Equal(RuleSeverity.Pass, ExpiredSpecimenBackEntryRule.Evaluate(test, inOutage, Expires, [Outage], "Read on paper").Severity);
        Assert.Contains("recorded outage", ExpiredSpecimenBackEntryRule.Evaluate(test, noOutage, Expires, [Outage], "Read on paper").Message);
        Assert.Contains("recorded outage", ExpiredSpecimenBackEntryRule.Evaluate(test, inOutage, Expires, [], "Read on paper").Message);
        Assert.Equal(RuleSeverity.Pass, ExpiredSpecimenBackEntryRule.Evaluate(xm, noOutage, Expires, [], "Read on paper").Severity);

        var late = ExpiredSpecimenBackEntryRule.Evaluate(xm, Expires.AddMinutes(1), Expires, [Outage], "Read on paper");
        Assert.Equal((RuleSeverity.HardStop, ExpiredSpecimenBackEntryRule.Code), (late.Severity, late.Code));
        Assert.StartsWith("The specimen had expired", late.Message);
        Assert.Contains("Give the reason", ExpiredSpecimenBackEntryRule.Evaluate(xm, noOutage, Expires, [], " ").Message);
        Assert.Equal(RuleSeverity.HardStop, ExpiredSpecimenBackEntryRule.Evaluate(xm, noOutage, Expires, [], new string('x', 501)).Severity);
        Assert.Equal(RuleSeverity.Pass, ExpiredSpecimenBackEntryRule.EvaluateTime(test, inOutage, Expires, [Outage]).Severity);
    }
}
