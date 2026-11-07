using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-541: the lots a result was tested with are the open, unexpired lots of active reagents that list its test;
// two open lots of one reagent are both recorded; the test list is stored upper-case and comma-separated.
public class ReagentLotUseRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TestCodes_AreParsedAndNormalized()
    {
        Assert.Equal(["ABORH", "ABSC"], ReagentLotUseRule.ParseTestCodes(" aborh; ABSC,absc "));
        Assert.Equal("ABORH, ABSC", ReagentLotUseRule.NormalizeTestCodes("aborh absc"));
        Assert.Null(ReagentLotUseRule.NormalizeTestCodes(" , "));
    }

    [Fact]
    public void LotsInUse_AreOpenUnexpiredLotsOfActiveReagentsForTheTest()
    {
        Reagent[] reagents =
        [
            new() { Id = 1, Code = "AHG", TestCodes = "ABSC, DAT" },
            new() { Id = 2, Code = "ANTI-A", TestCodes = "ABORH" },
            new() { Id = 3, Code = "OLD-AHG", TestCodes = "DAT", IsActive = false }
        ];
        ReagentLot[] lots =
        [
            new() { Id = 10, ReagentId = 1, LotNumber = "AHG-2", State = ReagentLotState.Opened, ExpiresUtc = Now.AddDays(5) },
            new() { Id = 11, ReagentId = 1, LotNumber = "AHG-1", State = ReagentLotState.Opened, ExpiresUtc = Now.AddDays(1) },
            new() { Id = 12, ReagentId = 1, LotNumber = "AHG-0", State = ReagentLotState.Opened, ExpiresUtc = Now.AddMinutes(-1) },
            new() { Id = 13, ReagentId = 1, LotNumber = "AHG-3", State = ReagentLotState.Received, ExpiresUtc = Now.AddDays(90) },
            new() { Id = 14, ReagentId = 1, LotNumber = "AHG-X", State = ReagentLotState.Closed, ExpiresUtc = Now.AddDays(90) },
            new() { Id = 20, ReagentId = 2, LotNumber = "AA-1", State = ReagentLotState.Opened, ExpiresUtc = Now.AddDays(5) },
            new() { Id = 30, ReagentId = 3, LotNumber = "OLD-1", State = ReagentLotState.Opened, ExpiresUtc = Now.AddDays(5) }
        ];

        Assert.Equal(["AHG-1", "AHG-2"], ReagentLotUseRule.LotsInUse("dat", reagents, lots, Now).Select(l => l.LotNumber));
        Assert.Equal(["AA-1"], ReagentLotUseRule.LotsInUse("ABORH", reagents, lots, Now).Select(l => l.LotNumber));
        Assert.Empty(ReagentLotUseRule.LotsInUse("XM", reagents, lots, Now));
        Assert.Empty(ReagentLotUseRule.LotsInUse(null, reagents, lots, Now));
    }

    // TEST-BB-544: entry warnings say when a reagent used for the test has no open lot, and when a recorded lot has no
    // passed QC today; they never stop the result.
    [Fact]
    public void EntryWarnings_NameMissingLotsAndQc()
    {
        Reagent[] reagents =
        [
            new() { Id = 1, Code = "AHG", TestCodes = "ABSC" },
            new() { Id = 2, Code = "SCREEN-CELLS", TestCodes = "ABSC" },
            new() { Id = 3, Code = "ANTI-A", TestCodes = "ABORH" }
        ];
        ReagentLot[] inUse = [new() { Id = 10, ReagentId = 1, LotNumber = "AHG-1" }, new() { Id = 11, ReagentId = 1, LotNumber = "AHG-2" }];

        var warnings = ReagentLotUseRule.EntryWarnings("absc", reagents, inUse, new HashSet<long> { 10 });

        Assert.All(warnings, w => Assert.Equal(RuleSeverity.Warning, w.Severity));
        Assert.Equal(
            [(ReagentLotUseRule.QcNotPassedCode, "AHG lot AHG-2 has no passed QC recorded today."),
             (ReagentLotUseRule.NoOpenLotCode, "No open lot of SCREEN-CELLS is recorded for ABSC, so this result records no SCREEN-CELLS lot. Open the lot in use on Reagent QC.")],
            warnings.Select(w => (w.Code, w.Message)));
        Assert.Empty(ReagentLotUseRule.EntryWarnings("XM", reagents, [], new HashSet<long>()));
    }
}
