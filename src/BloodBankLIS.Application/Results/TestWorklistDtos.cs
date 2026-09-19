using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Results;

public enum TestWorklistFilter
{
    Pending,
    Completed,
    All
}

public sealed record TestWorkItemDto(
    long OrderLineId,
    long OrderId,
    long PatientId,
    string PatientName,
    string Mrn,
    string TestCode,
    string TestName,
    string OrderNumber,
    OrderPriority Priority,
    DateTime OrderedUtc,
    string? AccessionNumber,
    long? SpecimenId,
    SpecimenStatus? SpecimenStatus,
    DateTime? SpecimenExpiresUtc,
    bool SpecimenExpired,
    string? CurrentBloodType,
    bool HasAntibodyHistory,
    string? AntibodySummary,
    long? CurrentResultId,
    ResultStatus? CurrentResultStatus,
    string? CurrentResultValue,
    string? CurrentResultInterpretation,
    ResultSource? CurrentResultSource,
    bool CanEnterResults,
    string? BlockReason,
    bool ElectronicXmEligible = false,
    bool FacilityAllowsElectronicCrossmatch = false,
    string? ElectronicXmBlockReason = null,
    string? ElectronicXmClinicalBlockReason = null,
    bool HasOpenAntibodyIdWorkup = false,
    long? OpenAntibodyIdWorkupId = null,
    string? CurrentResultEnteredBy = null,
    bool AboRhDeltaHold = false,
    string? AboRhDeltaDetail = null,
    string? Comment = null)
{
    public bool HasPostedInterfaceOrInstrumentValue =>
        CurrentResultStatus is ResultStatus.PendingVerification
        && CurrentResultSource is ResultSource.Interface or ResultSource.Instrument;

    public string BenchActionLabel => HasPostedInterfaceOrInstrumentValue ? "Verify" : "Enter";

    public bool IsAboSelfVerifyBlocked(string? userName) =>
        CurrentResultStatus is ResultStatus.Entered or ResultStatus.PendingVerification or ResultStatus.Corrected
        && string.Equals(TestCode, ResultService.AboRhTestCode, StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(CurrentResultEnteredBy)
        && string.Equals(CurrentResultEnteredBy, userName, StringComparison.OrdinalIgnoreCase);

    public bool OffersAntibodyIdHandoff =>
        HasOpenAntibodyIdWorkup
        || string.Equals(TestCode, "ABSC", StringComparison.OrdinalIgnoreCase)
        || string.Equals(TestCode, "ABID", StringComparison.OrdinalIgnoreCase);

    public string AntibodyIdHref
    {
        get
        {
            if (OpenAntibodyIdWorkupId is long workupId)
            {
                return $"/patients/{PatientId}/antibody-id/{workupId}";
            }

            var href = $"/patients/{PatientId}/antibody-id";
            if (SpecimenId is > 0)
            {
                href += $"?specimenId={SpecimenId}";
            }

            return href;
        }
    }

    public string AntibodyIdHandoffLabel => HasOpenAntibodyIdWorkup ? "ABID" : "Start ABID";

    public bool NeedsSpecimenLink =>
        SpecimenId is null or 0
        || BlockReason?.Contains("No specimen is linked", StringComparison.OrdinalIgnoreCase) == true;

    public string ChartAccessionHref => $"/patients/{PatientId}?tab=orders&panel=accession";
}
