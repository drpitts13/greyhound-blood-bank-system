using BloodBankLIS.Domain.Enums;

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
    int SortOrder = 0);

public sealed record InterpretationLogicRowDto(
    string InterpretationKey,
    string Label,
    IReadOnlyDictionary<string, ReactionPolarity> SubtestExpectations);

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
