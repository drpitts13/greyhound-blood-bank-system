using BloodBankLIS.Application.Results;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Tests;

public class TestWorkItemBenchActionTests
{
    private static TestWorkItemDto Item(string testCode, long? resultId, ResultStatus? status, ResultSource? source, string? enteredBy, bool canEnter = true) =>
        new(1, 1, 1, "Bench, Pat", "MRN1", testCode, testCode, "ORD1", OrderPriority.Routine, DateTime.UtcNow, "ACC1", 1,
            SpecimenStatus.Accepted, DateTime.UtcNow.AddDays(2), false, "O+", false, null, resultId, status, "Negative", null,
            source, canEnter, canEnter ? null : "Blocked", CurrentResultEnteredBy: enteredBy);

    // TEST-BB-427: a manually entered result waiting for verification says Verify on the bench, not Enter.
    [Fact]
    public void AnEnteredResult_SaysVerify_WhateverItsSource()
    {
        Assert.Equal("Enter", Item("DAT", null, null, null, null).BenchActionLabel);
        Assert.Equal("Verify", Item("DAT", 7, ResultStatus.Entered, ResultSource.Manual, "tech1").BenchActionLabel);
        Assert.Equal("Verify", Item("DAT", 7, ResultStatus.PendingVerification, ResultSource.Interface, null).BenchActionLabel);
        Assert.Equal("Enter", Item("DAT", 7, ResultStatus.Verified, ResultSource.Manual, "tech1").BenchActionLabel);
        Assert.Equal("Blocked", Item("DAT", 7, ResultStatus.Entered, ResultSource.Manual, "tech1", canEnter: false).BenchActionLabel);
    }

    [Fact]
    public void TheEntererOfAnAboRh_SeesReview_OthersSeeVerify_AndThePromptNamesTheOrigin()
    {
        var abo = Item("ABORH", 7, ResultStatus.Entered, ResultSource.Manual, "tech1");
        Assert.Equal("Review", abo.BenchActionLabelFor("tech1"));
        Assert.Equal("Verify", abo.BenchActionLabelFor("tech2"));
        Assert.Equal("The value entered by tech1", abo.AwaitingValueOrigin);
        Assert.Equal("The instrument value", Item("DAT", 7, ResultStatus.PendingVerification, ResultSource.Instrument, null).AwaitingValueOrigin);
    }
}
