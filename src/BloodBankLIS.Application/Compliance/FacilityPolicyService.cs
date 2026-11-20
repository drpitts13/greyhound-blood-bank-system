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
        // Same default as the catalog and the seeded setting (OCD-008: a facility may turn it off).
        GetBoolAsync(FacilityPolicyKeys.RequireSecondVerifier, true, ct);

    public Task<int> GetRhigAdministrationWindowHoursAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.RhigAdministrationWindowHours, 0, ct);

    public Task<int> GetLookbackNotifyWithinDaysAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.LookbackNotifyWithinDays, 84, ct);

    public Task<bool> GetSendFinalResultsAutomaticallyAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.SendFinalResultsAutomatically, true, ct);

    public Task<bool> GetRequireWardReceiptAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.RequireWardReceipt, true, ct);

    public Task<int> GetRetrospectiveCrossmatchDueHoursAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.RetrospectiveCrossmatchDueHours, 24, ct);

    public Task<int> GetInTransitDueHoursAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.InTransitDueHours, 4, ct);

    public Task<int> GetTransfusionCompletionReminderHoursAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.TransfusionCompletionReminderHours, TransfusionCompletionRule.DefaultReminderHours, ct);

    public Task<int> GetReturnToAvailableMinutesAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.ReturnToAvailableMinutes, ReturnReissueRule.DefaultReturnToAvailableMinutes, ct);

    public Task<bool> GetRequireQuarantineReleaseVerifierAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.RequireQuarantineReleaseVerifier, true, ct);

    public Task<bool> GetRequireReceiveVisualInspectionAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.RequireReceiveVisualInspection, true, ct);

    public Task<bool> GetRequireReceiveVerifierAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.RequireReceiveVerifier, false, ct);

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

    public Task<int> GetSignatureValidityMinutesAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.SignatureValidityMinutes, 15, ct);

    public Task<int> GetRetentionYearsAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.RetentionYears, 10, ct);

    public Task<bool> GetAllowBatchVerifyAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.AllowBatchVerify, false, ct);

    /// <summary>OCD-074: whether the neonatal rules are on, and the age limit in days.</summary>
    public async Task<(bool Enabled, int AgeLimitDays)> GetNeonatalSettingsAsync(CancellationToken ct = default) =>
        (await GetBoolAsync(FacilityPolicyKeys.NeonatalRulesEnabled, false, ct),
            Math.Clamp(await GetIntAsync(FacilityPolicyKeys.NeonatalAgeLimitDays, NeonatalTestingRule.DefaultAgeLimitDays, ct), 1, 365));

    /// <summary>OCD-069: the RhIG dose settings; each 0 until the medical director sets it.</summary>
    public async Task<RhigDoseRule.Settings> GetRhigDoseSettingsAsync(CancellationToken ct = default) => new(
        await GetIntAsync(FacilityPolicyKeys.RhigMlPerVial, 0, ct),
        await GetIntAsync(FacilityPolicyKeys.RhigMaternalBloodVolumeMl, 0, ct),
        (RhigDoseRule.Rounding)Math.Clamp(await GetIntAsync(FacilityPolicyKeys.RhigDoseRounding, 0, ct), 0, 3));

    public Task<int> GetTemperatureReadingIntervalHoursAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.TemperatureReadingIntervalHours, 0, ct);

    public Task<int> GetSpecimenRetentionDaysAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.SpecimenRetentionDays, 0, ct);

    /// <summary>Minutes before a blood order's needed-by time that it counts as needed soon (OCD-075); 0 = off.</summary>
    public Task<int> GetMtpIdleReminderMinutesAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.MtpIdleReminderMinutes, MtpIdleRule.DefaultReminderMinutes, ct);

    /// <summary>Hours an unidentified chart stays open before the home card names it (OCD-077); 0 = off.</summary>
    public Task<int> GetUnidentifiedReminderHoursAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.UnidentifiedReminderHours, UnidentifiedPatientRule.DefaultReminderHours, ct);

    /// <summary>STAT test turnaround target in minutes (pass 106); 0 = not set.</summary>
    public Task<int> GetRushTestTurnaroundMinutesAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.RushTestTurnaroundMinutes, 0, ct);

    public Task<int> GetBloodOrderNeededBySoonMinutesAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.BloodOrderNeededBySoonMinutes, 0, ct);

    public Task<bool> GetRequireTransfusionConsentAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.RequireTransfusionConsent, false, ct);

    public Task<bool> GetRequireSpecimenCollectorAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.RequireSpecimenCollector, false, ct);

    public Task<bool> GetBlockResultsWhenReagentQcOpenAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.BlockResultsWhenReagentQcOpen, false, ct);

    public Task<int> GetFinalDispositionDueHoursAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.FinalDispositionDueHours, FinalDispositionDueRule.DefaultDueHours, ct);

    public Task<bool> GetRequireModificationSupplyLotAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.RequireModificationSupplyLot, false, ct);

    public Task<bool> GetRequireIrradiationRecordAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.RequireIrradiationRecord, false, ct);

    public Task<bool> GetRequirePrivilegedMfaAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.RequirePrivilegedMfa, false, ct);

    public Task<bool> GetMfaEnabledAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.MfaEnabled, false, ct);

    public Task<bool> GetRequireAllUsersMfaAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.RequireAllUsersMfa, false, ct);

    public Task<int> GetExcursionQuarantineMinutesAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.ExcursionQuarantineMinutes, 30, ct);

    public Task<bool> GetPbmEvaluateOrdersAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.PbmEvaluateOrders, false, ct);

    public Task<bool> GetPbmRequireJustificationAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.PbmRequireJustification, false, ct);

    public Task<bool> GetRequireSpecimenReceiptByScanAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.RequireSpecimenReceiptByScan, false, ct);

    public Task<bool> GetPrefillPatientIdentifiersAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.PrefillPatientIdentifiers, false, ct);

    public Task<bool> GetInspectionDefaultAcceptableAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.InspectionDefaultAcceptable, false, ct);

    public Task<bool> GetAllowElectronicCrossmatchAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.AllowElectronicCrossmatch, false, ct);

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
            FacilityPolicyLists.Set(await GetTextAsync(FacilityPolicyKeys.AntibodyIdDosageSensitiveAntigens, ct), StringComparer.Ordinal))
        {
            RequireExclusionRationale = await GetBoolAsync(FacilityPolicyKeys.AntibodyIdRequireExclusionRationale, true, ct),
            CompleteOnExpiredSpecimen = await GetBoolAsync(FacilityPolicyKeys.AntibodyIdCompleteOnExpiredSpecimen, true, ct),
            CompleteWithExpiredPanelLot = await GetBoolAsync(FacilityPolicyKeys.AntibodyIdCompleteWithExpiredPanelLot, true, ct)
        };
    }

    // Medical director decisions recorded as settings (2026-11-20); each default is the decision previously built in.
    public Task<int> GetDeviationFdaReportabilityToCloseAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.DeviationFdaReportabilityToClose, 2, ct);

    public Task<bool> GetRetrospectiveIncompatibleOpensDeviationAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.RetrospectiveIncompatibleOpensDeviation, true, ct);

    public Task<bool> GetRetrospectiveCrossmatchOnExpiredSpecimenAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.RetrospectiveCrossmatchOnExpiredSpecimen, true, ct);

    public Task<bool> GetEmergencyReleaseBeforeSpecimenAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.EmergencyReleaseBeforeSpecimen, true, ct);

    public Task<bool> GetReactionWorkupOnExpiredSpecimenAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.ReactionWorkupOnExpiredSpecimen, true, ct);

    public Task<bool> GetReactionCloseRequiresConclusionAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.ReactionCloseRequiresConclusion, true, ct);

    public Task<int> GetRhigDVariantHandlingAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.RhigDVariantHandling, 1, ct);

    public Task<int> GetReactionUnderInvestigationIssueAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.ReactionUnderInvestigationIssue, 1, ct);

    public Task<int> GetBackEntryOutsideOutageAsync(CancellationToken ct = default) =>
        GetIntAsync(FacilityPolicyKeys.BackEntryOutsideOutage, 1, ct);

    public Task<bool> GetVerifyAfterSpecimenExpiryAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.VerifyAfterSpecimenExpiry, true, ct);

    /// <summary>A list policy's value; the catalog default when unset or no longer valid.</summary>
    public async Task<string> GetTextAsync(string key, CancellationToken ct = default)
    {
        var row = await _settings.FirstOrDefaultAsync(s => s.Key == key, ct);
        var value = row?.Value?.Trim();
        return string.IsNullOrEmpty(value) || FacilityPolicyLists.Problem(key, value) is not null
            ? FacilityPolicyCatalog.TextDefault(key)
            : value;
    }

    public async Task<IReadOnlySet<string>> GetInboundResultStatusesAsync(CancellationToken ct = default) =>
        FacilityPolicyLists.Set(await GetTextAsync(FacilityPolicyKeys.InboundResultStatuses, ct), StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlySet<string>> GetBatchVerifyTestsAsync(CancellationToken ct = default) =>
        FacilityPolicyLists.Set(await GetTextAsync(FacilityPolicyKeys.BatchVerifyTests, ct), StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<string>> GetGenotypeMethodWordsAsync(CancellationToken ct = default) =>
        FacilityPolicyLists.Items(await GetTextAsync(FacilityPolicyKeys.AntibodyIdGenotypeMethodWords, ct));

    public async Task<IReadOnlyDictionary<string, int>> GetTiterGradeScoresAsync(CancellationToken ct = default) =>
        FacilityPolicyLists.Scores(await GetTextAsync(FacilityPolicyKeys.TiterGradeScores, ct));

    /// <summary>OCD-005: the receipt temperature range (whole °C), defaulting to 1 to 10.</summary>
    public async Task<(int Min, int Max)> GetReceiveTemperatureRangeAsync(CancellationToken ct = default) =>
        (await GetIntAsync(FacilityPolicyKeys.ReceiveTemperatureMinC, 1, ct), await GetIntAsync(FacilityPolicyKeys.ReceiveTemperatureMaxC, 10, ct));

    public Task<bool> GetBlockInactivePatientClinicalUseAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.BlockInactivePatientClinicalUse, false, ct);

    public Task<bool> GetCorrectAfterSpecimenExpiryAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.CorrectAfterSpecimenExpiry, true, ct);

    public Task<bool> GetProductChargeOnTransfuseAsync(CancellationToken ct = default) =>
        GetBoolAsync(FacilityPolicyKeys.ProductChargeOnTransfuse, false, ct);
}
