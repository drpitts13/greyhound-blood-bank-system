using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

/// <summary>Recorded outages (Cycle 662): which outage a paper time falls in, and the warning when it falls in none.</summary>
public class DowntimeEpisodeRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    private static DowntimeEpisode Ep(long id, double startHours, double? endHours, string text = "LIS down") => new()
    {
        Id = id, StartedUtc = Now.AddHours(startHours), EndedUtc = endHours is { } e ? Now.AddHours(e) : null, Description = text
    };

    // TEST-BB-843: a time inside an ended outage, or after the start of the open one, is covered; outside every outage
    // it needs a reason (OCD-081, decided 2026-11-10), at most 500 characters; with no outage recorded nothing is asked;
    // the open outage is offered first, then the latest ended.
    [Fact]
    public void PaperTime_IsCheckedAgainstRecordedOutages()
    {
        var episodes = new[] { Ep(1, -48, -44, "Network outage"), Ep(2, -3, null) };
        Assert.Equal(1, DowntimeEpisodeRule.Covering(episodes, Now.AddHours(-46))!.Id);
        Assert.Equal(2, DowntimeEpisodeRule.Covering(episodes, Now.AddHours(-1))!.Id);
        Assert.Null(DowntimeEpisodeRule.Covering(episodes, Now.AddHours(-10)));

        Assert.Equal(RuleSeverity.Pass, DowntimeEpisodeRule.CheckPaperTime(episodes, Now.AddHours(-46), null).Severity);
        var outside = DowntimeEpisodeRule.CheckPaperTime(episodes, Now.AddHours(-10), "  ");
        Assert.Equal((RuleSeverity.HardStop, DowntimeEpisodeRule.OutsideCode), (outside.Severity, outside.Code));
        Assert.Contains("OCD-081", outside.Message);
        Assert.Equal(RuleSeverity.Pass, DowntimeEpisodeRule.CheckPaperTime(episodes, Now.AddHours(-10), "Paper from the earlier outage").Severity);
        Assert.Equal(RuleSeverity.HardStop, DowntimeEpisodeRule.CheckPaperTime(episodes, Now.AddHours(-10), new string('x', 501)).Severity);
        Assert.True(DowntimeEpisodeRule.IsOutside(episodes, Now.AddHours(-10)));
        Assert.Equal(RuleSeverity.Pass, DowntimeEpisodeRule.CheckPaperTime([], Now.AddHours(-10), null).Severity);

        Assert.Equal(2, DowntimeEpisodeRule.Current(episodes)!.Id);
        Assert.Equal(3, DowntimeEpisodeRule.Current([Ep(1, -48, -44), Ep(3, -10, -8)])!.Id);
        Assert.Equal("Issued on paper during downtime #1: Network outage", DowntimeEpisodeRule.BackEntryReason(episodes[0]));
    }

    // TEST-BB-843 (continued): outages touching end to start do not overlap; one inside another does.
    [Fact]
    public void Validate_AllowsAdjacentOutages()
    {
        var others = new[] { Ep(1, -10, -8) };
        Assert.Empty(DowntimeEpisodeRule.Validate(Now.AddHours(-8), Now.AddHours(-6), "Next", Now, others));
        Assert.Contains(DowntimeEpisodeRule.Validate(Now.AddHours(-9), Now.AddHours(-8.5), "Inside", Now, others),
            r => r.Code == DowntimeEpisodeRule.OverlapCode);
        Assert.Contains(DowntimeEpisodeRule.Validate(Now.AddHours(-9), null, "x", Now, others),
            r => r.Code == DowntimeEpisodeRule.OverlapCode);
        Assert.Contains(DowntimeEpisodeRule.Validate(Now.AddHours(-20), Now.AddHours(-19), new string('x', 201), Now, others),
            r => r.Code == DowntimeEpisodeRule.DescriptionCode);
    }
}
