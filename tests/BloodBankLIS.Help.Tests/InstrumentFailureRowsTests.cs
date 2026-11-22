using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class InstrumentFailureRowsTests
{
    private static readonly DateTime T0 = new(2026, 11, 12, 19, 45, 0, DateTimeKind.Utc);

    private static InstrumentMessageSummaryDto Message(long id, string? specimen, string? error, InstrumentMessageStatus status = InstrumentMessageStatus.Failed,
        DateTime? received = null, DateTime? resolved = null) =>
        new(id, 1, Hl7Direction.Inbound, InstrumentMessageKind.Result, status, received ?? T0.AddMinutes(id), null, specimen, "MRN0022", error, resolved);

    // TEST-BB-1163: an open failure's row gives its reason (shortened), and a later open failure with the same specimen
    // and reason says it repeats the first; resolved, processed and different-reason messages are not repeats
    // (end-user testing pass 141).
    [Fact]
    public void Rows_GiveTheReason_AndNameARepeatedFailure()
    {
        const string notOrdered = "Test DAT is not ordered on specimen ACC-DEMO-RCV1 (INST-NOT-ORDERED): not posted.";
        var first = Message(8, "ACC-DEMO-RCV1", notOrdered);
        var repeat = Message(9, "acc-demo-rcv1", notOrdered);
        var other = Message(7, "ACC-UAT-010", "Patient ID MRN0022 on the result is not the patient of specimen ACC-UAT-010 (INST-PATIENT).");
        var resolved = Message(10, "ACC-DEMO-RCV1", notOrdered, resolved: T0.AddHours(2));
        var processed = Message(11, "ACC-DEMO-RCV1", null, InstrumentMessageStatus.Processed);

        Assert.Equal(notOrdered, InstrumentFailureRows.Reason(first));
        Assert.Null(InstrumentFailureRows.Reason(resolved));
        Assert.Null(InstrumentFailureRows.Reason(processed));
        var longText = new string('x', 300);
        var shortened = InstrumentFailureRows.Reason(Message(12, "ACC-X", longText))!;
        Assert.Equal(InstrumentFailureRows.ReasonLength, shortened.Length);
        Assert.EndsWith("…", shortened);

        var repeats = InstrumentFailureRows.RepeatOf([repeat, first, other, resolved, processed]);
        Assert.Equal(new Dictionary<long, long> { [9] = 8 }, repeats);
    }
}
