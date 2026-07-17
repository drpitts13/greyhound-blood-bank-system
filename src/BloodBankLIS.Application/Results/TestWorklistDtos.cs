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
    long? CurrentResultId,
    ResultStatus? CurrentResultStatus,
    string? CurrentResultValue,
    string? CurrentResultInterpretation,
    bool CanEnterResults,
    string? BlockReason);
