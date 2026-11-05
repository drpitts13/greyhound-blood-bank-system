using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Tests;

public class DashboardQueueCatalogTests
{
    [Fact]
    public void Select_IssueCreate_IncludesIssueBoards_AndOmitsReactions()
    {
        var cards = DashboardQueueCatalog.Select(Has(PermissionCodes.IssueCreate));

        Assert.Contains(cards, card => card.Key == DashboardQueueCatalog.Retrospective && card.Href == "/issuing?desk=retro");
        Assert.Contains(cards, card => card.Key == DashboardQueueCatalog.InTransit);
        Assert.Contains(cards, card => card.Key == DashboardQueueCatalog.Ready);
        Assert.Contains(cards, card => card.Key == DashboardQueueCatalog.SecondAbo);
        Assert.Contains(cards, card => card.Key == DashboardQueueCatalog.Outstanding);
        Assert.Contains(cards, card => card.Key == DashboardQueueCatalog.Disposition);
        Assert.DoesNotContain(cards, card => card.Key == DashboardQueueCatalog.Reactions);
        Assert.DoesNotContain(cards, card => card.Key == DashboardQueueCatalog.Handoff);
        Assert.DoesNotContain(cards, card => card.Key == DashboardQueueCatalog.PendingTests);
    }

    [Fact]
    public void Select_TransfusionDocumentWithoutIssue_SeesIssuedBoardsOnly()
    {
        var cards = DashboardQueueCatalog.Select(Has(PermissionCodes.TransfusionDocument));

        Assert.Contains(cards, card => card.Key == DashboardQueueCatalog.Outstanding && card.Href == "/issuing?desk=issued");
        Assert.Contains(cards, card => card.Key == DashboardQueueCatalog.Disposition);
        Assert.DoesNotContain(cards, card => card.Key == DashboardQueueCatalog.InTransit);
        Assert.DoesNotContain(cards, card => card.Key == DashboardQueueCatalog.Retrospective);
    }

    [Fact]
    public void Select_ResultEnterAndSpecimenAccession_OpensThoseWorklists()
    {
        var cards = DashboardQueueCatalog.Select(Has(PermissionCodes.ResultEnter, PermissionCodes.SpecimenAccession));

        Assert.Contains(cards, card => card.Key == DashboardQueueCatalog.PendingTests && card.Href == "/test-worklist");
        Assert.Contains(cards, card => card.Key == DashboardQueueCatalog.PendingSpecimens && card.Href == "/test-worklist?tab=specimens");
    }

    [Fact]
    public void Select_SupervisorPermissions_IncludeTheQueue_AndOmitItOtherwise()
    {
        Assert.Contains(
            DashboardQueueCatalog.Select(Has(PermissionCodes.ResultVerify)),
            card => card.Key == DashboardQueueCatalog.Supervisor && card.Href == "/supervisor");
        Assert.Contains(
            DashboardQueueCatalog.Select(Has(PermissionCodes.InventoryRelease)),
            card => card.Key == DashboardQueueCatalog.Supervisor);
        Assert.DoesNotContain(
            DashboardQueueCatalog.Select(Has(PermissionCodes.ResultEnter)),
            card => card.Key == DashboardQueueCatalog.Supervisor);
    }

    [Fact]
    public void Select_ReactionAndHandoff_IncludeThoseCards()
    {
        var cards = DashboardQueueCatalog.Select(Has(PermissionCodes.ReactionInvestigate, PermissionCodes.ShiftHandoff));

        Assert.Contains(cards, card => card.Key == DashboardQueueCatalog.Reactions && card.Href == "/reactions");
        Assert.Contains(cards, card => card.Key == DashboardQueueCatalog.Handoff && card.Href == "/handoff");
    }

    // TEST-BB-330: audit read alone (auditor, read-only) does not get the handoff card.
    [Fact]
    public void Select_AuditReadOnly_OmitsHandoff() =>
        Assert.DoesNotContain(DashboardQueueCatalog.Select(Has(PermissionCodes.AuditRead)), card => card.Key == DashboardQueueCatalog.Handoff);

