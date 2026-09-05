using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Compliance;

/// <summary>Reads facility policy keys with AABB/FDA defaults when unset.</summary>
public sealed class FacilityPolicyService
{
    private readonly IRepository<SystemSetting> _settings;

    public FacilityPolicyService(IRepository<SystemSetting> settings)
    {
        _settings = settings;
    }

    public async Task<int> GetIntAsync(string key, int defaultValue, CancellationToken ct = default)
    {
        var row = await _settings.FirstOrDefaultAsync(s => s.Key == key, ct);
        return row is not null && int.TryParse(row.Value, out var parsed) ? parsed : defaultValue;
    }

    public async Task<bool> GetBoolAsync(string key, bool defaultValue, CancellationToken ct = default)
    {
        var row = await _settings.FirstOrDefaultAsync(s => s.Key == key, ct);
        return row is not null && bool.TryParse(row.Value, out var parsed) ? parsed : defaultValue;
    }

    public Task<int> GetSpecimenAlloHoursAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.SpecimenAlloimmunizationHours, SpecimenValidityPolicy.DefaultAlloimmunizationRiskHours, ct);

    public Task<int> GetSpecimenStandardHoursAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.SpecimenStandardHours, SpecimenValidityPolicy.DefaultStandardHours, ct);

    public Task<int> GetSpecimenLookbackDaysAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.SpecimenLookbackDays, SpecimenValidityPolicy.DefaultLookbackDays, ct);

    public Task<bool> GetRequireSecondVerifierAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.RequireSecondVerifier, false, ct);

    public Task<bool> GetRequireWardReceiptAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.RequireWardReceipt, true, ct);

    public Task<int> GetRetrospectiveCrossmatchDueHoursAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.RetrospectiveCrossmatchDueHours, 24, ct);

    public Task<int> GetInTransitDueHoursAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.InTransitDueHours, 4, ct);

    public Task<bool> GetRequireQuarantineReleaseVerifierAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.RequireQuarantineReleaseVerifier, true, ct);

    public Task<bool> GetRequireReceiveVisualInspectionAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.RequireReceiveVisualInspection, true, ct);

    public Task<bool> GetRequireReceiveVerifierAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.RequireReceiveVerifier, true, ct);

    public Task<bool> GetRequireReceiveTemperatureAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.RequireReceiveTemperature, true, ct);

    public Task<bool> GetRequireDiscardVerifierAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.RequireDiscardVerifier, true, ct);

    public Task<bool> GetRequireDirectedConversionVerifierAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.RequireDirectedConversionVerifier, true, ct);

    public Task<int> GetExpectedArrivalDueHoursAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.ExpectedArrivalDueHours, 24, ct);

    public Task<int> GetNearExpiryWarningHoursAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.NearExpiryWarningHours, 24, ct);

    public Task<bool> GetBlockSelfVerifyAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.BlockSelfVerify, false, ct);

    public Task<bool> GetBlockAboSelfVerifyAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.BlockAboSelfVerify, true, ct);

    public Task<bool> GetBlockRetypeSelfVerifyAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.BlockRetypeSelfVerify, true, ct);

    public Task<int> GetSignatureValidityMinutesAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.SignatureValidityMinutes, 15, ct);

    public Task<int> GetRetentionYearsAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.RetentionYears, 10, ct);

    public Task<bool> GetAllowElectronicCrossmatchAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.AllowElectronicCrossmatch, true, ct);

    public Task<bool> GetRequireSecondAboForCellularIssueAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.RequireSecondAboForCellularIssue, true, ct);

    public Task<bool> GetUncrossmatchedCellularMustBeGroupOAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.UncrossmatchedCellularMustBeGroupO, true, ct);

    public Task<bool> GetUncrossmatchedONegForChildbearingAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.UncrossmatchedONegForChildbearing, true, ct);

    public Task<int> GetChildbearingAgeYearsAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.ChildbearingAgeYears, 50, ct);

    public async Task<AntibodyIdentificationPolicy> GetAntibodyIdentificationPolicyAsync(CancellationToken ct = default)
    {
        var dosageAware = await GetBoolAsync(FacilityPolicyKeys.AntibodyIdDosageAware, true, ct);
        var minHom = await GetIntAsync(FacilityPolicyKeys.AntibodyIdMinHomozygousExclusions, 1, ct);
        var minHet = await GetIntAsync(FacilityPolicyKeys.AntibodyIdMinHeterozygousExclusions, 2, ct);
        var requireReview = await GetBoolAsync(FacilityPolicyKeys.AntibodyIdRequireSupervisorReview, true, ct);
        var blockSelf = await GetBoolAsync(FacilityPolicyKeys.AntibodyIdBlockSelfReview, true, ct);
        return new AntibodyIdentificationPolicy(
            dosageAware,
            Math.Clamp(minHom, 1, 5),
            Math.Clamp(minHet, 1, 5),
            requireReview,
            blockSelf,
            AntibodyIdentificationPolicy.DefaultDosageSensitiveCodes);
    }
}
