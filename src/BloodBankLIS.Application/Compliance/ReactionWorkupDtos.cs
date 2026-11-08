using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Compliance;

/// <summary>One test of a workup Test Group on its specimen: the current result and whether it is verified.</summary>
public sealed record ReactionWorkupLineDto(
    long OrderLineId,
    string TestCode,
    string TestName,
    ResultStatus? Status,
    string? Value,
    bool Verified);

/// <summary>A workup specimen on a reaction investigation, with the order that carries its Test Group.</summary>
public sealed record ReactionWorkupSpecimenDto(
    string Role,
    long SpecimenId,
    string AccessionNumber,
    DateTime CollectedUtc,
    DateTime? ExpiresUtc,
    bool Expired,
    long OrderId,
    string OrderNumber,
    IReadOnlyList<ReactionWorkupLineDto> Lines,
    // Where the tube is stored (Store tubes), so it can be pulled for the workup.
    string? StorageLocation = null)
{
    public const string PreRole = "Pre-transfusion";
    public const string PostRole = "Post-transfusion";

    /// <summary>Tests not yet verified. A workup order with no test lines counts as one, so it cannot pass unseen.</summary>
    public int Pending => Lines.Count == 0 ? 1 : Lines.Count(l => !l.Verified);

    /// <summary>The verified value of a test on this specimen, or null.</summary>
    public string? VerifiedValue(string testCode) =>
        Lines.FirstOrDefault(l => l.Verified && string.Equals(l.TestCode, testCode, StringComparison.OrdinalIgnoreCase))?.Value;
}

/// <summary>A specimen offered for a workup role.</summary>
public sealed record ReactionWorkupSpecimenOptionDto(
    long SpecimenId,
    string AccessionNumber,
    string SpecimenType,
    DateTime CollectedUtc,
    DateTime? ExpiresUtc,
    SpecimenStatus Status,
    bool Expired,
    bool Suggested);

/// <summary>
/// What the investigator chooses from: specimens collected on or before the issue (pre-transfusion; the unit's
/// crossmatch specimen suggested) and specimens collected after the transfusion began (post-transfusion), with the
/// Test Group each role will order.
/// </summary>
public sealed record ReactionWorkupOptionsDto(
    DateTime IssuedUtc,
    DateTime TransfusionStartUtc,
    string? PreTransfusionTestGroupCode,
    string? PostTransfusionTestGroupCode,
    IReadOnlyList<ReactionWorkupSpecimenOptionDto> PreCandidates,
    IReadOnlyList<ReactionWorkupSpecimenOptionDto> PostCandidates);

public sealed record AttachWorkupSpecimenRequest(long SpecimenId);

/// <summary>A post-transfusion specimen drawn for the workup, accessioned from the investigation.</summary>
public sealed record AccessionPostTransfusionSpecimenRequest(
    string AccessionNumber,
    string SpecimenType,
    DateTime CollectedUtc,
    string? Barcode = null);