    // TEST-BB-423: near-expiry units are on the home screen for whoever handles inventory or issue.
    [Fact]
    public void Select_NearExpiry_ForInventoryOrIssue_OpensTheNearExpiryBoard()
    {
        foreach (var code in new[] { PermissionCodes.InventoryReceive, PermissionCodes.InventoryDiscard, PermissionCodes.IssueCreate, PermissionCodes.CompatibilityAllocate })
        {
            Assert.Contains(DashboardQueueCatalog.Select(Has(code)),
                card => card.Key == DashboardQueueCatalog.NearExpiry && card.Href == "/inventory?board=near");
        }

        Assert.DoesNotContain(DashboardQueueCatalog.Select(Has(PermissionCodes.ResultEnter)), card => card.Key == DashboardQueueCatalog.NearExpiry);
        Assert.Null(DashboardQueueCatalog.NearExpiryNote(0));
        Assert.Equal("2 reserved for patients", DashboardQueueCatalog.NearExpiryNote(2));
    }

    [Fact]
    public void Select_NoPermissions_OmitsEveryWorkCard()
    {
        Assert.Empty(DashboardQueueCatalog.Select(Has()));
    }

    [Fact]
    public void AttentionNote_StaysSilentAtZero_AndNamesAPositiveCount()
    {
        Assert.Null(DashboardQueueCatalog.AttentionNote(0, "past due", "past due"));
        Assert.Equal("1 past due", DashboardQueueCatalog.AttentionNote(1, "past due", "past due"));
        Assert.Equal("2 fatality follow-ups", DashboardQueueCatalog.AttentionNote(2, "fatality follow-up", "fatality follow-ups"));
        Assert.Null(DashboardQueueCatalog.HandoffNote(false));
        Assert.Equal("Needs acknowledgment", DashboardQueueCatalog.HandoffNote(true));
    }

    [Fact]
    public void IssuingDesk_OpensTheNamedBoard_AndKeepsIssueWhenAPatientIsPrefilled()
    {
        Assert.Equal("retro", IssuingDeskQuery.Resolve("retro", canIssue: true, canSeeIssued: true, hasPatientOrUnit: false));
        Assert.Equal("issued", IssuingDeskQuery.Resolve("issued", canIssue: false, canSeeIssued: true, hasPatientOrUnit: false));
        Assert.Equal("ready", IssuingDeskQuery.Resolve("retro", canIssue: false, canSeeIssued: true, hasPatientOrUnit: false));
        Assert.Equal("issue", IssuingDeskQuery.Resolve("retro", canIssue: true, canSeeIssued: true, hasPatientOrUnit: true));
        Assert.Equal("ready", IssuingDeskQuery.Resolve("unknown", canIssue: true, canSeeIssued: true, hasPatientOrUnit: false));
        Assert.Equal("ready", IssuingDeskQuery.Resolve(null, canIssue: true, canSeeIssued: true, hasPatientOrUnit: false));
    }

    // TEST-BB-296: the excursion card follows inventory receipt or deviation management.
    [Fact]
    public void Select_TemperatureExcursions_FollowsReceiveOrDeviationPermission()
    {
        Assert.Contains(
            DashboardQueueCatalog.Select(Has(PermissionCodes.InventoryReceive)),
            card => card.Key == DashboardQueueCatalog.TemperatureExcursions && card.Href == "/storage-temps");
        Assert.Contains(
            DashboardQueueCatalog.Select(Has(PermissionCodes.DeviationManage)),
            card => card.Key == DashboardQueueCatalog.TemperatureExcursions);
        Assert.DoesNotContain(
            DashboardQueueCatalog.Select(Has(PermissionCodes.ResultEnter)),
            card => card.Key == DashboardQueueCatalog.TemperatureExcursions);
    }

    // TEST-BB-401
    [Fact]
    public void InterfaceCards_FollowTheInterfacePermissions()
    {
        Assert.Contains(DashboardQueueCatalog.Select(Has(PermissionCodes.Hl7Manage)), c => c.Key == DashboardQueueCatalog.Hl7Errors && c.Href == "/hl7");
        Assert.Contains(DashboardQueueCatalog.Select(Has(PermissionCodes.InstrumentManage)), c => c.Key == DashboardQueueCatalog.InstrumentFailures && c.Href == "/instruments");
        var bench = DashboardQueueCatalog.Select(Has(PermissionCodes.ResultEnter));
        Assert.DoesNotContain(bench, c => c.Key is DashboardQueueCatalog.Hl7Errors or DashboardQueueCatalog.InstrumentFailures);
    }

