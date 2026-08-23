using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Admin;

// ---- Test definitions ----

public sealed record SubtestChoiceDto(string Code, string Label, ReactionPolarity? Polarity);

public sealed record SubtestDefinitionDto(
    long Id,
    string Code,
    string Name,
    SubtestResultType ResultType,
    IReadOnlyList<SubtestChoiceDto> Choices,
    int Version,
    bool IsActive,
    bool IsDraft,
    DateTime? EffectiveUtc,
    DateTime? RetiredUtc,
    string? ChangeReason);

public sealed record SaveSubtestDefinitionRequest(
    string Code,
    string Name,
    SubtestResultType ResultType,
    IReadOnlyList<SubtestChoiceDto>? Choices,
    string? ChangeReason);

public sealed record PanelSubtestAssignmentDto(
    string SubtestCode,
    bool Required,
    int SortOrder = 0,
    IReadOnlyList<string>? PhaseCodes = null);

public sealed record InterpretationLogicRowDto(
    string InterpretationKey,
    string Label,
    IReadOnlyDictionary<string, ReactionPolarity> SubtestExpectations,
    InterpretationMatchMode MatchMode = InterpretationMatchMode.AllMatch);

public sealed record PhaseDefinitionDto(
    long Id,
    string Code,
    string Name,
    int SortOrder,
    bool IncludeInInterpretation,
    bool IsCheckCell,
    string? ValidatesPhaseCode,
    int Version,
    bool IsActive,
    bool IsDraft,
    DateTime? EffectiveUtc,
    DateTime? RetiredUtc,
    string? ChangeReason);

public sealed record SavePhaseDefinitionRequest(
    string Code,
    string Name,
    int SortOrder,
    bool IncludeInInterpretation,
    bool IsCheckCell,
    string? ValidatesPhaseCode,
    string? ChangeReason);

public sealed record PhaseListItemDto(
    string Code,
    string Name,
    int SortOrder,
    bool IncludeInInterpretation,
    bool IsCheckCell,
    string? ValidatesPhaseCode);

/// <summary>Legacy inline panel row; retained for backward-compatible API responses.</summary>
public sealed record PanelSubtestDefinitionDto(
    string Code,
    string Label,
    bool Required,
    int SortOrder = 0);

public sealed record TestDefinitionDto(
    long Id,
    string Code,
    string Name,
    TestCategory Category,
    ResultValueType ResultValueType,
    string? AllowedResultValues,
    IReadOnlyList<PanelSubtestAssignmentDto> PanelSubtestAssignments,
    IReadOnlyList<InterpretationLogicRowDto> InterpretationLogic,
    string? RequiredSpecimenType,
    string? TestingMethod,
    string? PerformingDepartment,
    int SortOrder,
    bool Billable,
    string? ChargeCodeMapping,
    bool VerificationRequired,
    bool ContributesToAboRhHistory,
    bool ContributesToAntibodyHistory,
    bool ContributesToCompatibility,
    IReadOnlyList<string> BloodAttributeScopeCodes,
    BloodAttributeKind? BloodAttributeScopeKind,
    bool ContributesToUnitBloodAttributes,
    int Version,
    bool IsActive,
    bool IsDraft,
    DateTime? EffectiveUtc,
    DateTime? RetiredUtc,
    string? ChangeReason);

public sealed record SaveTestDefinitionRequest(
    string Code,
    string Name,
    TestCategory Category,
    ResultValueType ResultValueType,
    string? AllowedResultValues,
    IReadOnlyList<PanelSubtestAssignmentDto>? PanelSubtestAssignments,
    IReadOnlyList<InterpretationLogicRowDto>? InterpretationLogic,
    string? RequiredSpecimenType,
    string? TestingMethod,
    string? PerformingDepartment,
    int SortOrder,
    bool Billable,
    string? ChargeCodeMapping,
    bool VerificationRequired,
    bool ContributesToAboRhHistory,
    bool ContributesToAntibodyHistory,
    bool ContributesToCompatibility,
    IReadOnlyList<string>? BloodAttributeScopeCodes,
    BloodAttributeKind? BloodAttributeScopeKind,
    bool ContributesToUnitBloodAttributes,
    string? ChangeReason);

// ---- Test groupers ----

public sealed record TestGrouperMemberDto(string TestCode, int SortOrder = 0);

public sealed record TestGrouperDto(
    long Id,
    string Code,
    string Name,
    IReadOnlyList<TestGrouperMemberDto> Members,
    int Version,
    bool IsActive,
    bool IsDraft,
    DateTime? EffectiveUtc,
    DateTime? RetiredUtc,
    string? ChangeReason);

public sealed record SaveTestGrouperRequest(
    string Code,
    string Name,
    IReadOnlyList<TestGrouperMemberDto>? Members,
    string? ChangeReason);

