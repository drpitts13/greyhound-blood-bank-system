using BloodBankLIS.Application.Results;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Tests;

// TEST-BB-566: the worklist names the reflex lines that appeared since it last loaded, with the result that added them.
public class ReflexNoteTests
{
    private static TestWorkItemDto Line(long id, string testCode, string order, string? reflexFrom = null) =>
        new(id, 1, 1, "Bench, Pat", "MRN1", testCode, testCode, order, OrderPriority.Routine, DateTime.UtcNow, "ACC1", 1,
            SpecimenStatus.Accepted, DateTime.UtcNow.AddDays(2), false, "O+", false, null, null, null, null, null,
            null, true, null, ReflexFrom: reflexFrom);

    [Fact]
    public void NewReflexNote_NamesOnlyReflexLinesThatJustAppeared()
    {
        var before = new[] { Line(1, "ABSC", "ORD1"), Line(2, "DAT", "ORD2", "ABSC Positive") };
        var after = new[] { Line(2, "DAT", "ORD2", "ABSC Positive"), Line(3, "ABID", "ORD1", "ABSC Positive"), Line(4, "XM", "ORD1") };

        Assert.Equal("Reflex added: ABID on ORD1 (from ABSC Positive).", TestWorklistView.NewReflexNote(before, after));
        Assert.Null(TestWorklistView.NewReflexNote(after, after));
    }

    [Fact]
    public void ReflexLabel_IsTheTriggerTestAndValue()
    {
        Assert.Equal("ABSC Positive", TestWorklistView.ReflexLabel(" ABSC ", "Positive "));
        Assert.Equal("DAT", TestWorklistView.ReflexLabel("DAT", " "));
        Assert.Null(TestWorklistView.ReflexLabel(null, "Positive"));
    }
}
