using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class ReactionDatTextTests
{
    private static ReactionInvestigationDto Reaction(long id, long patientId, DatWorkupResult dat) => new ReactionInvestigationDto(
        Id: id, TransfusionEventId: 4, PatientId: patientId, BloodProductId: 62, ReportedUtc: new DateTime(2026, 11, 6, 17, 0, 0, DateTimeKind.Utc),
        ReportedBy: "tech", ReactionType: null, Severity: ReactionSeverity.Unknown, Findings: null,
        Conclusions: null, FollowUp: null, Status: ReactionInvestigationStatus.Open, Disposition: null, ProductAtFault: false,
        IsFatality: false, FatalityNotificationStatus: FatalityNotificationStatus.NotApplicable,
        WrittenReportDueUtc: null, CberNotifiedUtc: null, WrittenReportSubmittedUtc: null) with { DatResult = dat };

    // TEST-BB-1219: the chart's DAT / Elution tab lists DATs recorded in the patient's reaction investigations
    // (end-user testing pass 197); a reaction with no DAT recorded, or another patient's, is not listed.
    [Fact]
    public void For_ListsThePatientsReactionDats()
    {
        var rows = ReactionDatText.For([
            Reaction(1, 6, DatWorkupResult.Negative),
            Reaction(2, 6, DatWorkupResult.NotRecorded),
            Reaction(3, 9, DatWorkupResult.Positive)], 6);

        var (id, text) = Assert.Single(rows);
        Assert.Equal(1, id);
        Assert.StartsWith("Reaction investigation #1 (reported 2026-11-0", text);
        Assert.EndsWith("): DAT negative.", text);
    }
}
