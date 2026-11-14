using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Admin;

public sealed record InstrumentDataItemDto(string Key, string DisplayName, string Description, string DefaultAstmPath, bool Required);

public sealed record InstrumentConnectionDto(string? IpAddress, int Port, bool IsServer);

public sealed record InstrumentFieldMappingDto(string DataItemKey, string AstmPath, bool IsRequired, string? DisplayName = null);

/// <summary>One instrument translation row; <paramref name="Context"/> limits a value row to one LIS test.</summary>
public sealed record InstrumentTranslationDto(
    string DataItemKey,
    string InstrumentValue,
    string LocalValue,
    InterfaceTranslationDirection Direction,
    int SortOrder,
    string? Context = null,
    string? Description = null);

/// <summary>A translation field an instrument can fill, with the local values it may use (empty for any value).</summary>
public sealed record InstrumentTranslationFieldDto(
    string Key,
    string DisplayName,
    string Description,
    bool Outbound,
    bool AllowsContext,
    IReadOnlyList<InterfaceLocalValueDto> LocalValues);

/// <summary>What an instrument would do with a value, using its saved translations.</summary>
public sealed record InstrumentTranslationCheckDto(string Input, string Output, bool Translated, string? Context, string? Note);

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
    InstrumentInterpretationMatch InterpretationMatch,
    string? InterpretationAnalyteCodes,
    InstrumentConnectionDto Connection,
    IReadOnlyList<InstrumentFieldMappingDto> FieldMappings,
    IReadOnlyList<InstrumentTranslationDto> Translations,
    IReadOnlyList<InstrumentRuleDto> Rules);

public sealed record SaveInstrumentInterfaceRequest(
    string Name,
    string? Environment,
    string? ChangeReason,
    InstrumentConnectionDto Connection,
    IReadOnlyList<InstrumentFieldMappingDto>? FieldMappings,
    IReadOnlyList<InstrumentTranslationDto>? Translations,
    IReadOnlyList<InstrumentRuleDto>? Rules,
    InstrumentInterpretationMatch InterpretationMatch = InstrumentInterpretationMatch.AnalyteEqualsAssay,
    string? InterpretationAnalyteCodes = null);

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
    string? ErrorDetail,
    DateTime? ResolvedUtc = null,
    string? ResolvedBy = null,
    string? ResolutionNote = null)
{
    /// <summary>A failed message nobody has replayed successfully or resolved yet.</summary>
    public bool OpenFailure => Status == InstrumentMessageStatus.Failed && ResolvedUtc is null;

    /// <summary>An open failure whose result an instrument error code held: review it, then release or re-run.</summary>
    public bool Held => BloodBankLIS.Domain.Rules.InstrumentMessageFollowUpRule.IsHeld(Status, ResolvedUtc, ErrorDetail);
}

public sealed record InstrumentParsedFieldDto(string DataItem, string? Value);

public sealed record InstrumentMappedValueDto(string InstrumentValue, string ApplicationValue);

public sealed record InstrumentMessageDetailDto(
    InstrumentMessageSummaryDto Message,
    string RawMessage,
    IReadOnlyList<InstrumentParsedFieldDto> ParsedFields,
    IReadOnlyList<InstrumentMappedValueDto> MappedValues,
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

/// <summary>
/// A simulated send. With <paramref name="RawMessage"/> (the previewed message), exactly that message is sent; without
/// it, a new randomized message is generated (end-user testing, 2026-11-12: Send generated new values, so what posted
/// differed from the preview the user had just read).
/// </summary>
public sealed record EchoSimulatorSendRequest(long InterfaceId, EchoSimulatorPreviewRequest Preview, string? RawMessage = null)
{
    /// <summary>The previewed message as ASTM records (one per line in the preview, CR-terminated on the wire).</summary>
    public static string ToWire(string preview) =>
        string.Join('\r', preview.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0)) + "\r";
}

public sealed record EchoSimulatorSendResultDto(bool Sent, string? Error, long? MessageLogId, string RawMessage);
