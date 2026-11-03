using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Tests;

public class DashboardDueCountTests
{
    [Fact]
    public void RetrospectiveCountOverdue_IncludesOnlyItemsPastTheStoredDueTime()
    {
        var count = RetrospectiveCrossmatchAttentionRule.CountOverdue(
        [
            Retro(isOverdue: true),
            Retro(isOverdue: false),
            Retro(isOverdue: true)
        ]);

        Assert.Equal(2, count);
        Assert.Equal(0, RetrospectiveCrossmatchAttentionRule.CountOverdue([Retro(isOverdue: false)]));
    }

    // TEST-BB-361: the ready-to-issue card says how many reservations need a new crossmatch.
    [Fact]
    public void ReadyToIssue_CountsOnlyReservationsWhoseCrossmatchExpired()
    {
        var count = ReadyToIssueAttentionRule.CountCrossmatchExpired(
        [
            ProductAllocationDisplayStatus.CrossmatchExpired,
            ProductAllocationDisplayStatus.ReadyForIssue,
            ProductAllocationDisplayStatus.Reserved,
            ProductAllocationDisplayStatus.Exception,
            ProductAllocationDisplayStatus.CrossmatchExpired
        ]);

        Assert.Equal(2, count);
        Assert.Equal("2 need a new crossmatch", DashboardQueueCatalog.AttentionNote(count, "needs a new crossmatch", "need a new crossmatch"));
        Assert.Null(DashboardQueueCatalog.AttentionNote(
            ReadyToIssueAttentionRule.CountCrossmatchExpired([ProductAllocationDisplayStatus.ReadyForIssue]),
            "needs a new crossmatch",
            "need a new crossmatch"));
    }

    [Fact]
    public void FatalityFollowUpDue_CountsClosedCasesThatStillNeedFollowUp()
    {
        var count = OpenReactionInvestigationRule.CountFatalityFollowUpDue(
        [
            Reaction(ReactionInvestigationStatus.Closed, followUpDue: true),
            Reaction(ReactionInvestigationStatus.Open, followUpDue: false),
            Reaction(ReactionInvestigationStatus.UnderReview, followUpDue: true)
        ]);

        Assert.Equal(2, count);
    }

    // TEST-BB-369: the pending tests card says how many results must be verified before their specimen expires.
    [Fact]
    public void PendingTests_CountsOnlyRowsWithAVerificationDeadline()
    {
        var count = PendingTestAttentionRule.CountVerifyBeforeExpiry(
        [
            Work("Verify before 2026-11-02 15:00 UTC, when the specimen expires. After that this result cannot be verified."),
            Work(null),
            Work("Verify before 2026-11-02 18:00 UTC, when the specimen expires. After that this result cannot be verified.")
        ]);

        Assert.Equal(2, count);
        Assert.Equal("2 to verify before their specimens expire",
            DashboardQueueCatalog.AttentionNote(count, "to verify before its specimen expires", "to verify before their specimens expire"));
        Assert.Equal("1 to verify before its specimen expires",
            DashboardQueueCatalog.AttentionNote(1, "to verify before its specimen expires", "to verify before their specimens expire"));
        Assert.Equal(0, PendingTestAttentionRule.CountVerifyBeforeExpiry([Work(null)]));
    }

    private static TestWorkItemDto Work(string? deadline) => new(
        OrderLineId: 1, OrderId: 1, PatientId: 1, PatientName: "Patient", Mrn: "MRN", TestCode: "ABSC", TestName: "Antibody screen",
        OrderNumber: "ORD", Priority: OrderPriority.Routine, OrderedUtc: DateTime.UtcNow, AccessionNumber: "ACC", SpecimenId: 1,
        SpecimenStatus: SpecimenStatus.Accepted, SpecimenExpiresUtc: null, SpecimenExpired: false, CurrentBloodType: null,
        HasAntibodyHistory: false, AntibodySummary: null, CurrentResultId: 1, CurrentResultStatus: ResultStatus.PendingVerification,
        CurrentResultValue: "Negative", CurrentResultInterpretation: null, CurrentResultSource: ResultSource.Manual,
        CanEnterResults: true, BlockReason: null, VerificationDeadlineNotice: deadline);

    // TEST-BB-380: the Ready to issue card also says how many reserved units expire soon.
    [Fact]
    public void ReadyToIssueCard_NamesExpiringSoonAndExpiredCrossmatches()
    {
        Assert.Null(ReadyToIssueAttentionRule.CardNote([Ready(1), Ready(2)]));
        Assert.Equal("1 expires soon", ReadyToIssueAttentionRule.CardNote([Ready(1) with { ExpiresSoon = true }, Ready(2)]));
        Assert.Equal(
            "1 needs a new crossmatch · 2 expire soon",
            ReadyToIssueAttentionRule.CardNote(
            [
                Ready(1) with { DisplayStatus = ProductAllocationDisplayStatus.CrossmatchExpired },
                Ready(2) with { ExpiresSoon = true },
                Ready(3) with { ExpiresSoon = true }
            ]));
        // An outdated unit is already past expiry, not about to expire.
        Assert.Equal(0, ReadyToIssueAttentionRule.CountExpiringSoon([Ready(1) with { ExpiresSoon = true, UnitOutdated = true }]));
    }

