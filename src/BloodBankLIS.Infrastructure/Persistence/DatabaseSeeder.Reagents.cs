using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

/// <summary>
/// Demo reagents with the tests they are used for and one open lot each, and the demo manual ABO/Rh and screen results
/// recorded against those lots, so the Reagent QC lot lookback has something to show. Added once, only while the
/// database has no reagents (a site's own reagents are never touched). Lot numbers are examples.
/// </summary>
public static partial class DatabaseSeeder
{
    private static readonly (string Code, string Name, string Tests, string Lot)[] DemoReagents =
    [
        ("ANTI-A", "Anti-A typing serum", "ABORH", "AA-24117"),
        ("ANTI-B", "Anti-B typing serum", "ABORH", "AB-24118"),
        ("ANTI-D", "Anti-D typing serum", "ABORH", "AD-24092"),
        ("A1-CELLS", "A1 reverse grouping cells", "ABORH", "A1-24301"),
        ("B-CELLS", "B reverse grouping cells", "ABORH", "BC-24302"),
        ("SCREEN-CELLS", "Antibody screening cells (3-cell)", "ABSC", "SC-24415"),
        ("AHG", "Anti-human globulin (polyspecific)", "ABSC, DAT, XM", "AHG-24033")
    ];

    private static async Task EnsureReagentLotDemoAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.Reagents.AnyAsync(ct))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var lots = new List<(Reagent Reagent, ReagentLot Lot)>();
        foreach (var (code, name, tests, lotNumber) in DemoReagents)
        {
            var reagent = new Reagent { Code = code, Name = name, IsActive = true, TestCodes = tests };
            context.Reagents.Add(reagent);
            await context.SaveChangesAsync(ct);
            var lot = new ReagentLot
            {
                ReagentId = reagent.Id,
                LotNumber = lotNumber,
                ReceivedUtc = now.AddDays(-20),
                OpenedUtc = now.AddDays(-14),
                ExpiresUtc = now.AddDays(75),
                State = ReagentLotState.Opened
            };
            context.ReagentLots.Add(lot);
            lots.Add((reagent, lot));
        }

        await context.SaveChangesAsync(ct);

        // The demo's manual results from the last two weeks were tested while these lots were open.
        var since = now.AddDays(-14);
        var results = await context.TestResults
            .Where(r => r.Source == ResultSource.Manual && r.EnteredUtc != null && r.EnteredUtc >= since
                && (r.TestCode == "ABORH" || r.TestCode == "ABSC" || r.TestCode == "DAT" || r.TestCode == "XM"))
            .ToListAsync(ct);
        foreach (var result in results)
        {
            foreach (var (reagent, lot) in lots.Where(l => Domain.Rules.ReagentLotUseRule.UsedFor(l.Reagent, result.TestCode)))
            {
                context.TestResultReagentLots.Add(new TestResultReagentLot
                {
                    TestResultId = result.Id,
                    ReagentLotId = lot.Id,
                    ReagentCode = reagent.Code,
                    LotNumber = lot.LotNumber,
                    RecordedUtc = result.EnteredUtc ?? now
                });
            }
        }

        await context.SaveChangesAsync(ct);
    }
}