// ---- Reflex rules ----

public sealed record ReflexRuleDto(
    long Id,
    string Code,
    string Name,
    string TriggerTestCode,
    string TriggerResultValue,
    string ReflexTestCode,
    int Version,
    bool IsActive,
    bool IsDraft,
    DateTime? EffectiveUtc,
    DateTime? RetiredUtc,
    string? ChangeReason);

public sealed record SaveReflexRuleRequest(
    string Code,
    string Name,
    string TriggerTestCode,
    string TriggerResultValue,
    string ReflexTestCode,
    string? ChangeReason);

// ---- Order and test rules ----

public sealed record RuleDefinitionDto(
    long Id,
    string Code,
    string Name,
    string? Description,
    RuleLevel Level,
    int Priority,
    bool StopOnMatch,
    string ConditionExpression,
    string ActionExpression,
    int Version,
    bool IsActive,
    bool IsDraft,
    DateTime? EffectiveUtc,
    DateTime? RetiredUtc,
    string? ChangeReason);

public sealed record SaveRuleDefinitionRequest(
    string Code,
    string Name,
    string? Description,
    RuleLevel Level,
    int Priority,
    bool StopOnMatch,
    string ConditionExpression,
    string ActionExpression,
    string? ChangeReason);

public sealed record ValidateRuleRequest(
    RuleLevel Level,
    string ConditionExpression,
    string ActionExpression);

public sealed record RuleMessageDto(string Code, string Severity, string Message);

public sealed record RuleValidationDto(
    bool IsValid,
    IReadOnlyList<RuleMessageDto> HardStops,
    IReadOnlyList<RuleMessageDto> Warnings,
    IReadOnlyList<string> ParsedActions);

public sealed record RuleAttributeDto(
    string Path,
    string Kind,
    string Description,
    string Example,
    string AvailableTo);

public sealed record RuleFunctionDto(
    string Name,
    string Kind,
    string Description,
    string Example,
    string AvailableTo);

public sealed record RuleActionDto(
    string Name,
    string Description,
    string Example,
    string AvailableTo);

public sealed record RuleOperatorDto(
    string Symbol,
    string Description,
    string Example);

/// <summary>Everything the rule authoring UI needs to describe one level's vocabulary.</summary>
public sealed record RuleVocabularyDto(
    RuleLevel Level,
    IReadOnlyList<RuleAttributeDto> Attributes,
    IReadOnlyList<RuleFunctionDto> Functions,
    IReadOnlyList<RuleActionDto> Actions);

/// <summary>
/// The complete authoring reference across both levels, generated from the rule catalog so
/// the help can never describe a vocabulary the engine does not actually support.
/// </summary>
public sealed record RuleHelpDto(
    IReadOnlyList<RuleAttributeDto> Attributes,
    IReadOnlyList<RuleFunctionDto> Functions,
    IReadOnlyList<RuleOperatorDto> Operators,
    IReadOnlyList<RuleActionDto> Actions);

// ---- Products ----

public sealed record ProductDefinitionDto(
    long Id,
    string ProductCode,
    string Name,
    ComponentClass ComponentClass,
    string? Category,
    int? DefaultShelfLifeHours,
    bool RequiresCrossmatch,
    bool RequiresAboMatch,
    bool RequiresRhMatch,
    string? Isbt128ProductCode,
    string? DefaultChargeCode,
    string? StorageRequirements,
    string? IssueRules,
    string? ReturnRules,
    string? ModificationRules,
    int Version,
    bool IsActive,
    IReadOnlyList<ProductAttributeAssignmentDto> Attributes);

public sealed record ProductAttributeAssignmentDto(long AttributeId, string Code, string Name, bool IsRequired);

public sealed record SaveProductDefinitionRequest(
    string ProductCode,
    string Name,
    ComponentClass ComponentClass,
    string? Category,
    int? DefaultShelfLifeHours,
    bool RequiresCrossmatch,
    bool RequiresAboMatch,
    bool RequiresRhMatch,
    string? Isbt128ProductCode,
    string? DefaultChargeCode,
    string? StorageRequirements,
    string? IssueRules,
    string? ReturnRules,
    string? ModificationRules,
    IReadOnlyList<ProductAttributeSelection>? Attributes,
    string? ChangeReason);

public sealed record ProductAttributeSelection(long AttributeId, bool IsRequired);

public sealed record ProductAttributeDto(long Id, string Code, string Name, string? Description, bool IsActive);

// ---- Expiration modification codes ----

