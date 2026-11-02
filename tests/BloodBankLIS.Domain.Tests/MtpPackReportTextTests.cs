using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-326: the pack report states only what the system knows; a time limit appears only from facility hours.
public class MtpPackReportTextTests
{
    [Fact]
    public void BaseStatements_HaveNoTimeLimit()
    {
        var lines = MtpPackReportText.Disclaimers(false, null, false);
        Assert.Equal(4, lines.Count);
        Assert.DoesNotContain(lines, l => l.Contains("hour", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(lines, l => l.Contains("bedside"));
    }

    [Fact]
    public void Uncrossmatched_TemperatureAndHours_AddTheirStatements()
    {
        var lines = MtpPackReportText.Disclaimers(true, 4, true);
        Assert.Equal(7, lines.Count);
        Assert.Contains(lines, l => l.Contains("Uncrossmatched"));
        Assert.Contains(lines, l => l.Contains("outside the range"));
        Assert.Contains(lines, l => l.Contains("up to 4 hours"));
        Assert.Contains("up to 1 hour from", string.Join(" ", MtpPackReportText.Disclaimers(false, 1, false)));
    }
}
