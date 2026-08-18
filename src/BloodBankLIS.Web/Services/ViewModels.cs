using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Web.Services;

// View models for API responses whose source records live in the Api/Printing projects
// (not referenced by the UI). Shapes mirror the JSON the API emits.

public sealed record Hl7MessageVm(
    long Id,
    Hl7Direction Direction,
    string MessageType,
    string? TriggerEvent,
    string MessageControlId,
    Hl7MessageStatus Status,
    DateTime ReceivedUtc,
    DateTime? ProcessedUtc,
    string? AckCode,
    string? ErrorDetail);

public sealed record Hl7MessageDetailVm(Hl7MessageVm Message, string? Raw);

public sealed record Hl7ErrorVm(
    long Id,
    long Hl7MessageId,
    string ErrorType,
    string ErrorDetail,
    int RetryCount,
    DateTime? NextRetryUtc,
    bool Resolved);

public sealed record Hl7ReplayVm(string AckCode, string Ack, long LogId);

public sealed record PrintJobVm(
    long Id,
    PrintJobType JobType,
    string TemplateCode,
    LabelFormat Format,
    string? TargetPrinter,
    string? ContextType,
    long? ContextId,
    PrintJobStatus Status,
    bool IsReprint,
    string? ReprintReason,
    string PrintedBy,
    DateTime? PrintedUtc,
    string? RenderedZpl);

public sealed record AuditEventVm(
    long Id,
    AuditEventType EventType,
    string EntityType,
    long? EntityId,
    string UserName,
    string? Workstation,
    DateTime OccurredUtc,
    string? Reason,
    long? SignatureId);

public sealed record AuditPageVm(int Total, int Skip, int Take, List<AuditEventVm> Items);

public sealed record MeVm(string UserName, string DisplayName, int SecurityLevel, string[] Permissions);

public sealed record LoginRequestVm(string UserName, string? Password = null, string? Workstation = null);

public sealed record ExpireDueVm(int Expired);

public sealed record SignatureCreatedVm(long Id);

public sealed record ModificationResultVm(long ModificationId, List<BloodUnitDto> ResultUnits);

public sealed record TransferRequestVm(long ToLocationId, string? Reason);
public sealed record ReasonRequestVm(string Reason);
public sealed record PrintRequestVm(LabelFormat Format = LabelFormat.Zpl, string? TemplateCode = null, string? TargetPrinter = null);
public sealed record SignatureRequestVm(
    string Action,
    string MeaningOfSignature,
    string? ContextType = null,
    long? ContextId = null,
    string? ReauthenticationSecret = null);
