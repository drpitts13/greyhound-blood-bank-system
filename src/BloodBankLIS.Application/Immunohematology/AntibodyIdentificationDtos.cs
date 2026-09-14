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
    bool IsExpired,
    int OpenWorkupCount = 0,
    int CompletedWorkupCount = 0,
    int PostedHistoryWorkupCount = 0);

public sealed record AntibodyPanelManufacturerListItemDto(
    long Id,
    string Code,
    string Name,
    bool IsActive,
    int OpenWorkupCount = 0,
    int CompletedWorkupCount = 0,
    int PostedHistoryWorkupCount = 0);

public sealed record CreateAntibodyPanelManufacturerRequest(string Code, string Name);

public sealed record CreateAntibodyPanelLotAntigenRequest(
    long BloodAttributeDefinitionId,
    AntigenExpression Expression);

public sealed record CreateAntibodyPanelLotCellRequest(
    string CellNumber,
    PanelCellRole Role,
    int SortOrder,
    IReadOnlyList<CreateAntibodyPanelLotAntigenRequest> Antigens);

public sealed record CreateAntibodyPanelLotRequest(
    long ManufacturerId,
    string LotNumber,
    string PanelName,
    DateOnly ExpiresOn,
    bool IsSelectedCellLot,
    IReadOnlyList<CreateAntibodyPanelLotCellRequest> Cells);

public sealed record AntibodyPanelLotAntigenDetailDto(
    long BloodAttributeDefinitionId,
    string AntigenCode,
    string AntibodyName,
    AntigenExpression Expression);

public sealed record AntibodyPanelLotCellDetailDto(
    long CellId,
    string CellNumber,
    PanelCellRole Role,
    int SortOrder,
    IReadOnlyList<AntibodyPanelLotAntigenDetailDto> Antigens);

public sealed record AntibodyPanelLotOpenWorkupDto(
    long WorkupId,
    long PatientId,
    string? PatientMrn,
    string? PatientName,
    AntibodyWorkupStatus Status,
    AntibodyIdWorklistNextAction NextAction,
    string? LotNumber = null,
    int PostedHistoryCount = 0);

public sealed record AntibodyPanelManufacturerDetailDto(
    AntibodyPanelManufacturerListItemDto Manufacturer,
    IReadOnlyList<AntibodyPanelLotOpenWorkupDto> OpenWorkups,
    IReadOnlyList<AntibodyPanelLotOpenWorkupDto> CompletedWorkups);

public sealed record AntibodyPanelLotDetailDto(
    AntibodyPanelLotListItemDto Lot,
    IReadOnlyList<AntibodyPanelLotCellDetailDto> Cells,
    IReadOnlyList<AntibodyPanelLotOpenWorkupDto> OpenWorkups,
    IReadOnlyList<AntibodyPanelLotOpenWorkupDto> CompletedWorkups);

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
    string? PatientName = null,
    bool HasInactiveLot = false,
    bool HasExpiredLot = false,
    AntibodyIdWorklistNextAction NextAction = AntibodyIdWorklistNextAction.RecordReactions,
    IReadOnlyList<string>? AttachedLotNumbers = null,
    IReadOnlyList<string>? InactiveLotNumbers = null,
    IReadOnlyList<string>? ExpiredLotNumbers = null,
    string? ManufacturerName = null,
    IReadOnlyList<string>? AttachedManufacturers = null,
    bool HasUnusableSpecimen = false,
    bool HasExpiredSpecimen = false,
    bool HasUnacceptedSpecimen = false,
    bool HasNotReadySpecimen = false,
    bool HasWithdrawnJudgment = false,
    bool HasPendingTypeCorrection = false,
    bool HasReservedOrIssuedUnits = false);

public sealed record AntibodyIdOpenWorklistSummaryDto(
    int OpenCount,
    int RecordReactionsCount,
    int InterpretCount,
    int ReviewCount,
    int InactiveLotCount,
    int ExpiredLotCount,
    IReadOnlyList<string>? InactiveLotNumbers = null,
    IReadOnlyList<string>? ExpiredLotNumbers = null,
    int UnusableSpecimenCount = 0,
    int ExpiredSpecimenCount = 0,
    int UnacceptedSpecimenCount = 0,
    int NotReadySpecimenCount = 0,
    int WithdrawnJudgmentCount = 0,
    int PendingTypeCorrectionCount = 0,
    int ReservedOrIssuedUnitCount = 0);

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
    bool AssistIsAdvisory,
    bool HasReservedOrIssuedUnits = false,
    string? JudgmentWithdrawnReason = null);

public sealed record AntibodyIdAssistDto(
    IReadOnlyList<AntibodyIdFindingDto> Findings,
    IReadOnlyList<RuleResult> Warnings);
