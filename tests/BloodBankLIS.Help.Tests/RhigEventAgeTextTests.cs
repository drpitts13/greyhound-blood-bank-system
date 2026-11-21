using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class RhigEventAgeTextTests
{
    // TEST-BB-1244: an open RhIG evaluation with RhIG not given says how long ago the event was (end-user testing pass
    // 222); a given, completed, or not-candidate one says nothing.
    [Fact]
    public void For_SaysHowLongAgoTheEventWasWhileRhigIsDue()
    {
        var evt = new DateTime(2026, 11, 7, 13, 27, 0, DateTimeKind.Utc);
        RhigEvaluationDto Eval(RhigDecision decision, DateTime? completed = null, DateTime? given = null) =>
            new(1, 16, RhigContext.Postpartum, evt, RhType.Positive, FetalBleedScreen.NotDone, null, decision, null, null, "tech",
                evt.AddHours(1), completed is null ? null : "lead", completed, [], GivenUtc: given);
        var now = evt.AddDays(9).AddHours(23);

        Assert.Equal("9 days 23 hours ago, RhIG not given", RhigEventAgeText.For(Eval(RhigDecision.Undetermined), now));
        Assert.Equal("9 days 23 hours ago, RhIG not given", RhigEventAgeText.For(Eval(RhigDecision.Candidate), now));
        Assert.Null(RhigEventAgeText.For(Eval(RhigDecision.NotCandidate), now));
        Assert.Null(RhigEventAgeText.For(Eval(RhigDecision.Candidate, given: evt.AddHours(20)), now));
        Assert.Null(RhigEventAgeText.For(Eval(RhigDecision.Candidate, completed: evt.AddDays(1)), now));
    }
}
