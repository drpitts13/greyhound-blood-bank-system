using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Immunohematology;

public sealed record CreateAntibodyIdWorkupRequest(
    long? SpecimenId,
    long PrimaryLotId,
    IReadOnlyList<long>? AdditionalLotIds = null);

public sealed record RecordAntibodyIdReactionRequest(long CellId, string PhaseCode, ReactionGrade Strength);

public sealed record RecordAntibodyIdDatRequest(AntibodyIdDatResult DatResult, string? DatMethod);

public sealed record AntibodyIdInterpretationItem(
    long? BloodAttributeDefinitionId,
    string Specificity,
    AntibodyIdClassification Classification,
    string? Rationale);

public sealed record RecordAntibodyIdInterpretationRequest(
    string Interpretation,
    IReadOnlyList<AntibodyIdInterpretationItem> Findings);

public sealed record ReviewAntibodyIdWorkupRequest(bool Accepted, string? Comment, string? WarningAcknowledgment = null);

public sealed record AntibodyIdCommentRequest(string? Comment);

public sealed record VoidAntibodyIdWorkupRequest(string Reason);

public sealed record AttachAntibodyIdLotsRequest(IReadOnlyList<long> LotIds);

public sealed record LinkAntibodyIdSpecimenRequest(long SpecimenId);

public sealed record CompleteAntibodyIdWorkupRequest(string? WarningAcknowledgment = null);

public sealed record AntibodyPanelLotListItemDto(
    long Id,
    long ManufacturerId,
    string ManufacturerName,
    string LotNumber,
    DateOnly ExpiresOn,
    string PanelName,
    bool IsSelectedCellLot,
    bool IsActive,
    bool IsExpired);

public sealed record AntibodyIdWorkupListItemDto(
    long Id,
    long PatientId,
    long? SpecimenId,
    string? SpecimenAccession,
    long PrimaryLotId,
    string LotNumber,
    string PanelName,
    AntibodyWorkupStatus Status,
    DateTime CreatedUtc,
    string CreatedBy,
    string? PatientMrn = null,
    string? PatientName = null);

public sealed record AntibodyIdCellDto(
    long CellId,
    string CellNumber,
    PanelCellRole Role,
    int SortOrder,
    bool IsSelected,
    IReadOnlyList<AntibodyIdCellAntigenDto> Antigens,
    IReadOnlyList<AntibodyIdReactionDto> Reactions);

public sealed record AntibodyIdCellAntigenDto(
    long BloodAttributeDefinitionId,
    string AntigenCode,
    string AntibodyName,
    AntigenExpression Expression);

public sealed record AntibodyIdReactionDto(string PhaseCode, ReactionGrade Strength);

public sealed record AntibodyIdFindingDto(
    long Id,
    long? BloodAttributeDefinitionId,
    string Specificity,
    AntibodyIdClassification Classification,
    AntibodyIdSource Source,
    string? Rationale,
    bool PostedToHistory);

public sealed record AntibodyIdWorkupDetailDto(
    long Id,
    long PatientId,
    long? SpecimenId,
    long? SourceResultId,
    AntibodyWorkupStatus Status,
    AntibodyIdDatResult DatResult,
    string? DatMethod,
    string? Comment,
    string? TechnologistInterpretation,
    string? TechnologistUser,
    DateTime? InterpretedUtc,
    string? SupervisorUser,
    DateTime? ReviewedUtc,
    string? SupervisorComment,
    bool SupervisorAccepted,
    DateTime? CompletedUtc,
    string? CompletedBy,
    string? VoidReason,
    IReadOnlyList<AntibodyPanelLotListItemDto> Lots,
    IReadOnlyList<AntibodyIdCellDto> Cells,
    IReadOnlyList<AntibodyIdFindingDto> Findings,
    IReadOnlyList<string> InterpretivePhases,
    bool AssistIsAdvisory);

public sealed record AntibodyIdAssistDto(
    IReadOnlyList<AntibodyIdFindingDto> Findings,
    IReadOnlyList<RuleResult> Warnings);
