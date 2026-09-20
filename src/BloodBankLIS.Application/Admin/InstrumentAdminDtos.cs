using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Admin;

public sealed record InstrumentDataItemDto(string Key, string DisplayName, string Description, string DefaultAstmPath, bool Required);

public sealed record InstrumentConnectionDto(string? IpAddress, int Port, bool IsServer);

public sealed record InstrumentFieldMappingDto(string DataItemKey, string AstmPath, bool IsRequired, string? DisplayName = null);

public sealed record InstrumentTestMappingDto(
    string InstrumentCode,
    string ApplicationCode,
    InstrumentMappingKind Kind,
    InterfaceTranslationDirection Direction,
    int SortOrder);

public sealed record InstrumentRuleDto(
    string Name,
    string ConditionExpression,
    string ActionExpression,
    int SortOrder,
    bool IsEnabled,
    bool StopOnMatch);

public sealed record InstrumentInterfaceDto(
    long Id,
    string Name,
    bool IsEnabled,
    string? Environment,
    int Version,
    InstrumentConnectionDto Connection,
    IReadOnlyList<InstrumentFieldMappingDto> FieldMappings,
    IReadOnlyList<InstrumentTestMappingDto> TestMappings,
    IReadOnlyList<InstrumentRuleDto> Rules);

public sealed record SaveInstrumentInterfaceRequest(
    string Name,
    string? Environment,
    string? ChangeReason,
    InstrumentConnectionDto Connection,
    IReadOnlyList<InstrumentFieldMappingDto>? FieldMappings,
    IReadOnlyList<InstrumentTestMappingDto>? TestMappings,
    IReadOnlyList<InstrumentRuleDto>? Rules);

public sealed record InstrumentStatusDto(
    long Id,
    string Name,
    bool IsEnabled,
    bool IsServer,
    string? IpAddress,
    int Port,
    string State,
    string? Detail,
    DateTime? LastEventUtc);

public sealed record InstrumentLogDto(
    long Id,
    long InterfaceId,
    DateTime OccurredUtc,
    string Level,
    string EventType,
    string Message);

public sealed record InstrumentMessageSummaryDto(
    long Id,
    long InterfaceId,
    Hl7Direction Direction,
    InstrumentMessageKind MessageKind,
    InstrumentMessageStatus Status,
    DateTime ReceivedUtc,
    DateTime? ProcessedUtc,
    string? SpecimenId,
    string? PatientId,
    string? ErrorDetail);

public sealed record InstrumentMessageDetailDto(
    InstrumentMessageSummaryDto Message,
    string RawMessage,
    string? ParsedJson,
    string? MappedJson,
    string? RuleTraceJson);

public sealed record EchoSimulatorPreviewRequest(
    string Assay,
    string SpecimenId,
    string? PatientId,
    string? LastName,
    string? FirstName,
    string? DateOfBirth,
    string? Sex,
    int? Seed);

public sealed record EchoSimulatorPreviewDto(string Assay, string RawMessage, IReadOnlyList<string> Records);

public sealed record EchoSimulatorSendRequest(long InterfaceId, EchoSimulatorPreviewRequest Preview);

public sealed record EchoSimulatorSendResultDto(bool Sent, string? Error, long? MessageLogId, string RawMessage);
