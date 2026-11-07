using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-496: the daily QC page lists the last 14 days of QC (newest first, with reagent and lot), offers the expected
// result last recorded for the reagent, and selects a lot by scan only when exactly one open lot carries the number.
public class ReagentQcHistoryTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private ReagentQcService Service(BloodBankDbContext c) =>
        new(new EfRepository<Reagent>(c), new EfRepository<ReagentLot>(c), new EfRepository<ReagentDailyQc>(c),
            c, _factory.Clock, _factory.CurrentUser, new AuditWriter(c, _factory.Clock, _factory.CurrentUser));

    [Fact]
    public async Task RecentQc_NewestFirst_WithinTheWindow_AndTheFormHelpers()
    {
        var now = _factory.Clock.UtcNow;
        await using var c = _factory.Create();
        var antiA = new Reagent { Code = "ANTI-A", Name = "Anti-A" };
        var antiB = new Reagent { Code = "ANTI-B", Name = "Anti-B" };
        c.Reagents.AddRange(antiA, antiB);
        await c.SaveChangesAsync();
        var lotA1 = new ReagentLot { ReagentId = antiA.Id, LotNumber = "A-100", ReceivedUtc = now.AddDays(-30), ExpiresUtc = now.AddDays(60), State = ReagentLotState.Opened };
        var lotA2 = new ReagentLot { ReagentId = antiA.Id, LotNumber = "A-200", ReceivedUtc = now.AddDays(-2), ExpiresUtc = now.AddDays(90), State = ReagentLotState.Opened };
        var lotB = new ReagentLot { ReagentId = antiB.Id, LotNumber = "B-100", ReceivedUtc = now.AddDays(-30), ExpiresUtc = now.AddDays(60), State = ReagentLotState.Opened };
        c.ReagentLots.AddRange(lotA1, lotA2, lotB);
        await c.SaveChangesAsync();
        ReagentDailyQc Qc(ReagentLot lot, string expected, string observed, int daysAgo) => new()
        {
            ReagentLotId = lot.Id, QcDateUtc = now.AddDays(-daysAgo).Date, ExpectedResult = expected, ObservedResult = observed,
            Passed = expected == observed, PerformedBy = "tech1", PerformedUtc = now.AddDays(-daysAgo)
        };
        c.ReagentDailyQcs.AddRange(
            Qc(lotA1, "Old 3+", "Old 3+", 20),
            Qc(lotA1, "4+", "4+", 3),
            Qc(lotB, "Negative", "1+", 1));
        await c.SaveChangesAsync();

        var recent = await Service(c).ListRecentQcAsync();
        Assert.Equal(["B-100", "A-100"], recent.Select(q => q.LotNumber));
        Assert.Equal(["ANTI-B", "ANTI-A"], recent.Select(q => q.ReagentCode));
        Assert.False(recent[0].Passed);

        // A new lot of the same reagent is offered the last expected result recorded for that reagent.
        Assert.Equal("4+", ReagentQcHistory.LastExpected(antiA.Id, recent));
        Assert.Null(ReagentQcHistory.LastExpected(999, recent));

        var open = (await Service(c).ListLotsAsync()).ToList();
        Assert.Equal(lotA2.Id, ReagentQcHistory.LotForScan(" a-200 ", open)?.Id);
        Assert.Null(ReagentQcHistory.LotForScan("A-999", open));
        Assert.Null(ReagentQcHistory.LotForScan("A-100", [.. open, open.Single(l => l.LotNumber == "A-100") with { Id = 9999 }]));
    }
}
