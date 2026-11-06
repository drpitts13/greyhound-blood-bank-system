using BloodBankLIS.Application.Compliance;

namespace BloodBankLIS.Application.Tests;

public class ShiftHandoffSummaryTests
{
    private static ShiftHandoffDto Handoff(int toVerify = 0, int lapsed = 0, int nearExpiry = 0, int nearReserved = 0) =>
        new(1, DateTime.UtcNow, "tech1", null, "notes", 0, 0, 2, 1, 0, 5, 1, 0, 0, null, null,
            ResultsAwaitingVerification: toVerify, ReservationsLapsed: lapsed, NearExpiryUnits: nearExpiry, NearExpiryReserved: nearReserved);

    // TEST-BB-429: the handoff line names results to verify, lapsed reservations, and near-expiry units, and stays quiet at zero.
    [Fact]
    public void Summary_NamesTheNewCounts_OnlyWhenAboveZero()
    {
        var quiet = Handoff().QueueSummary;
        Assert.DoesNotContain("to verify", quiet);
        Assert.DoesNotContain("past reservation", quiet);
        Assert.DoesNotContain("near expiry", quiet);

        var busy = Handoff(toVerify: 3, lapsed: 2, nearExpiry: 4, nearReserved: 1).QueueSummary;
        Assert.Contains("tests 5, 3 to verify", busy);
        Assert.Contains("transit 1; 2 past reservation", busy);
        Assert.Contains("near expiry 4, 1 reserved", busy);
    }

    // TEST-BB-460: the handoff line names coolers still out and how many were not checked or had differences.
    [Fact]
    public void Summary_NamesCoolersOut_AndTheirChecks()
    {
        Assert.DoesNotContain("coolers out", Handoff().QueueSummary);
        var line = (Handoff() with { CoolersInTransit = 3, CoolersNotChecked = 2, CoolersWithDifferences = 1 }).QueueSummary;
        Assert.Contains("transit 1; coolers out 3, 2 not checked, 1 checked with differences", line);
        Assert.Contains("coolers out 1;", (Handoff() with { CoolersInTransit = 1 }).QueueSummary);
    }

    // TEST-BB-474: the handoff line ends with expired units still on a shelf, in capitals, and is quiet at zero.
    [Fact]
    public void Summary_NamesExpiredUnitsOnTheShelf()
    {
        Assert.DoesNotContain("EXPIRED", Handoff().QueueSummary);
        Assert.EndsWith("; EXPIRED ON SHELF 2, to discard", (Handoff() with { ExpiredOnShelf = 2 }).QueueSummary);
    }
}