    // TEST-BB-362: the Ready to issue board lists rows that can be issued now first.
    [Fact]
    public void ReadyToIssue_IssuableRowsComeFirst_AndKeepTheirOrder()
    {
        var rows = new[]
        {
            Ready(1) with { CrossmatchBlocksRoutineIssue = true, DisplayStatus = ProductAllocationDisplayStatus.CrossmatchExpired },
            Ready(2),
            Ready(3) with { SpecimenBlocksIssue = true },
            Ready(4)
        };

        var ordered = ReadyToIssueAttentionRule.IssuableFirst(rows);

        Assert.Equal([2L, 4L, 1L, 3L], ordered.Select(r => r.AllocationId));
        Assert.Equal("2 can be issued now. 2 need attention first and are listed after them.", ReadyToIssueAttentionRule.Summary(ordered));
        Assert.Equal("All 2 can be issued now.", ReadyToIssueAttentionRule.Summary([Ready(2), Ready(4)]));
        Assert.Null(ReadyToIssueAttentionRule.Summary([]));
    }

    [Fact]
    public void ReadyToIssue_EachBlockingMark_StopsRoutineIssue()
    {
        Assert.True(ReadyToIssueAttentionRule.CanIssueRoutinely(Ready(1) with { OpenAntibodyWorkup = true, ExpiresSoon = true, AntigenNotice = "Check antigen" }));
        Assert.False(ReadyToIssueAttentionRule.CanIssueRoutinely(Ready(1) with { SpecimenBlocksIssue = true }));
        Assert.False(ReadyToIssueAttentionRule.CanIssueRoutinely(Ready(1) with { UnitOutdated = true }));
        Assert.False(ReadyToIssueAttentionRule.CanIssueRoutinely(Ready(1) with { SpecialRequirementNotice = "Irradiated required" }));
        Assert.False(ReadyToIssueAttentionRule.CanIssueRoutinely(Ready(1) with { AboRhDiscrepancyBlocksIssue = true }));
        Assert.False(ReadyToIssueAttentionRule.CanIssueRoutinely(Ready(1) with { NeedsSecondAbo = true }));
        Assert.False(ReadyToIssueAttentionRule.CanIssueRoutinely(Ready(1) with { CrossmatchBlocksRoutineIssue = true }));
    }

    private static ReadyToIssueWorkItemDto Ready(long id) => new(
        AllocationId: id, PatientId: 1, MedicalRecordNumber: "MRN", PatientDisplayName: "Patient", DateOfBirth: "1980-01-01",
        BloodUnitId: id, UnitNumber: "W0000", CurrentBloodType: "O+", HasAntibodyHistory: false, AntibodySummary: null,
        SpecimenExpiresUtc: null, SpecimenExpired: false, AllocatedUtc: DateTime.UtcNow,
        DisplayStatus: ProductAllocationDisplayStatus.ReadyForIssue, LatestCrossmatchResult: CrossmatchResult.Compatible);

    private static RetrospectiveCrossmatchWorkItemDto Retro(bool isOverdue) => new(
        IssueId: 1, PatientId: 1, MedicalRecordNumber: "MRN", PatientDisplayName: "Patient",
        BloodUnitId: 1, UnitNumber: "W0000", CurrentBloodType: "O+", HasAntibodyHistory: false,
        AntibodySummary: null, SpecimenExpiresUtc: null, SpecimenExpired: false,
        IssuedUtc: DateTime.UtcNow, DueUtc: DateTime.UtcNow, IsOverdue: isOverdue,
        IssueType: IssueType.EmergencyRelease, Status: IssueStatus.Issued, EmergencyReleaseDetails: null);

    private static ReactionInvestigationDto Reaction(ReactionInvestigationStatus status, bool followUpDue) => new(
        Id: 1, TransfusionEventId: 1, PatientId: 1, BloodProductId: 1, ReportedUtc: DateTime.UtcNow,
        ReportedBy: "tech", ReactionType: null, Severity: ReactionSeverity.Mild, Findings: null,
        Conclusions: null, FollowUp: null, Status: status, Disposition: null, ProductAtFault: false,
        IsFatality: followUpDue, FatalityNotificationStatus: FatalityNotificationStatus.NotApplicable,
        WrittenReportDueUtc: null, CberNotifiedUtc: null, WrittenReportSubmittedUtc: null,
        FatalityFollowUpDue: followUpDue);
}
