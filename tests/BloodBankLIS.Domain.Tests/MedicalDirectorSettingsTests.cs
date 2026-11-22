using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

/// <summary>
/// Medical director decisions recorded as facility settings (2026-11-20: different medical directors use the
/// application). Each setting defaults to the decision previously built in, and each choice changes the rule as described.
/// </summary>
public class MedicalDirectorSettingsTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    // TEST-BB-1352: each decision is a facility setting whose default is the decision as it was built in, so an existing
    // facility sees no change until its medical director changes it.
    [Theory]
    [InlineData(FacilityPolicyKeys.DeviationFdaReportabilityToClose, "2")]
    [InlineData(FacilityPolicyKeys.RetrospectiveIncompatibleOpensDeviation, "true")]
    [InlineData(FacilityPolicyKeys.RetrospectiveCrossmatchOnExpiredSpecimen, "true")]
    [InlineData(FacilityPolicyKeys.EmergencyReleaseBeforeSpecimen, "true")]
    [InlineData(FacilityPolicyKeys.ReactionWorkupOnExpiredSpecimen, "true")]
    [InlineData(FacilityPolicyKeys.ReactionCloseRequiresConclusion, "true")]
    [InlineData(FacilityPolicyKeys.RhigDVariantHandling, "1")]
    [InlineData(FacilityPolicyKeys.ReactionUnderInvestigationIssue, "1")]
    [InlineData(FacilityPolicyKeys.BackEntryOutsideOutage, "1")]
    [InlineData(FacilityPolicyKeys.VerifyAfterSpecimenExpiry, "true")]
    [InlineData(FacilityPolicyKeys.CorrectAfterSpecimenExpiry, "true")]
    [InlineData(FacilityPolicyKeys.AntibodyIdRequireExclusionRationale, "true")]
    [InlineData(FacilityPolicyKeys.InboundResultStatuses, "F,C,P,R")]
    [InlineData(FacilityPolicyKeys.AntibodyIdDosageSensitiveAntigens, "C,c,E,e,FYA,FYB,JKA,JKB,M,N,S,s")]
    [InlineData(FacilityPolicyKeys.AntibodyIdGenotypeMethodWords, "genotype,molecular,predicted,dna,pcr")]
    [InlineData(FacilityPolicyKeys.BatchVerifyTests, "ABORH,ABSC,DAT")]
    [InlineData(FacilityPolicyKeys.TiterGradeScores, "0=0,w+=2,1+=5,2+=8,3+=10,4+=12")]
    [InlineData(FacilityPolicyKeys.ReceiveTemperatureMinC, "1")]
    [InlineData(FacilityPolicyKeys.ReceiveTemperatureMaxC, "10")]
    [InlineData(FacilityPolicyKeys.AntibodyIdCompleteOnExpiredSpecimen, "true")]
    [InlineData(FacilityPolicyKeys.AntibodyIdCompleteWithExpiredPanelLot, "true")]
    [InlineData(FacilityPolicyKeys.BlockInactivePatientClinicalUse, "false")]
    [InlineData(FacilityPolicyKeys.AntibodyIdOnReceivedSpecimen, "true")]
    [InlineData(FacilityPolicyKeys.TransfusionReportedForUnitNotIssued, "1")]
    [InlineData(FacilityPolicyKeys.PatientTestOnExpiredSpecimen, "2")]
    [InlineData(FacilityPolicyKeys.CrossmatchOnExpiredSpecimen, "1")]
    public void Decision_IsAFacilitySetting_DefaultingToTheDecisionMade(string key, string decided)
    {
        var definition = FacilityPolicyCatalog.Find(key);
        Assert.NotNull(definition);
        Assert.Equal(decided, definition!.DefaultValue);
        Assert.StartsWith("Medical director / OCD-", definition.Citation);
        Assert.False(definition.AwaitingEntryAtDefault);
    }

    // TEST-BB-1353 (OCD-090): which deviations need their FDA reportability determined to close.
    [Theory]
    [InlineData(0, DeviationSeverity.Critical, false)]
    [InlineData(1, DeviationSeverity.Minor, true)]
    [InlineData(2, DeviationSeverity.Minor, false)]
    [InlineData(2, DeviationSeverity.Major, true)]
    [InlineData(3, DeviationSeverity.Major, false)]
    [InlineData(3, DeviationSeverity.Critical, true)]
    public void FdaReportability_FollowsTheSetting(int setting, DeviationSeverity severity, bool required)
    {
        var close = DeviationFdaReportRule.EvaluateClose(DeviationStatus.Closed, severity, DeviationFdaReportability.NotDetermined, setting);
        Assert.Equal(required ? RuleSeverity.HardStop : RuleSeverity.Pass, close.Severity);
        Assert.Equal(RuleSeverity.HardStop,
            DeviationFdaReportRule.EvaluateClose(DeviationStatus.Closed, DeviationSeverity.Major, DeviationFdaReportability.NotDetermined).Severity);
    }

    // TEST-BB-1354 (OCD-078): another unit while a reaction is under investigation: no check, warning, or refused.
    [Fact]
    public void ReactionUnderInvestigation_FollowsTheSetting()
    {
        var reported = Now.AddHours(-2);
        Assert.Equal(RuleSeverity.Pass, ReactionUnderInvestigationRule.Evaluate(7, reported, Now, 0).Severity);
        Assert.Equal(RuleSeverity.Warning, ReactionUnderInvestigationRule.Evaluate(7, reported, Now).Severity);
        var refused = ReactionUnderInvestigationRule.Evaluate(7, reported, Now, 2);
        Assert.Equal(RuleSeverity.HardStop, refused.Severity);
        Assert.Contains("until the investigation is closed", refused.Message);
        Assert.Equal(RuleSeverity.Pass, ReactionUnderInvestigationRule.Evaluate(7, Now.AddHours(1), Now, 2).Severity);
    }

    // TEST-BB-1355 (OCD-081): a back-entry outside every recorded outage: allowed, reason required, or refused.
    [Fact]
    public void BackEntryOutsideOutage_FollowsTheSetting()
    {
        IReadOnlyCollection<DowntimeEpisode> outages = [new DowntimeEpisode { StartedUtc = Now.AddDays(-3), EndedUtc = Now.AddDays(-2) }];
        var paper = Now.AddHours(-5);
        Assert.Equal(RuleSeverity.Pass, DowntimeEpisodeRule.CheckPaperTime(outages, paper, null, 0).Severity);
        Assert.Equal(RuleSeverity.HardStop, DowntimeEpisodeRule.CheckPaperTime(outages, paper, null).Severity);
        Assert.Equal(RuleSeverity.Pass, DowntimeEpisodeRule.CheckPaperTime(outages, paper, "Clock on the paper form was wrong").Severity);
        Assert.Equal(RuleSeverity.HardStop, DowntimeEpisodeRule.CheckPaperTime(outages, paper, "Clock on the paper form was wrong", 2).Severity);
        Assert.Equal(RuleSeverity.Pass, DowntimeEpisodeRule.CheckPaperTime(outages, Now.AddDays(-2.5), null, 2).Severity);
    }

    // TEST-BB-1356 (OCD-058, OCD-051, OCD-093): results and workups on an expired specimen follow the settings.
    [Fact]
    public void ExpiredSpecimenAllowances_FollowTheSettings()
    {
        Assert.True(ReactionWorkupSpecimenRule.AllowsExpiredSpecimen(OrderType.TransfusionReactionWorkup));
        Assert.False(ReactionWorkupSpecimenRule.AllowsExpiredSpecimen(OrderType.TransfusionReactionWorkup, facilityAllows: false));
        Assert.Equal(RuleSeverity.Pass, ReactionWorkupSpecimenRule.EvaluatePre(1, 1, SpecimenStatus.Expired, Now.AddDays(-5), Now.AddDays(-4), "ACC1").Severity);
        Assert.Equal(RuleSeverity.HardStop, ReactionWorkupSpecimenRule.EvaluatePre(1, 1, SpecimenStatus.Expired, Now.AddDays(-5), Now.AddDays(-4), "ACC1", expiredAllowed: false).Severity);

        var expires = Now.AddDays(-1);
        var entered = Now.AddDays(-2);
        Assert.True(VerificationDeadlineRule.AllowsExpiredSpecimen(ResultStatus.Corrected));
        Assert.False(VerificationDeadlineRule.AllowsExpiredSpecimen(ResultStatus.Corrected, correctionsAllowed: false));
        Assert.True(VerificationDeadlineRule.AllowsExpiredSpecimen(ResultStatus.Entered, entered, null, expires));
        Assert.False(VerificationDeadlineRule.AllowsExpiredSpecimen(ResultStatus.Entered, entered, null, expires, enteredAllowed: false));
    }

    // TEST-BB-1359 (OCD-019, 020, 024, 061, 065): list settings are checked when saved, read back as the decided
    // default, and change the rules that use them.
    [Fact]
    public void ListSettings_AreCheckedAndUsed()
    {
        Assert.Null(FacilityPolicyLists.Problem(FacilityPolicyKeys.InboundResultStatuses, "F,C"));
        Assert.NotNull(FacilityPolicyLists.Problem(FacilityPolicyKeys.InboundResultStatuses, "F,final"));
        Assert.NotNull(FacilityPolicyLists.Problem(FacilityPolicyKeys.BatchVerifyTests, "ABSC,XM"));
        Assert.NotNull(FacilityPolicyLists.Problem(FacilityPolicyKeys.TiterGradeScores, "1+=five"));
        Assert.Null(FacilityPolicyLists.Problem(FacilityPolicyKeys.TiterGradeScores, FacilityPolicyLists.DefaultTiterGradeScores));
        Assert.Equal(TiterRule.ExampleScores, FacilityPolicyLists.Scores(FacilityPolicyLists.DefaultTiterGradeScores));
        Assert.True(AntibodyIdentificationPolicy.DefaultDosageSensitiveCodes.SetEquals(
            FacilityPolicyLists.Set(FacilityPolicyLists.DefaultDosageSensitiveAntigens, StringComparer.Ordinal)));

        var finalOnly = FacilityPolicyLists.Set("F", StringComparer.OrdinalIgnoreCase);
        Assert.Equal(RuleSeverity.Pass, InterfaceResultAcceptanceRule.Evaluate("P").Severity);
        Assert.Equal(RuleSeverity.HardStop, InterfaceResultAcceptanceRule.Evaluate("P", finalOnly).Severity);
        Assert.Equal(RuleSeverity.Pass, InterfaceResultAcceptanceRule.Evaluate("", finalOnly).Severity);

        Assert.True(AntigenTypingMethodInfo.IndicatesPredictedGenotype("PCR-SSP"));
        Assert.False(AntigenTypingMethodInfo.IndicatesPredictedGenotype("PCR-SSP", ["genotype"]));

        var screen = new BatchVerifyRule.Facts("ABSC", "Negative", ResultStatus.Entered, "tech1", "tech2", null, false, false);
        Assert.Equal(RuleSeverity.Pass, BatchVerifyRule.Evaluate(screen).Severity);
        Assert.NotEqual(RuleSeverity.Pass, BatchVerifyRule.Evaluate(screen, FacilityPolicyLists.Set("ABORH", StringComparer.OrdinalIgnoreCase)).Severity);
    }

    // TEST-BB-1360 (OCD-005, OCD-025, OCD-027): the receipt temperature range and completing antibody identification on
    // an expired specimen or panel lot follow the settings.
    [Fact]
    public void ReceiptRangeAndExpiredIdentification_FollowTheSettings()
    {
        Assert.Equal(RuleSeverity.HardStop, ReceiveTemperatureRule.Evaluate(true, 12m).Severity);
        Assert.Equal(RuleSeverity.Pass, ReceiveTemperatureRule.Evaluate(true, 12m, 1, 15).Severity);
        Assert.Contains("1–15 °C", ReceiveTemperatureRule.Evaluate(true, 16m, 1, 15).Message);

        RuleResult[] warnings =
        [
            RuleResult.Warning(AntibodyIdentificationWorkupScopeRule.SpecimenExpiredCode, "Specimen expired."),
            RuleResult.Warning(AntibodyPanelLotValidityRule.ExpiredCode, "Panel lot expired.")
        ];
        Assert.Empty(AntibodyIdentificationPolicy.Default.RefusedWarnings(warnings));
        var refused = (AntibodyIdentificationPolicy.Default with { CompleteOnExpiredSpecimen = false }).RefusedWarnings(warnings);
        var stop = Assert.Single(refused);
        Assert.Equal((AntibodyIdentificationWorkupScopeRule.SpecimenExpiredCode, RuleSeverity.HardStop), (stop.Code, stop.Severity));
        Assert.Single((AntibodyIdentificationPolicy.Default with { CompleteWithExpiredPanelLot = false }).RefusedWarnings(warnings),
            r => r.Code == AntibodyPanelLotValidityRule.ExpiredCode);
    }

    // TEST-BB-1361 (OCD-009): an inactive patient is refused for clinical use only when the facility turns it on; a
    // merged record is refused either way.
    [Fact]
    public void InactivePatient_FollowsTheSetting()
    {
        Assert.Equal(RuleSeverity.Pass, PatientMergeRule.EvaluateClinicalUse(PatientStatus.Inactive).Severity);
        Assert.Equal(RuleSeverity.HardStop, PatientMergeRule.EvaluateClinicalUse(PatientStatus.Inactive, blockInactive: true).Severity);
        Assert.Equal(RuleSeverity.HardStop, PatientMergeRule.EvaluateClinicalUse(PatientStatus.Merged).Severity);
        Assert.Equal(RuleSeverity.Pass, PatientMergeRule.EvaluateClinicalUse(PatientStatus.Active, blockInactive: true).Severity);
    }

    // TEST-BB-1362 (OCD-091, OCD-026): a paper result on a specimen expired since follows the setting for its kind (refused,
    // tested before expiry, or also in a recorded outage), and a received specimen not yet accepted may be refused for
    // antibody identification.
    [Fact]
    public void PaperResultsAndReceivedSpecimens_FollowTheSettings()
    {
        var expires = Now.AddDays(-1);
        var tested = Now.AddDays(-2);
        IReadOnlyCollection<DowntimeEpisode> none = [];
        IReadOnlyCollection<DowntimeEpisode> covering = [new DowntimeEpisode { StartedUtc = tested.AddHours(-1), EndedUtc = tested.AddHours(1) }];
        var test = ExpiredSpecimenBackEntryRule.Kind.PatientTest;
        var xm = ExpiredSpecimenBackEntryRule.Kind.Crossmatch;

        Assert.Equal(RuleSeverity.HardStop, ExpiredSpecimenBackEntryRule.EvaluateTime(test, tested, expires, none).Severity);
        Assert.Equal(RuleSeverity.Pass, ExpiredSpecimenBackEntryRule.EvaluateTime(test, tested, expires, covering).Severity);
        Assert.Equal(RuleSeverity.Pass, ExpiredSpecimenBackEntryRule.EvaluateTime(test, tested, expires, none, 1).Severity);
        Assert.Equal(RuleSeverity.Pass, ExpiredSpecimenBackEntryRule.EvaluateTime(xm, tested, expires, none).Severity);
        Assert.Equal(RuleSeverity.HardStop, ExpiredSpecimenBackEntryRule.EvaluateTime(xm, tested, expires, none, 2).Severity);
        Assert.Equal(RuleSeverity.HardStop, ExpiredSpecimenBackEntryRule.EvaluateTime(xm, tested, expires, covering, 0).Severity);

        RuleResult[] received = [AntibodyIdentificationWorkupScopeRule.EvaluateSpecimenReadiness(SpecimenStatus.Received, completing: true)];
        Assert.Equal(RuleSeverity.Warning, received[0].Severity);
        Assert.Empty(AntibodyIdentificationPolicy.Default.RefusedWarnings(received));
        Assert.Single((AntibodyIdentificationPolicy.Default with { OnReceivedSpecimen = false }).RefusedWarnings(received),
            r => r.Severity == RuleSeverity.HardStop);
    }
}
