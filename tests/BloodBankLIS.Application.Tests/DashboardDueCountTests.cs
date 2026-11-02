using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Issuing;
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
