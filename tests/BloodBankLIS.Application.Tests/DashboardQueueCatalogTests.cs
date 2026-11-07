using BloodBankLIS.Application.Admin;
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

    // TEST-BB-557: the open handoff's home card names its urgent items, up to three and then how many more.
    [Fact]
    public void HandoffNote_NamesTheUrgentItemsOfTheOpenHandoff()
    {
        ShiftHandoffDto Open(int mtp = 0, int expired = 0, int reagentLots = 0, int critical = 0, int pending = 0) =>
            new(1, DateTime.UtcNow, "tech", null, "note", 0, 0, 0, 0, 0, pending, 0, 0, 0, null, null,
                ActiveMtp: mtp, ExpiredOnShelf: expired, ReagentLotsFailedQc: reagentLots, CriticalDeviations: critical);

        Assert.Null(DashboardQueueCatalog.HandoffNote((ShiftHandoffDto?)null));
        Assert.Equal("Needs acknowledgment", DashboardQueueCatalog.HandoffNote(Open(pending: 4)));
        Assert.Equal("Needs acknowledgment; urgent: Massive transfusion active, Expired on shelf", DashboardQueueCatalog.HandoffNote(Open(mtp: 1, expired: 2)));
        Assert.Equal("Needs acknowledgment; urgent: Massive transfusion active, Critical deviation open, Reagent QC failed, and 1 more",
            DashboardQueueCatalog.HandoffNote(Open(mtp: 1, critical: 1, reagentLots: 1, expired: 1)));
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
        Assert.Contains(DashboardQueueCatalog.Select(Has(PermissionCodes.InstrumentManage)), c => c.Key == DashboardQueueCatalog.InstrumentFailures && c.Href == "/instruments?show=open");
        var bench = DashboardQueueCatalog.Select(Has(PermissionCodes.ResultEnter));
        Assert.DoesNotContain(bench, c => c.Key is DashboardQueueCatalog.Hl7Errors or DashboardQueueCatalog.InstrumentFailures);
    }

    // TEST-BB-540: the Instrument failures card counts open failures and says how many are held for an instrument error
    // code (they wait on a review, not a fix); resolved failures and other failures are not counted as held.
    [Fact]
    public void InstrumentFailuresCard_NamesHeldResults()
    {
        var now = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);
        InstrumentMessageSummaryDto Message(long id, string? error, DateTime? resolved = null) => new(
            id, 1, Hl7Direction.Inbound, InstrumentMessageKind.Result, InstrumentMessageStatus.Failed, now, now, "ACC", "MRN", error, resolved);

        var (count, note) = DashboardQueueCatalog.InstrumentFailuresCard(
        [
            Message(1, "Instrument error Q (Hold): ABORH for ACC held for review, not posted (INST-HOLD)."),
            Message(2, "Instrument error W (Hold): ABSC for ACC held for review, not posted (INST-HOLD)."),
            Message(3, "Instrument code X has no translation to a test (INST-CODE)"),
            Message(4, "held (INST-HOLD)", resolved: now)
        ]);

        Assert.Equal((3, "2 held for instrument error codes"), (count, note));
        Assert.Equal((1, (string?)null), DashboardQueueCatalog.InstrumentFailuresCard([Message(3, "INST-CODE")]));
    }

    // TEST-BB-528: unmapped interface values are on the home screen for whoever keeps the translation table.
    [Fact]
    public void UnmappedValuesCard_FollowsTheInterfaceAdminPermission()
    {
        Assert.Contains(DashboardQueueCatalog.Select(Has(PermissionCodes.AdminHl7Manage)),
            c => c.Key == DashboardQueueCatalog.UnmappedInterfaceValues && c.Href == "/admin/hl7/translations?tab=unmapped");
        Assert.DoesNotContain(DashboardQueueCatalog.Select(Has(PermissionCodes.Hl7Manage)), c => c.Key == DashboardQueueCatalog.UnmappedInterfaceValues);
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

    // TEST-BB-465: lines with no tube are listed STAT first; the Pending tests note counts STAT orders (not lines)
    // without a tube, and stays quiet for routine ones. A rejected tube is Recollect, not No specimen.
    [Fact]
    public void NoSpecimen_ListsRushFirst_AndTheNoteCountsStatOrders()
    {
        var at = new DateTime(2026, 11, 5, 8, 0, 0, DateTimeKind.Utc);
        TestWorkItemDto Bare(long orderId, string code, OrderPriority priority, int minutes) =>
            Line(code, null, null, null) with { OrderId = orderId, Priority = priority, OrderedUtc = at.AddMinutes(minutes), SpecimenId = null, AccessionNumber = null, SpecimenStatus = null };
        var routineOld = Bare(1, "ABSC", OrderPriority.Routine, 0);
        var statAbo = Bare(2, "ABORH", OrderPriority.Stat, 30);
        var statScreen = Bare(2, "ABSC", OrderPriority.Stat, 30);
        var rejected = Line("DAT", null, null, null) with { SpecimenStatus = SpecimenStatus.Rejected };
        var linked = Line("XM", null, null, null);

        Assert.Equal(new[] { "ABORH", "ABSC", "ABSC" }, TestWorklistView.NoSpecimen([routineOld, linked, statAbo, rejected, statScreen]).Select(i => i.TestCode).ToArray());
        Assert.Equal(2L, TestWorklistView.NoSpecimen([routineOld, statAbo])[0].OrderId);
        Assert.Equal("1 STAT/urgent order waiting for a specimen", DashboardQueueCatalog.PendingTestsNote([routineOld, statAbo, statScreen, linked]));
        Assert.Null(DashboardQueueCatalog.PendingTestsNote([routineOld, linked]));
    }

    private static TestWorkItemDto Line(string testCode, long? resultId, ResultStatus? status, string? enteredBy, string? deadline = null) =>
        new(1, 1, 1, "Bench, Pat", "MRN1", testCode, testCode, "ORD1", OrderPriority.Routine, DateTime.UtcNow, "ACC1", 1,
            SpecimenStatus.Accepted, DateTime.UtcNow.AddDays(2), false, "O+", false, null, resultId, status, "Negative", null,
            ResultSource.Manual, true, null, CurrentResultEnteredBy: enteredBy, VerificationDeadlineNotice: deadline);

    private static Func<string, bool> Has(params string[] codes) =>
        code => codes.Contains(code, StringComparer.Ordinal);

    // TEST-BB-461: the In transit card names overdue units and coolers out unchecked or checked with differences.
    [Fact]
    public void InTransitNote_NamesOverdueUnits_AndCoolerChecks()
    {
        var at = new DateTime(2026, 11, 5, 9, 0, 0, DateTimeKind.Utc);
        OpenCoolerDto Cooler(string id, DateTime? checkedUtc, bool? ready) => new(id, 1, ["Doe, J"], ["ICU"], at, checkedUtc, ready);
        var coolers = new[] { Cooler("C1", null, null), Cooler("C2", null, null), Cooler("C3", at, false), Cooler("C4", at, true) };

        Assert.Null(DashboardQueueCatalog.InTransitNote(0, null));
        Assert.Null(DashboardQueueCatalog.InTransitNote(0, [Cooler("C4", at, true)]));
        Assert.Equal("1 past the arrival window", DashboardQueueCatalog.InTransitNote(1, []));
        Assert.Equal("1 past the arrival window; 2 coolers not checked; 1 cooler checked with differences",
            DashboardQueueCatalog.InTransitNote(1, coolers));
    }

    // TEST-BB-484: the Supervisor card names results due before specimen expiry, missing or damaged units, and overrides to review.
    [Fact]
    public void SupervisorNote_NamesTheItemsWithADeadlineOrAReview()
    {
        SupervisorQueueItemDto Item(string kind, bool dueSoon = false) => new(kind, "t", "d", "/supervisor", "Go", DueSoon: dueSoon);

        Assert.Null(DashboardQueueCatalog.SupervisorNote([]));
        Assert.Null(DashboardQueueCatalog.SupervisorNote([
            Item(SupervisorQueueRule.KindAntibodyReview), Item(SupervisorQueueRule.KindQuarantineRelease), Item(SupervisorQueueRule.KindResultVerify)]));
        Assert.Equal("1 to verify before its specimen expires; 2 missing or damaged units; 1 override to review",
            DashboardQueueCatalog.SupervisorNote([
                Item(SupervisorQueueRule.KindResultVerify, dueSoon: true), Item(SupervisorQueueRule.KindResultVerify),
                Item(SupervisorQueueRule.KindDiscrepancy), Item(SupervisorQueueRule.KindDiscrepancy),
                Item(SupervisorQueueRule.KindOverrideReview)]));
    }

    // TEST-BB-490: deviation managers see Open deviations (everything not closed), with critical ones named; the card
    // sits in the Supervisor focus.
    [Fact]
    public void Deviations_ForManagers_CountOpenAndNameCritical()
    {
        Assert.Contains(DashboardQueueCatalog.Select(Has(PermissionCodes.DeviationManage)), c => c.Key == DashboardQueueCatalog.Deviations && c.Href == "/deviations");
        Assert.DoesNotContain(DashboardQueueCatalog.Select(Has(PermissionCodes.ResultEnter)), c => c.Key == DashboardQueueCatalog.Deviations);
        Assert.True(DashboardHome.InFocus(DashboardQueueCatalog.Deviations, WorkFocus.Supervisor));
        Assert.False(DashboardHome.InFocus(DashboardQueueCatalog.Deviations, WorkFocus.Bench));

        var at = new DateTime(2026, 11, 6, 8, 0, 0, DateTimeKind.Utc);
        DeviationDto Row(DeviationSeverity severity, DeviationStatus status) => new(1, "t", "d", severity, status, null, null, null, "x", at);

        Assert.Equal((0, (string?)null), DashboardQueueCatalog.DeviationsCard([Row(DeviationSeverity.Critical, DeviationStatus.Closed)]));
        Assert.Equal((2, (string?)null), DashboardQueueCatalog.DeviationsCard([Row(DeviationSeverity.Major, DeviationStatus.Open), Row(DeviationSeverity.Minor, DeviationStatus.UnderReview)]));
        Assert.Equal((3, "1 critical"), DashboardQueueCatalog.DeviationsCard([
            Row(DeviationSeverity.Critical, DeviationStatus.CorrectiveAction), Row(DeviationSeverity.Critical, DeviationStatus.Closed),
            Row(DeviationSeverity.Major, DeviationStatus.Open), Row(DeviationSeverity.Minor, DeviationStatus.Open)]));
    }

    // TEST-BB-494: result-entry staff see Reagent QC on the bench, with failed and overdue QC named; review stays in the count.
    [Fact]
    public void ReagentQc_ForResultEntry_NamesFailedAndOverdue()
    {
        Assert.Contains(DashboardQueueCatalog.Select(Has(PermissionCodes.ResultEnter)), c => c.Key == DashboardQueueCatalog.ReagentQc && c.Href == "/reagents");
        Assert.DoesNotContain(DashboardQueueCatalog.Select(Has(PermissionCodes.IssueCreate)), c => c.Key == DashboardQueueCatalog.ReagentQc);
        Assert.True(DashboardHome.InFocus(DashboardQueueCatalog.ReagentQc, WorkFocus.Bench));

        Inventory.ReagentQcWorkItemDto Work(string kind) => new(kind, 1, "LOT-1", null, "m");
        Assert.Null(DashboardQueueCatalog.ReagentQcNote([]));
        Assert.Null(DashboardQueueCatalog.ReagentQcNote([Work(ReagentQcWorklistRule.Unreviewed)]));
        Assert.Equal("1 failed; 2 overdue", DashboardQueueCatalog.ReagentQcNote([
            Work(ReagentQcWorklistRule.Failed), Work(ReagentQcWorklistRule.Overdue), Work(ReagentQcWorklistRule.Overdue), Work(ReagentQcWorklistRule.Unreviewed)]));
        // TEST-BB-558: expired lots are named on the card.
        Assert.Equal("1 overdue; 2 lots expired", DashboardQueueCatalog.ReagentQcNote([
            Work(ReagentQcWorklistRule.Overdue), Work(ReagentQcWorklistRule.Expired), Work(ReagentQcWorklistRule.Expired)]));
    }
}
