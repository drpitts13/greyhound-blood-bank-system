using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-670: one antibody worded the same in every warning (chart header, issue desk, reaction investigation):
// plain when found here and active, marked historical when no longer detectable, naming the reporting facility and date
// for an outside report, both notes together when both apply.
public class AntibodyHistoryEntryTests
{
    [Fact]
    public void HistoryEntry_OneWordingForEveryWarning()
    {
        Assert.Equal("Anti-K", OutsideAntibodyReportRule.HistoryEntry("Anti-K", true, null, null));
        Assert.Equal("Anti-E (historical / currently undetectable)", OutsideAntibodyReportRule.HistoryEntry("Anti-E", false, " ", null));
        Assert.Equal("Anti-Jka (reported by St. Mary's, identified 2025-02-01)",
            OutsideAntibodyReportRule.HistoryEntry("Anti-Jka", true, "St. Mary's", new DateOnly(2025, 2, 1)));
        Assert.Equal("Anti-Fya (reported by County General)", OutsideAntibodyReportRule.HistoryEntry("Anti-Fya", true, "County General", null));
        Assert.Equal("Anti-c (historical / currently undetectable; reported by County General, identified 2024-06-30)",
            OutsideAntibodyReportRule.HistoryEntry("Anti-c", false, "County General", new DateOnly(2024, 6, 30)));
    }
}
