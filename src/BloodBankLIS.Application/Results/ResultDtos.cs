using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Results;

public sealed record EnterResultRequest(
    long SpecimenId,
    string TestCode,
    string Value,
    long? OrderId = null,
    string? Units = null,
    string? Interpretation = null);

/// <param name="Subtests">Optional panel subtests (Anti-A, Anti-B, Anti-D, A-Cells, B-Cells; Control and Weak-D optional).</param>
public sealed record EnterAboRhRequest(
    long SpecimenId,
    AboGroup Abo,
    RhType RhD,
    IReadOnlyDictionary<string, string>? Subtests = null,
    long? OrderId = null);

public sealed record CorrectResultRequest(string NewValue, string Reason);

/// <summary>Optional override payload when verifying an ABO/Rh result that disagrees with history.</summary>
public sealed record VerifyResultRequest(
    string? OverrideReason = null,
    string? AuthorizedBy = null,
    AboRhHistoryResolution? HistoryResolution = null,
    long? SignatureId = null);

public sealed record SaveTestResultRequest(
    long SpecimenId,
    long OrderId,
    long OrderLineId,
    string TestCode,
    string? Value,
    string? Units,
    string? Interpretation,
    AboGroup? Abo,
    RhType? RhD,
    IReadOnlyDictionary<string, string>? Subtests,
    bool MarkComplete,
    string? CorrectionReason,
    string? UnitNumber,
    CrossmatchMethod? CrossmatchMethod,
    CrossmatchResult? CrossmatchResult,
    bool? AntibodyScreenNegative,
    string? OverrideReason = null,
    string? AuthorizedBy = null,
    AboRhHistoryResolution? HistoryResolution = null,
    long? SignatureId = null);

public sealed record TestResultDto(
    long Id,
    long SpecimenId,
    long PatientId,
    long? OrderId,
    string TestCode,
    int Version,
    long? SupersededByResultId,
    string? Value,
    string? Units,
    string? Interpretation,
    ResultStatus Status,
    string? EnteredBy,
    DateTime? EnteredUtc,
    string? VerifiedBy,
    DateTime? VerifiedUtc,
    string? CorrectionReason)
{
    public static TestResultDto From(TestResult r) => new(
        r.Id, r.SpecimenId, r.PatientId, r.OrderId, r.TestCode, r.Version, r.SupersededByResultId,
        r.Value, r.Units, r.Interpretation, r.Status, r.EnteredBy, r.EnteredUtc,
        r.VerifiedBy, r.VerifiedUtc, r.CorrectionReason);
}