    // TEST-BB-435: verifiers get a Results to verify card that counts only what they can verify now.
    [Fact]
    public void ToVerify_ForVerifiersOnly_CountsTheirOwnVerifiableResults_WithTheExpiryNote()
    {
        Assert.Contains(DashboardQueueCatalog.Select(Has(PermissionCodes.ResultVerify)),
            card => card.Key == DashboardQueueCatalog.ToVerify && card.Href == "/test-worklist?show=verify");
        Assert.DoesNotContain(DashboardQueueCatalog.Select(Has(PermissionCodes.ResultEnter)),
            card => card.Key == DashboardQueueCatalog.ToVerify);
        Assert.True(DashboardHome.InFocus(DashboardQueueCatalog.ToVerify, WorkFocus.Bench));
        Assert.True(DashboardHome.InFocus(DashboardQueueCatalog.ToVerify, WorkFocus.Supervisor));
        Assert.False(DashboardHome.InFocus(DashboardQueueCatalog.ToVerify, WorkFocus.IssueDesk));

        var pending = new[]
        {
            Line("DAT", 1, ResultStatus.Entered, "tech1", deadline: "Verify before 14:00"),
            Line("ABSC", 2, ResultStatus.Entered, "tech1"),
            Line("ABORH", 3, ResultStatus.Entered, "tech2", deadline: "Verify before 15:00"),
            Line("XM", null, null, null)
        };

        Assert.Equal((2, "1 before its specimen expires"), DashboardQueueCatalog.ToVerifyCard(pending, "tech2"));
        // Only ABO/Rh is barred from self-verification: tech1 counts their own DAT and screen and tech2's ABO/Rh.
        Assert.Equal((3, "2 before their specimens expire"), DashboardQueueCatalog.ToVerifyCard(pending, "tech1"));
        Assert.Equal((0, (string?)null), DashboardQueueCatalog.ToVerifyCard([Line("XM", null, null, null)], "tech2"));
    }

    // TEST-BB-452: the Pending tests note names lines waiting for a new specimen with those due before expiry; the
    // worklist's Recollect view keeps only lines on a rejected or cancelled tube.
    [Fact]
    public void PendingTestsNote_NamesRecollections_AndTheRecollectViewKeepsThem()
    {
        var rejected = Line("DAT", null, null, null) with { SpecimenStatus = SpecimenStatus.Rejected, CanEnterResults = false };
        var cancelled = Line("ABSC", null, null, null) with { SpecimenStatus = SpecimenStatus.Cancelled, CanEnterResults = false };
        var dueSoon = Line("ABORH", 3, ResultStatus.Entered, "tech1", deadline: "Verify before 15:00");
        var plain = Line("XM", null, null, null);

        Assert.Equal("1 to verify before its specimen expires; 2 need a new specimen",
            DashboardQueueCatalog.PendingTestsNote([rejected, cancelled, dueSoon, plain]));
        Assert.Equal("1 needs a new specimen", DashboardQueueCatalog.PendingTestsNote([rejected, plain]));
        Assert.Null(DashboardQueueCatalog.PendingTestsNote([plain]));
        Assert.Equal(new[] { "DAT", "ABSC" }, TestWorklistView.Recollect([rejected, plain, cancelled]).Select(i => i.TestCode).ToArray());
        Assert.Equal("Recollect", rejected.BenchActionLabel);
    }

    private static TestWorkItemDto Line(string testCode, long? resultId, ResultStatus? status, string? enteredBy, string? deadline = null) =>
        new(1, 1, 1, "Bench, Pat", "MRN1", testCode, testCode, "ORD1", OrderPriority.Routine, DateTime.UtcNow, "ACC1", 1,
            SpecimenStatus.Accepted, DateTime.UtcNow.AddDays(2), false, "O+", false, null, resultId, status, "Negative", null,
            ResultSource.Manual, true, null, CurrentResultEnteredBy: enteredBy, VerificationDeadlineNotice: deadline);

    private static Func<string, bool> Has(params string[] codes) =>
        code => codes.Contains(code, StringComparer.Ordinal);
}