public sealed record ExpirationModificationCodeDto(
    long Id,
    string Code,
    int OffsetAmount,
    ExpirationOffsetUnit OffsetUnit,
    ExpirationRelativeTo RelativeTo,
    string? Description,
    int Version,
    bool IsActive)
{
    public string DisplayLabel => FormatLabel(Code, OffsetAmount, OffsetUnit, RelativeTo);

    public static string FormatLabel(
        string code, int amount, ExpirationOffsetUnit unit, ExpirationRelativeTo relativeTo)
    {
        var unitWord = unit == ExpirationOffsetUnit.Hours
            ? (amount == 1 ? "hour" : "hours")
            : (amount == 1 ? "day" : "days");
        var from = relativeTo == ExpirationRelativeTo.ModificationDateTime ? "modification" : "collection";
        return $"{code} — {amount} {unitWord} from {from}";
    }
}

public sealed record SaveExpirationModificationCodeRequest(
    string Code,
    int OffsetAmount,
    ExpirationOffsetUnit OffsetUnit,
    ExpirationRelativeTo RelativeTo,
    string? Description,
    string? ChangeReason);

// ---- Modification rules ----

public sealed record ModificationRuleDto(
    long Id,
    string ModificationCode,
    long SourceProductTypeId,
    string SourceProductCode,
    ModificationType ModificationType,
    long TargetProductTypeId,
    string TargetProductCode,
    long ExpirationModificationCodeId,
    string ExpirationOffsetCode,
    ExpirationRelativeTo ExpirationRelativeTo,
    string? Description,
    int Version,
    bool IsActive);

public sealed record SaveModificationRuleRequest(
    string ModificationCode,
    long SourceProductTypeId,
    ModificationType ModificationType,
    long TargetProductTypeId,
    long ExpirationModificationCodeId,
    string? Description,
    string? ChangeReason);

// ---- ISBT product description codes ----

public sealed record IsbtProductCodeDto(
    long Id,
    string ProductDescriptionCode,
    string Description,
    string ComponentClass,
    string? Modifier,
    string? StorageRequirements,
    bool RequiresExtendedDivision,
    DateOnly? EffectiveDate,
    DateOnly? RetiredDate,
    string StandardVersion,
    bool IsPlaceholder,
    bool IsRetired);

// ---- HL7 endpoints ----

public sealed record Hl7EndpointDto(
    long Id,
    string Name,
    Hl7Direction Direction,
    InterfaceTransport Transport,
    string? Host,
    int? Port,
    string? Path,
    string MessageTypes,
    string? MappingProfile,
    bool IsEnabled,
    string? Environment,
    string? SendingApplication,
    string? SendingFacility,
    string? ReceivingApplication,
    string? ReceivingFacility,
    int? AckTimeoutSeconds,
    int? MaxRetryCount,
    int? RetryDelaySeconds,
    string? MessageLoggingLevel,
    bool ReplayAllowed,
    int Version);

public sealed record SaveHl7EndpointRequest(
    string Name,
    Hl7Direction Direction,
    InterfaceTransport Transport,
    string? Host,
    int? Port,
    string? Path,
    string MessageTypes,
    string? MappingProfile,
    string? Environment,
    string? SendingApplication,
    string? SendingFacility,
    string? ReceivingApplication,
    string? ReceivingFacility,
    int? AckTimeoutSeconds,
    int? MaxRetryCount,
    int? RetryDelaySeconds,
    string? MessageLoggingLevel,
    bool ReplayAllowed,
    string? ChangeReason);

// ---- Users & roles ----

public sealed record AdminUserDto(
    long Id,
    string UserName,
    string DisplayName,
    string? Email,
    bool IsActive,
    bool IsLocked,
    bool IsServiceAccount,
    DateTime? LastLoginUtc,
    IReadOnlyList<string> Roles);

public sealed record SaveUserRequest(
    string UserName,
    string DisplayName,
    string? Email,
    bool IsServiceAccount,
    IReadOnlyList<string>? Roles,
    string? ChangeReason);

public sealed record AssignRolesRequest(IReadOnlyList<string> Roles, string? ChangeReason);

public sealed record AdminRoleDto(long Id, string Name, string? Description, int SecurityLevel, IReadOnlyList<string> Permissions);

public sealed record SaveRoleRequest(string Name, string? Description, int SecurityLevel, IReadOnlyList<string> Permissions, string? ChangeReason);

public sealed record SetActiveRequest(bool Active, string? Reason);

public sealed record ReasonOnlyRequest(string? Reason);

public sealed record CloneRequest(string NewCode);

// ---- Change history ----

public sealed record ConfigHistoryDto(
    long Id,
    string EntityType,
    long? EntityId,
    int Version,
    ConfigChangeAction Action,
    string? OldValueJson,
    string? NewValueJson,
    string? ChangeReason,
    string ChangedBy,
    string? Workstation,
    DateTime ChangedUtc,
    string? Environment,
    bool IsDevMode);
