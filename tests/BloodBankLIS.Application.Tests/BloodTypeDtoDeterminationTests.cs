using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Tests;

// TEST-BB-372: the chart header decides "second ABO/Rh on file" exactly as issue does.
public class BloodTypeDtoDeterminationTests
{
    private static PatientBloodTypeHistory Row(
        bool current,
        long? specimenId,
        BloodTypeSource source = BloodTypeSource.TestResult,
        ManualTypeDocumentation? documentation = null,
        bool invalidated = false,
        AboGroup abo = AboGroup.A) => new()
    {
        PatientId = 1,
        Abo = abo,
        RhD = RhType.Positive,
        Source = source,
        SpecimenId = specimenId,
        IsCurrent = current,
        ManualDocumentation = documentation,
        InvalidatedUtc = invalidated ? new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc) : null
    };

    public static TheoryData<string, PatientBloodTypeHistory[], bool> Cases => new()
    {
        { "two specimens", [Row(true, 10), Row(false, 11)], true },
        { "same specimen twice", [Row(true, 10), Row(false, 10)], false },
        { "undocumented manual entry", [Row(true, 10), Row(false, null, BloodTypeSource.ManualEntry, ManualTypeDocumentation.NotDocumented)], false },
        { "documented outside source", [Row(true, 10), Row(false, null, BloodTypeSource.ManualEntry, ManualTypeDocumentation.OutsideSource)], true },
        { "second type invalidated", [Row(true, 10), Row(false, 11, invalidated: true)], false },
        { "discordant second type", [Row(true, 10), Row(false, 11, abo: AboGroup.O)], false },
        { "only one type", [Row(true, 10)], false },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void ChartDeterminations_MatchTheServer(string _, PatientBloodTypeHistory[] rows, bool expected)
    {
        var server = SecondAboDeterminationRule.HasSecondConcordant(SecondAboDeterminationRule.FromHistory(rows));
        var chart = SecondAboDeterminationRule.HasSecondConcordant(BloodTypeDto.Determinations(rows.Select(BloodTypeDto.From)));

        Assert.Equal(expected, server);
        Assert.Equal(server, chart);
    }

    // TEST-BB-373: the chart's ABO/Rh discrepancy banner sees the same history as the issue gate.
    public static TheoryData<string, PatientBloodTypeHistory[], bool> DiscrepancyCases => new()
    {
        { "agreeing history", [Row(true, 10), Row(false, 11)], false },
        { "disagreeing history", [Row(true, 10), Row(false, 11, abo: AboGroup.O)], true },
        { "disagreeing type invalidated", [Row(true, 10), Row(false, 11, invalidated: true, abo: AboGroup.O)], false },
        { "undocumented manual entry disagrees", [Row(true, 10), Row(false, null, BloodTypeSource.ManualEntry, ManualTypeDocumentation.NotDocumented, abo: AboGroup.B)], true },
        { "no current type", [Row(false, 11, abo: AboGroup.O)], false },
    };

    [Theory]
    [MemberData(nameof(DiscrepancyCases))]
    public void ChartDiscrepancy_MatchesTheIssueGate(string _, PatientBloodTypeHistory[] rows, bool expected)
    {
        var server = IssueGate.HasUnresolvedAboRhDiscrepancy(IssueGate.DiscrepancyInput(rows));
        var chart = IssueGate.HasUnresolvedAboRhDiscrepancy(BloodTypeDto.DiscrepancyInput(rows.Select(BloodTypeDto.From)));

        Assert.Equal(expected, server);
        Assert.Equal(server, chart);
        Assert.Equal(expected, IssueGate.PatientBannerNotice(BloodTypeDto.DiscrepancyInput(rows.Select(BloodTypeDto.From))) is not null);
    }

    [Fact]
    public void TheDto_CarriesTheSpecimen()
    {
        Assert.Equal(10, BloodTypeDto.From(Row(true, 10)).SpecimenId);
        Assert.Null(BloodTypeDto.From(Row(true, null, BloodTypeSource.ManualEntry, ManualTypeDocumentation.OutsideSource)).SpecimenId);
    }
}
