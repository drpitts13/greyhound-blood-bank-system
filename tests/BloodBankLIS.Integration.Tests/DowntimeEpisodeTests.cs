using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

/// <summary>
/// Recorded outages (Cycle 662): the window worked on paper is recorded and ended, audited, and the paper issues
/// back-entered inside it are counted, so recovery does not depend on anyone remembering when the system was down.
/// </summary>
public class DowntimeEpisodeTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private DowntimeEpisodeService Service(BloodBankDbContext c) =>
        new(new EfRepository<DowntimeEpisode>(c), new EfRepository<Issue>(c),
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser), c, _factory.Clock, _factory.CurrentUser);

    private DateTime Now => _factory.Clock.UtcNow;

    // TEST-BB-844: an outage is recorded open, ended when the system is back, and each change is audited; ending it again,
    // or an unknown outage, is refused.
    [Fact]
    public async Task Record_ThenEnd_IsAudited()
    {
        await using var c = _factory.Create();
        var recorded = await Service(c).RecordAsync(new RecordDowntimeEpisodeRequest(Now.AddHours(-4), null, "  LIS upgrade  "));
        Assert.True(recorded.Succeeded, recorded.Error);
        var id = recorded.Value!.Id;
        Assert.Equal(("LIS upgrade", (DateTime?)null, "tech-test"), (recorded.Value.Description, recorded.Value.EndedUtc, recorded.Value.RecordedBy));
        Assert.Equal($"Issued on paper during downtime #{id}: LIS upgrade", recorded.Value.BackEntryReason);

        var ended = await Service(c).EndAsync(id, new EndDowntimeEpisodeRequest(Now.AddHours(-1)));
        Assert.True(ended.Succeeded, ended.Error);
        var row = await c.DowntimeEpisodes.AsNoTracking().SingleAsync(e => e.Id == id);
        Assert.Equal((Now.AddHours(-1), "tech-test"), (row.EndedUtc!.Value, row.EndedBy));

        var audits = await c.AuditEvents.AsNoTracking().Where(a => a.EntityType == nameof(DowntimeEpisode) && a.EntityId == id).ToListAsync();
        Assert.Contains(audits, a => a.EventType == AuditEventType.Create && a.NewValueJson!.Contains("LIS upgrade"));
        Assert.Contains(audits, a => a.EventType == AuditEventType.Update && a.Reason == "Outage ended");

        var again = await Service(c).EndAsync(id, new EndDowntimeEpisodeRequest(Now));
        Assert.Contains("already ended", again.Error);
        Assert.Equal("Outage not found.", (await Service(c).EndAsync(9999, new EndDowntimeEpisodeRequest(Now))).Error);
    }

    // TEST-BB-845: refused, with nothing saved: no description, a start in the future, an end before the start, an end in
    // the future, an overlap with another outage (and a second open one); ending at a time that runs into a later outage.
    [Fact]
    public async Task Record_RefusesWhatCannotBeTrue()
    {
        await using var c = _factory.Create();
        Assert.True((await Service(c).RecordAsync(new RecordDowntimeEpisodeRequest(Now.AddDays(-2), Now.AddDays(-2).AddHours(3), "Network outage"))).Succeeded);
        var open = await Service(c).RecordAsync(new RecordDowntimeEpisodeRequest(Now.AddHours(-2), null, "LIS down"));
        Assert.True(open.Succeeded);

        async Task<string> Refused(RecordDowntimeEpisodeRequest r)
        {
            var result = await Service(c).RecordAsync(r);
            Assert.False(result.Succeeded);
            return string.Join(" | ", result.Evaluation!.HardStops.Select(h => h.Code));
        }

        Assert.StartsWith(DowntimeEpisodeRule.DescriptionCode, await Refused(new(Now.AddDays(-5), Now.AddDays(-5).AddHours(1), " ")));
        Assert.StartsWith(DowntimeEpisodeRule.TimeCode, await Refused(new(Now.AddHours(1), null, "Future")));
        Assert.StartsWith(DowntimeEpisodeRule.TimeCode, await Refused(new(Now.AddDays(-5), Now.AddDays(-6), "Backwards")));
        Assert.StartsWith(DowntimeEpisodeRule.TimeCode, await Refused(new(Now.AddDays(-5), Now.AddHours(2), "Ends later")));
        Assert.StartsWith(DowntimeEpisodeRule.OverlapCode, await Refused(new(Now.AddDays(-2).AddHours(1), Now.AddDays(-1), "Overlaps")));
        Assert.StartsWith(DowntimeEpisodeRule.OverlapCode, await Refused(new(Now.AddMinutes(-30), null, "Second open")));
        Assert.Equal(2, await c.DowntimeEpisodes.CountAsync());

        // Recorded after the fact, an earlier outage cannot be ended across the open one.
        var earlier = await Service(c).RecordAsync(new RecordDowntimeEpisodeRequest(Now.AddDays(-1), Now.AddDays(-1).AddHours(1), "Planned maintenance"));
        Assert.True(earlier.Succeeded);
        var reopened = new DowntimeEpisode { StartedUtc = Now.AddHours(-10), Description = "Left open", CreatedBy = "tech-test", CreatedUtc = Now };
        c.DowntimeEpisodes.Add(reopened);
        await c.SaveChangesAsync();
        var across = await Service(c).EndAsync(reopened.Id, new EndDowntimeEpisodeRequest(Now));
        Assert.Contains(across.Evaluation!.HardStops, h => h.Code == DowntimeEpisodeRule.OverlapCode);
    }

    // TEST-BB-846: the list, newest first, counts the paper issues back-entered with an issue time inside each outage and
    // how many of them are still issued; an issue entered normally, or back-entered outside the window, is not counted.
    [Fact]
    public async Task List_CountsBackEntriesInsideEachOutage()
    {
        await using var c = _factory.Create();
        var older = await Service(c).RecordAsync(new RecordDowntimeEpisodeRequest(Now.AddDays(-3), Now.AddDays(-3).AddHours(4), "Network outage"));
        var latest = await Service(c).RecordAsync(new RecordDowntimeEpisodeRequest(Now.AddHours(-6), Now.AddHours(-2), "LIS upgrade"));

        var product = new ProductType { ProductCode = "RBC-DTE", Name = "Outage RBC", ComponentClass = ComponentClass.RedBloodCells };
        var patient = new Patient { MedicalRecordNumber = "MRN-DTE", LastName = "Outage", FirstName = "Paper", DateOfBirth = new DateOnly(1970, 1, 1) };
        c.ProductTypes.Add(product);
        c.Patients.Add(patient);
        await c.SaveChangesAsync();

        var cases = new (DateTime issued, bool backEntry, IssueStatus status)[]
        {
            (Now.AddHours(-5), true, IssueStatus.Issued),
            (Now.AddHours(-3), true, IssueStatus.Transfused),
            (Now.AddHours(-4), false, IssueStatus.Issued),
            (Now.AddHours(-1), true, IssueStatus.Issued),
            (Now.AddDays(-3).AddHours(1), true, IssueStatus.Transfused)
        };
        for (var n = 0; n < cases.Length; n++)
        {
            var unit = new BloodUnit
            {
                UnitNumber = $"U-DTE-{n}", ProductTypeId = product.Id, Abo = AboGroup.O, RhD = RhType.Positive,
                ExpiresUtc = Now.AddDays(20), Status = UnitStatus.Issued
            };
            c.BloodUnits.Add(unit);
            await c.SaveChangesAsync();
            c.Issues.Add(new Issue
            {
                BloodProductId = unit.Id, PatientId = patient.Id, IssuedUtc = cases[n].issued, IssuedBy = "tech-test",
                Status = cases[n].status, DowntimeBackEntry = cases[n].backEntry
            });
        }

        await c.SaveChangesAsync();

        var list = await Service(c).ListAsync();
        Assert.Equal([latest.Value!.Id, older.Value!.Id], list.Select(e => e.Id));
        Assert.Equal((2, 1), (list[0].BackEntries, list[0].BackEntriesOpen));
        Assert.Equal((1, 0), (list[1].BackEntries, list[1].BackEntriesOpen));
    }
}
