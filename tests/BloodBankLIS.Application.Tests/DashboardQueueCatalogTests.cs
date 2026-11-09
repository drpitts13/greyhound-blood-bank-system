using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Application.Patients;
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

    // TEST-BB-736: given the time, the Instrument failures card also says how long the oldest open failure has waited;
    // resolved and successful messages do not count, and no open failure gives no note.
    [Fact]
    public void InstrumentFailuresCard_NamesTheOldestWait()
    {
        var now = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);
        InstrumentMessageSummaryDto Message(long id, DateTime received, InstrumentMessageStatus status = InstrumentMessageStatus.Failed,
            string? error = "INST-CODE", DateTime? resolved = null) => new(
            id, 1, Hl7Direction.Inbound, InstrumentMessageKind.Result, status, received, received, "ACC", "MRN", error, resolved);

        Assert.Equal((2, "1 held for an instrument error code; oldest waiting 26 h"), DashboardQueueCatalog.InstrumentFailuresCard(
        [
            Message(1, now.AddHours(-26)),
            Message(2, now.AddMinutes(-15), error: "held (INST-HOLD)"),
            Message(3, now.AddDays(-5), resolved: now.AddDays(-4)),
            Message(4, now.AddDays(-9), InstrumentMessageStatus.Processed, error: null)
        ], now));
        Assert.Equal((0, (string?)null), DashboardQueueCatalog.InstrumentFailuresCard([Message(3, now.AddDays(-5), resolved: now)], now));
    }

    // TEST-BB-528: unmapped interface values are on the home screen for whoever keeps the translation table.
    [Fact]
    public void UnmappedValuesCard_FollowsTheInterfaceAdminPermission()
    {
        Assert.Contains(DashboardQueueCatalog.Select(Has(PermissionCodes.AdminHl7Manage)),
            c => c.Key == DashboardQueueCatalog.UnmappedInterfaceValues && c.Href == "/admin/hl7/translations?tab=unmapped");
        Assert.DoesNotContain(DashboardQueueCatalog.Select(Has(PermissionCodes.Hl7Manage)), c => c.Key == DashboardQueueCatalog.UnmappedInterfaceValues);
    }

    // TEST-BB-571: whoever merges charts gets a Possible duplicates card; pairs sharing an external identifier are
    // named in the note, name-and-birthday pairs stay in the count only.
    [Fact]
    public void PossibleDuplicatesCard_ForMergers_NamesSharedIdentifierPairs()
    {
        Assert.Contains(DashboardQueueCatalog.Select(Has(PermissionCodes.PatientMerge)),
            c => c.Key == DashboardQueueCatalog.PossibleDuplicates && c.Href == "/patients/duplicates");
        Assert.DoesNotContain(DashboardQueueCatalog.Select(Has(PermissionCodes.PatientWrite)), c => c.Key == DashboardQueueCatalog.PossibleDuplicates);

        PossibleDuplicatePairDto Pair(string reason) => new(null!, null!, reason);
        Assert.Null(DashboardQueueCatalog.PossibleDuplicatesNote([Pair(PossibleDuplicatePatientRule.SameNameAndBirthday)]));
        Assert.Equal("1 shares an external ID", DashboardQueueCatalog.PossibleDuplicatesNote([
            Pair(PossibleDuplicatePatientRule.SameNameAndBirthday),
            Pair($"{PossibleDuplicatePatientRule.SameNameAndBirthday}; {PossibleDuplicatePatientRule.SharedIdentifier("EPI-1", "EPI PI")}")]));
        Assert.Equal("2 share an external ID", DashboardQueueCatalog.PossibleDuplicatesNote([
            Pair(PossibleDuplicatePatientRule.SharedIdentifier("EPI-1", null)),
            Pair(PossibleDuplicatePatientRule.SharedIdentifier("EPI-2", "EPI PI"))]));
    }

    // TEST-BB-574: the bench and verifiers get an Open RhIG evaluations card; the note counts those ready to complete.
    [Fact]
    public void OpenRhigCard_ForRecordersAndVerifiers_CountsThoseReadyToComplete()
    {
        Assert.Contains(DashboardQueueCatalog.Select(Has(PermissionCodes.ResultVerify)), c => c.Key == DashboardQueueCatalog.OpenRhig && c.Href == "/rhig");
        Assert.Contains(DashboardQueueCatalog.Select(Has(PermissionCodes.ImmunoRecord)), c => c.Key == DashboardQueueCatalog.OpenRhig);
        Assert.DoesNotContain(DashboardQueueCatalog.Select(Has(PermissionCodes.IssueCreate)), c => c.Key == DashboardQueueCatalog.OpenRhig);

        RhigQueueItemDto Item(bool ready) => new(
            new RhigEvaluationDto(1, 1, RhigContext.Postpartum, DateTime.UtcNow, RhType.Unknown, FetalBleedScreen.NotDone, null,
                RhigDecision.Undetermined, null, null, "tech", DateTime.UtcNow, null, null, []),
            "Mother, Ann", "MRN1", ready);
        Assert.Null(DashboardQueueCatalog.RhigQueueNote([Item(false)]));
        Assert.Equal("2 ready to complete", DashboardQueueCatalog.RhigQueueNote([Item(true), Item(false), Item(true)]));

        // TEST-BB-575: completed candidates with no RhIG given recorded are counted as to give.
        var toGive = new RhigQueueItemDto(
            new RhigEvaluationDto(1, 1, RhigContext.Postpartum, DateTime.UtcNow, RhType.Positive, FetalBleedScreen.Negative, null,
                RhigDecision.Candidate, 1, null, "tech", DateTime.UtcNow, "lead", DateTime.UtcNow, []),
            "Mother, Ann", "MRN1", false);
        Assert.Equal("1 ready to complete; 1 to give", DashboardQueueCatalog.RhigQueueNote([Item(true), toGive]));
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
        Assert.Equal("1 lot to replace", DashboardQueueCatalog.ReagentQcNote([Work(ReagentQcWorklistRule.Replace)]));
    }

    // TEST-BB-727: the Second ABO/Rh card names patients with no ABO/Rh ordered and leads under Needs attention after
    // In transit; patients whose ABO/Rh is already ordered stay in the count only.
    [Fact]
    public void SecondAboNote_NamesPatientsWithNothingOrdered()
    {
        SecondAboWorkItemDto Item(string? order) => new(1, "MRN1", "Demo, Pat", "O Pos", 2, "reason", order);

        Assert.Null(DashboardQueueCatalog.SecondAboNote([]));
        Assert.Null(DashboardQueueCatalog.SecondAboNote([Item("ORD-9")]));
        Assert.Equal("1 with no ABO/Rh ordered", DashboardQueueCatalog.SecondAboNote([Item("ORD-9"), Item(null)]));
        Assert.Equal("2 with no ABO/Rh ordered", DashboardQueueCatalog.SecondAboNote([Item(null), Item(null)]));

        var cards = DashboardQueueCatalog.Select(_ => true);
        DashboardCardState State(string key, string? note) => new(cards.Single(c => c.Key == key), 1, note);
        var layout = DashboardHome.Arrange(
        [
            State(DashboardQueueCatalog.SecondAbo, DashboardQueueCatalog.SecondAboNote([Item(null)])),
            State(DashboardQueueCatalog.InTransit, "1 past the arrival window"),
            State(DashboardQueueCatalog.ReagentQc, "1 overdue")
        ], WorkFocus.Bench);
        Assert.Equal([DashboardQueueCatalog.InTransit, DashboardQueueCatalog.SecondAbo, DashboardQueueCatalog.ReagentQc],
            layout.Attention.Select(c => c.Card.Key));
    }

    // TEST-BB-731: the Unidentified patients card names only charts open past the reminder, with the oldest's age; a
    // chart within the window, or none at all, gives no note, so the card stays with the ordinary queues.
    [Fact]
    public void UnidentifiedNote_NamesChartsPastTheReminder()
    {
        var now = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);
        PatientDto Chart(long id, DateTime registered, bool past) => new(
            id, $"UNK-{id}", "UNIDENTIFIED", "TRAUMA", null, new DateOnly(1990, 1, 1), Sex.Unknown, PatientStatus.Active,
            registered, "tech", Unidentified: true, UnidentifiedPastReminder: past);

        Assert.Null(DashboardQueueCatalog.UnidentifiedNote([], now));
        Assert.Null(DashboardQueueCatalog.UnidentifiedNote([Chart(1, now.AddHours(-2), false)], now));
        Assert.Equal("1 not identified, oldest open 30 h",
            DashboardQueueCatalog.UnidentifiedNote([Chart(1, now.AddHours(-2), false), Chart(2, now.AddHours(-30), true)], now));
        Assert.Equal("2 not identified, oldest open 3 d",
            DashboardQueueCatalog.UnidentifiedNote([Chart(2, now.AddHours(-30), true), Chart(3, now.AddDays(-3), true)], now));
    }

    // TEST-BB-734: the HL7 errors card says how long the oldest unresolved error has waited; no errors, or none with a
    // queued time, gives no note.
    [Fact]
    public void OldestWaitingNote_NamesTheOldestError()
    {
        var now = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

        Assert.Null(DashboardQueueCatalog.OldestWaitingNote([], now));
        Assert.Null(DashboardQueueCatalog.OldestWaitingNote([null], now));
        Assert.Equal("oldest waiting 40 min", DashboardQueueCatalog.OldestWaitingNote([now.AddMinutes(-5), null, now.AddMinutes(-40)], now));
        Assert.Equal("oldest waiting 2 d", DashboardQueueCatalog.OldestWaitingNote([now.AddHours(-3), now.AddDays(-2)], now));
    }

    // TEST-BB-725: the Final disposition card names how far behind its oldest unit is, and an empty board has no note.
    [Fact]
    public void DispositionNote_NamesTheOldestPastDue()
    {
        var now = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);
        OverdueDispositionWorkItemDto Due(DateTime due) => new(1, 2, "W0001230009205", "Demo, Pat (MRN1)", due.AddDays(-1), due);

        Assert.Null(DashboardQueueCatalog.DispositionNote([], now));
        Assert.Equal("oldest 3 d past due", DashboardQueueCatalog.DispositionNote([Due(now.AddHours(-5)), Due(now.AddDays(-3)), Due(now.AddHours(-30))], now));
        Assert.Equal("oldest 5 h past due", DashboardQueueCatalog.DispositionNote([Due(now.AddHours(-5))], now));
    }

    // TEST-BB-726: every unit on the board is overdue, so a non-empty Final disposition card leads under Needs attention.
    [Fact]
    public void DispositionCard_WithOverdueUnits_NeedsAttention()
    {
        var card = DashboardQueueCatalog.Select(_ => true).Single(c => c.Key == DashboardQueueCatalog.Disposition);
        var now = DateTime.UtcNow;
        var note = DashboardQueueCatalog.DispositionNote([new(1, 2, "W1", "P", now.AddDays(-2), now.AddDays(-1))], now);

        var busy = DashboardHome.Arrange([new DashboardCardState(card, 1, note)], WorkFocus.IssueDesk);
        Assert.Single(busy.Attention);

        var clear = DashboardHome.Arrange([new DashboardCardState(card, 0, DashboardQueueCatalog.DispositionNote([], now))], WorkFocus.IssueDesk);
        Assert.Empty(clear.Attention);
        Assert.Single(clear.Clear);
    }
}
