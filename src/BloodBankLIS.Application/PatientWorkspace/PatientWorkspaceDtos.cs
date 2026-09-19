using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.PatientWorkspace;

public sealed record CreateEncounterRequest(
    string VisitNumber,
    EncounterType EncounterType,
    EncounterStatus Status,
    DateTime? AdmitUtc,
    DateTime? DischargeUtc,
    string? AccountNumber,
    long? AttendingProviderId,
    string? AdmissionLocation,
    string? CurrentLocation,
    string? DischargeDisposition,
    string? FinancialClass,
    string? SourceSystem,
    string? ExternalVisitId);

public sealed record UpdateEncounterRequest(
    EncounterType EncounterType,
    EncounterStatus Status,
    DateTime? AdmitUtc,
    DateTime? DischargeUtc,
    string? AccountNumber,
    long? AttendingProviderId,
    string? AdmissionLocation,
    string? CurrentLocation,
    string? DischargeDisposition,
    string? FinancialClass);

public sealed record EncounterDto(
    long Id,
    long PatientId,
    string VisitNumber,
    string? AccountNumber,
    EncounterType EncounterType,
    EncounterStatus Status,
    DateTime? AdmitUtc,
    DateTime? DischargeUtc,
    long? AttendingProviderId,
    string? AttendingProvider,
    string? AdmissionLocation,
    string? CurrentLocation,
    string? DischargeDisposition,
    string? FinancialClass,
    string? SourceSystem,
    string? ExternalVisitId,
    DateTime CreatedUtc,
    string CreatedBy)
{
    public static EncounterDto From(Encounter e) => new(
        e.Id, e.PatientId, e.VisitNumber, e.AccountNumber, e.EncounterType, e.Status,
        e.AdmitUtc, e.DischargeUtc, e.AttendingProviderId, e.AttendingProvider, e.AdmissionLocation, e.CurrentLocation,
        e.DischargeDisposition, e.FinancialClass, e.SourceSystem, e.ExternalVisitId,
        e.CreatedUtc, e.CreatedBy);
}

public sealed record OrderLineInputDto(
    OrderCategory LineCategory,
    string? TestCode,
    long? ProductTypeId);

public sealed record OrderLineDto(
    long Id,
    int LineNumber,
    OrderCategory LineCategory,
    string LineName,
    string? TestCode,
    OrderType OrderType,
    long? ProductTypeId,
    FulfillmentStatus? FulfillmentStatus,
    ResultStatus? ResultStatus,
    string? Comment = null)
{
    public static OrderLineDto From(OrderLine line) => new(
        line.Id, line.LineNumber, line.LineCategory, line.LineName, line.TestCode, line.OrderType,
        line.ProductTypeId, line.FulfillmentStatus, line.ResultStatus, line.Comment);
}

public sealed record CreateOrderRequest(
    long EncounterId,
    long OrderingLocationId,
    string OrderNumber,
    IReadOnlyList<OrderLineInputDto> Lines,
    OrderPriority Priority,
    DateTime OrderedUtc,
    long? OrderingProviderId,
    OrderSource Source,
    string? SourceSystem,
    string? OrderedByUser,
    long? SpecimenId = null,
    string? Comment = null);

public sealed record CancelOrderRequest(string CancellationReason);

public sealed record LinkOrderSpecimenRequest(long SpecimenId);

public sealed record UpdateOrderRequest(
    long EncounterId,
    long OrderingLocationId,
    IReadOnlyList<OrderLineInputDto> Lines,
    OrderPriority Priority,
    long? OrderingProviderId,
    string? OverrideReason = null,
    string? AuthorizedBy = null,
    string? Comment = null);

public sealed record UpdateOrderLineCommentRequest(string? Comment);

public sealed record PatientOrderDto(
    long Id,
    long PatientId,
    long EncounterId,
    string VisitNumber,
    string OrderNumber,
    OrderCategory OrderCategory,
    string OrderName,
    OrderPriority Priority,
    OrderStatus Status,
    long OrderingLocationId,
    string OrderingLocationName,
    long? OrderingProviderId,
    string? OrderingProvider,
    long? SpecimenId,
    string? AccessionNumber,
    ResultStatus? ResultStatus,
    FulfillmentStatus? FulfillmentStatus,
    OrderSource Source,
    DateTime OrderedUtc,
    string? CancellationReason,
    IReadOnlyList<OrderLineDto> Lines,
    string? Comment = null,
    long? CurrentResultId = null,
    string? CurrentResultValue = null,
    ResultSource? CurrentResultSource = null,
    string? CurrentResultEnteredBy = null,
    string? CurrentResultTestCode = null)
{
    public bool IsUrgentPriority =>
        Priority is OrderPriority.Stat or OrderPriority.Urgent or OrderPriority.EmergencyRelease
            or OrderPriority.MassiveTransfusionProtocol;

    public bool IsActive =>
        Status is not (OrderStatus.Completed or OrderStatus.Cancelled or OrderStatus.Discontinued);

    public bool HasPostedInterfaceOrInstrumentValue =>
        ResultStatus is BloodBankLIS.Domain.Enums.ResultStatus.PendingVerification
        && CurrentResultId is not null
        && CurrentResultSource is ResultSource.Interface or ResultSource.Instrument;

    public string BenchActionLabel => HasPostedInterfaceOrInstrumentValue ? "Verify" : "Edit";
}

public sealed record PatientProductHistoryRowDto(
    long EventId,
    string EventSourceType,
    PatientProductHistoryEventType EventType,
    DateTime EventUtc,
    string? UnitNumber,
    string? ProductTypeName,
    string? UnitBloodType,
    string? PatientBloodType,
    string? VisitNumber,
    string? OrderNumber,
    string? AccessionNumber,
    string? CompatibilityStatus,
    string? PerformedBy,
    string? IssuedToLocation,
    string? ReturnedBy,
    DateTime? TransfusionStartUtc,
    DateTime? TransfusionStopUtc,
    decimal? VolumeTransfused,
    string? FinalDisposition,
    bool ReactionSuspected,
    bool HasMissingVisitContext,
    bool IsOpenAssignment,
    string? PatientIdentificationMethod = null,
    string? Transfusionist = null,
    long? UnitId = null,
    long? IssueId = null);

public sealed record PatientTestHistoryRowDto(
    long ResultId,
    DateTime VerifiedUtc,
    string TestCode,
    string TestName,
    string? Value,
    string? Interpretation,
    string AccessionNumber,
    long SpecimenId,
    long? OrderId,
    string? OrderNumber,
    int Version,
    string? VerifiedBy,
    ResultStatus Status,
    ResultSource Source,
    bool IsCurrent,
    string? Reason);
