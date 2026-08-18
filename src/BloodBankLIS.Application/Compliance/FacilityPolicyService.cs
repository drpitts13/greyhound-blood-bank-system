using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

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

    public Task<bool> GetBlockSelfVerifyAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.BlockSelfVerify, false, ct);

    public Task<int> GetSignatureValidityMinutesAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.SignatureValidityMinutes, 15, ct);

    public Task<int> GetRetentionYearsAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.RetentionYears, 10, ct);
}
