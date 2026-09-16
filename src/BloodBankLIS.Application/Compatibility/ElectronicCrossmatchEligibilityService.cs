using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Compatibility;

public sealed record ElectronicCrossmatchCriterionDto(
    string Code,
    string Label,
    bool Satisfied,
    string Detail);

public sealed record ElectronicCrossmatchEligibilityDto(
    long PatientId,
    bool Eligible,
    bool FacilityAllowsElectronicCrossmatch,
    IReadOnlyList<ElectronicCrossmatchCriterionDto> Criteria,
    string? BlockingReason);

/// <summary>
/// SafeTrace / SoftBank electronic XM eligibility board: AABB 5.16 criteria plus
/// the facility allow-EXM policy, shown on the patient workspace.
/// </summary>
public sealed class ElectronicCrossmatchEligibilityService
{
    private static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [ElectronicCrossmatchEligibilityRule.FacilityCode] = "Facility policy allows electronic XM",
        [ElectronicCrossmatchEligibilityRule.CurrentTypeCode] = "Current ABO/Rh confirmed",
        [ElectronicCrossmatchEligibilityRule.SecondTypeCode] = "Second concordant ABO/Rh",
        [ElectronicCrossmatchEligibilityRule.ScreenCode] = "Antibody screen negative",
        [ElectronicCrossmatchEligibilityRule.HistoryCode] = "No significant antibody history",
        [ElectronicCrossmatchEligibilityRule.WorkupOpenCode] = "No open antibody-identification workup",
        [ElectronicCrossmatchEligibilityRule.ExtendedXmCode] = "No extended-crossmatch special requirement",
        [ElectronicCrossmatchEligibilityRule.VisitsCode] = "Minimum negative-screen visits",
        [ElectronicCrossmatchEligibilityRule.SpecimensCode] = "Minimum negative-screen specimens",
        [ElectronicCrossmatchEligibilityRule.TestsCode] = "Minimum negative antibody screens"
    };

    private readonly IRepository<Patient> _patients;
    private readonly IRepository<PatientBloodTypeHistory> _bloodTypes;
    private readonly IRepository<AntibodyHistory> _antibodies;
    private readonly IRepository<AntibodyIdentificationWorkup> _workups;
    private readonly AntibodyScreenCompatLoader _antibodyScreen;
    private readonly FacilityPolicyService _policy;
    private readonly CrossmatchSettingsReader? _crossmatchSettings;

    public ElectronicCrossmatchEligibilityService(
        IRepository<Patient> patients,
        IRepository<PatientBloodTypeHistory> bloodTypes,
        IRepository<AntibodyHistory> antibodies,
        IRepository<AntibodyIdentificationWorkup> workups,
        AntibodyScreenCompatLoader antibodyScreen,
        FacilityPolicyService policy,
        CrossmatchSettingsReader? crossmatchSettings = null)
    {
        _patients = patients;
        _bloodTypes = bloodTypes;
        _antibodies = antibodies;
        _workups = workups;
        _antibodyScreen = antibodyScreen;
        _policy = policy;
        _crossmatchSettings = crossmatchSettings;
    }

    public async Task<ElectronicCrossmatchEligibilityDto?> AssessAsync(long patientId, CancellationToken ct = default)
    {
        if (await _patients.GetByIdAsync(patientId, ct) is null)
        {
            return null;
        }

        var facilityAllows = await _policy.GetAllowElectronicCrossmatchAsync(ct);
        var history = await _bloodTypes.ListAsync(h => h.PatientId == patientId, ct);
        var currentConfirmed = history.Any(h => h.IsCurrent && h.BloodType.IsKnown);
        var secondAbo = SecondAboDeterminationRule.HasSecondConcordant(
            history.Select(h => new SecondAboDeterminationRule.Determination(h.BloodType, h.IsCurrent)).ToList());
        // Deactivated ("currently undetectable") antibodies still block computer XM.
        var hasAntibodyHistory = await _antibodies.AnyAsync(a => a.PatientId == patientId, ct);
        var screenNegative = !await _antibodyScreen.HasPositiveAntibodyScreenAsync(patientId, ct);
        var hasOpenWorkup = await _workups.AnyAsync(
            w => w.PatientId == patientId
                && (w.Status == AntibodyWorkupStatus.InProgress
                    || w.Status == AntibodyWorkupStatus.PendingInterpretation
                    || w.Status == AntibodyWorkupStatus.PendingSupervisorReview),
            ct);

        var requiresExtendedXm = await _antibodyScreen.HasActiveExtendedCrossmatchRequirementAsync(patientId, ct);
        var counts = await _antibodyScreen.CountNegativeAntibodyScreensAsync(patientId, ct);
        var settings = _crossmatchSettings is null
            ? null
            : await _crossmatchSettings.GetActiveAsync(ct);
        var clinical = ElectronicCrossmatchEligibilityRule.EvaluateCriteria(
            currentConfirmed,
            screenNegative,
            hasAntibodyHistory,
            secondAbo,
            hasOpenWorkup,
            requiresExtendedXm,
            counts.Visits,
            counts.Specimens,
            counts.Tests,
            settings?.ElectronicXmMinimumVisits ?? 0,
            settings?.ElectronicXmMinimumSpecimens ?? 0,
            settings?.ElectronicXmMinimumTests ?? 0);
        var facility = facilityAllows
            ? RuleResult.Pass(ElectronicCrossmatchEligibilityRule.FacilityCode, "Electronic XM is enabled in facility policy.")
            : RuleResult.HardStop(
                ElectronicCrossmatchEligibilityRule.FacilityCode,
                "Electronic crossmatch is disabled in facility policy until AABB 5.16 validation is complete.");

        var criteria = new[] { facility }.Concat(clinical)
            .Select(r => new ElectronicCrossmatchCriterionDto(
                r.Code,
                Labels.GetValueOrDefault(r.Code, r.Code),
                r.Severity == RuleSeverity.Pass,
                r.Message))
            .ToList();

        var blocking = criteria.Where(c => !c.Satisfied).Select(c => c.Detail).FirstOrDefault();
        return new ElectronicCrossmatchEligibilityDto(
            patientId,
            criteria.All(c => c.Satisfied),
            facilityAllows,
            criteria,
            blocking);
    }
}
