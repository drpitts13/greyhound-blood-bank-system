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
    string? CurrentResultEnteredBy = null)
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
}
