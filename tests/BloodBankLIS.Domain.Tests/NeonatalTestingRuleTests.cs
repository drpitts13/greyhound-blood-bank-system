using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

/// <summary>OCD-074 (decided 2026-11-10): an infant's specimen is valid for the admission when the neonatal rules are on.</summary>
public class NeonatalTestingRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Born = new(2026, 11, 1);

    // TEST-BB-872: an infant is under the limit until the day it is reached; the admission dating runs to discharge or the
    // age limit, whichever is first, never below the usual expiration; nothing changes when the rules are off, without a
    // visit, past the limit, or with a visit already discharged. Discharge ends it, never below the usual expiration.
    [Fact]
    public void AdmissionDating()
    {
        Assert.True(NeonatalTestingRule.IsNeonate(Born, Now, 120));
        Assert.True(NeonatalTestingRule.IsNeonate(Born, Born.AddDays(119).ToDateTime(TimeOnly.MinValue), 120));
        Assert.False(NeonatalTestingRule.IsNeonate(Born, Born.AddDays(120).ToDateTime(TimeOnly.MinValue), 120));
        Assert.False(NeonatalTestingRule.IsNeonate(Born, Born.AddDays(-1).ToDateTime(TimeOnly.MinValue), 120));

        var usual = Now.AddDays(3);
        var limit = NeonatalTestingRule.AgeLimitReachedUtc(Born, 120);
        Assert.Equal(new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc), limit);
        Assert.Equal(limit, NeonatalTestingRule.AdmissionExpiresUtc(true, 120, Born, Now, true, null, usual));
        Assert.Equal(Now.AddDays(10), NeonatalTestingRule.AdmissionExpiresUtc(true, 120, Born, Now, true, Now.AddDays(10), usual));
        Assert.Null(NeonatalTestingRule.AdmissionExpiresUtc(true, 120, Born, Now, true, Now.AddDays(1), usual));
        Assert.Null(NeonatalTestingRule.AdmissionExpiresUtc(false, 120, Born, Now, true, null, usual));
        Assert.Null(NeonatalTestingRule.AdmissionExpiresUtc(true, 120, Born, Now, false, null, usual));
        Assert.Null(NeonatalTestingRule.AdmissionExpiresUtc(true, 5, Born, Now, true, null, usual));
        Assert.Null(NeonatalTestingRule.AdmissionExpiresUtc(true, 120, Born, Now, true, Now.AddHours(-1), usual));

        Assert.Equal(Now.AddDays(10), NeonatalTestingRule.OnDischarge(limit, usual, Now.AddDays(10)));
        Assert.Equal(usual, NeonatalTestingRule.OnDischarge(limit, usual, Now.AddDays(1)));
        Assert.Null(NeonatalTestingRule.OnDischarge(Now.AddDays(5), usual, Now.AddDays(10)));
    }
}
