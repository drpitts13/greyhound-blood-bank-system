using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Tests;

public class AllocationTimelineNoteTests
{
    // TEST-BB-709: a reservation's outcome reads in words on the timelines, and one ended by the unit's expiry gives the
    // sweep's reason (before: "Now Expired" with no reason, and "Now Consumed" for an issued unit).
    [Theory]
    [InlineData(AllocationStatus.Reserved, null, "Still reserved")]
    [InlineData(AllocationStatus.Consumed, null, "Issued")]
    [InlineData(AllocationStatus.Released, "Unit damaged", "Released: Unit damaged")]
    [InlineData(AllocationStatus.Released, " ", "Released")]
    [InlineData(AllocationStatus.Expired, "Unit W0001 expired at 2026-11-09 14:30 UTC.", "Ended: Unit W0001 expired at 2026-11-09 14:30 UTC.")]
    [InlineData(AllocationStatus.Expired, null, "Ended: the unit expired")]
    public void Outcome_ReadsInWords(AllocationStatus status, string? reason, string expected) =>
        Assert.Equal(expected, AllocationTimelineNote.Describe(status, reason));
}
