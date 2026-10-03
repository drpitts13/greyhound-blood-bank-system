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
