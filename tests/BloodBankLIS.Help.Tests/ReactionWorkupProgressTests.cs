using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class ReactionWorkupProgressTests
{
    private static ReactionInvestigationDto Reaction() => new(
        Id: 2, TransfusionEventId: 4, PatientId: 12, BloodProductId: 62, ReportedUtc: DateTime.UtcNow,
        ReportedBy: "tech", ReactionType: null, Severity: ReactionSeverity.Unknown, Findings: null,
        Conclusions: null, FollowUp: null, Status: ReactionInvestigationStatus.Open, Disposition: null, ProductAtFault: false,
        IsFatality: false, FatalityNotificationStatus: FatalityNotificationStatus.NotApplicable,
        WrittenReportDueUtc: null, CberNotifiedUtc: null, WrittenReportSubmittedUtc: null);

    // TEST-BB-1185: the Reactions board's Workup column says which workup steps are recorded and which are still to do,
    // instead of repeating the Next step sentence (end-user testing pass 162).
    [Fact]
    public void Text_ListsWhatIsRecordedAndWhatIsLeft()
    {
        var started = Reaction() with { ClericalCheckCompleted = true };
        Assert.Equal("1 of 5 recorded; to do: visual inspection, repeat patient ABO/Rh, repeat unit ABO/Rh, DAT", ReactionWorkupProgress.Text(started));

        var done = started with
        {
            VisualInspectionCompleted = true, RepeatPatientAboRh = "O+", RepeatUnitAboRh = "O+", DatResult = DatWorkupResult.Negative
        };
        Assert.Equal("All 5 recorded", ReactionWorkupProgress.Text(done));
    }
}
