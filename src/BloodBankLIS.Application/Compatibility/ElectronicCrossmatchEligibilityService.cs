using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
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
        [ElectronicCrossmatchEligibilityRule.HistoryCode] = "No significant antibody history"
    };

    private readonly IRepository<Patient> _patients;
    private readonly IRepository<PatientBloodTypeHistory> _bloodTypes;
    private readonly IRepository<AntibodyHistory> _antibodies;
    private readonly AntibodyScreenCompatLoader _antibodyScreen;
    private readonly FacilityPolicyService _policy;

    public ElectronicCrossmatchEligibilityService(
        IRepository<Patient> patients,
        IRepository<PatientBloodTypeHistory> bloodTypes,
        IRepository<AntibodyHistory> antibodies,
        AntibodyScreenCompatLoader antibodyScreen,
        FacilityPolicyService policy)
    {
        _patients = patients;
        _bloodTypes = bloodTypes;
        _antibodies = antibodies;
        _antibodyScreen = antibodyScreen;
        _policy = policy;
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

        var clinical = ElectronicCrossmatchEligibilityRule.EvaluateCriteria(
            currentConfirmed, screenNegative, hasAntibodyHistory, secondAbo);
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
