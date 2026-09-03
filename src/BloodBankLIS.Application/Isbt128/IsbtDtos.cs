using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Isbt128.Validation;

namespace BloodBankLIS.Application.Isbt128;

public sealed record ParseIsbtInputRequest(string Value);

public sealed record ParseIsbtInputResponse(
    IsbtInputMode Mode,
    IsbtDataStructureKind StructureKind,
    string Original,
    string Sanitized,
    object? Parsed,
    IReadOnlyList<string> ErrorCodes,
    IReadOnlyList<string> ErrorMessages);

public sealed record StartScanSessionRequest(IReadOnlyList<IsbtDataStructureKind>? ExpectedStructures = null);

public sealed record ScanSessionDto(
    Guid SessionKey,
    DateTime StartedAt,
    DateTime LastScanAt,
    bool IsCompleted,
    IReadOnlyList<IsbtDataStructureKind> Expected,
    IReadOnlyList<IsbtDataStructureKind> Received,
    CanonicalComponentSummary? Draft,
    ValidationResult? Validation);

public sealed record AddScanRequest(Guid SessionKey, string Value);

public sealed record CompleteScanSessionRequest(
    Guid SessionKey,
    long ProductTypeId,
    long? LocationId = null,
    string? Supplier = null,
    string? ShipmentId = null,
    string? CollectionFacility = null,
    bool ReleaseToAvailable = false,
    decimal? Volume = null,
    bool VisualInspectionAcceptable = true,
    string? VisualInspectionNotes = null);

/// <summary>
/// Manual receipt. <paramref name="DonationNumber"/> is the combined human-readable
/// unit/DIN string (e.g. <c>G123417654321</c>, <c>G1234 17 654321</c>, or with keyboard check).
/// </summary>
public sealed record ManualComponentEntryRequest(
    string DonationNumber,
    string AboRhdCode,
    string ProductDescriptionCode,
    string CollectionTypeCode,
    string DivisionCode,
    string? ExtendedDivisionCode,
    DateTime ExpirationLocal,
    bool ExpirationHasExplicitTime,
    long ProductTypeId,
    long? LocationId = null,
    string? Supplier = null,
    string? ShipmentId = null,
    string? CollectionFacility = null,
    bool ReleaseToAvailable = false,
    bool AllowDinCheckException = false,
    string? DinCheckExceptionReason = null,
    decimal? Volume = null,
    bool VisualInspectionAcceptable = true,
    string? VisualInspectionNotes = null);

public sealed record CanonicalComponentSummary(
    string? ComponentIdentity,
    string? Din,
    string? DinFlags,
    string? AboRhdCode,
    string? Abo,
    string? RhD,
    string? ProductCodeData,
    string? ProductDescription,
    string? ExpirationEncoded,
    DateTime? ExpirationLocal,
    bool? ExpirationHasExplicitTime,
    bool HasRequiredQuadrants);

public sealed record CorrectIdentityRequest(
    long BloodProductId,
    string Field,
    string CorrectedValue,
    string Reason,
    string? ApproverId = null,
    string? SupportingEvidence = null);

public sealed record ComponentScanVerification(
    string Din,
    string ProductCodeData,
    string? ExtendedDivisionCode,
    string AboRhdCode,
    string ExpirationEncoded);
