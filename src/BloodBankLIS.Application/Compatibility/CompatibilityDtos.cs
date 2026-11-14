using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Compatibility;

public sealed record RecordCrossmatchRequest(
    long BloodUnitId,
    long PatientId,
    long SpecimenId,
    CrossmatchMethod Method,
    CrossmatchResult Result = CrossmatchResult.Compatible,
    bool AntibodyScreenNegative = true,
    string? Comment = null,
    string? OverrideReason = null,
    string? AuthorizedBy = null);

public sealed record AllocateUnitRequest(
    long BloodUnitId,
    long PatientId,
    long? SpecimenId = null,
    DateTime? ExpiresUtc = null,
    string? OverrideReason = null,
    string? AuthorizedBy = null);

public sealed record EvaluateCompatibilityRequest(long BloodUnitId, long PatientId);

public sealed record CrossmatchDto(
    long Id,
    long BloodUnitId,
    long PatientId,
    long SpecimenId,
    CrossmatchMethod Method,
    CrossmatchResult Result,
    DateTime PerformedUtc,
    string PerformedBy,
    DateTime? ExpiresUtc,
    string? Comment)
{
    public static CrossmatchDto From(Crossmatch x) => new(
        x.Id, x.BloodProductId, x.PatientId, x.SpecimenId, x.Method, x.Result,
        x.PerformedUtc, x.PerformedBy, x.ExpiresUtc, x.Comment);
}

public sealed record AllocationDto(
    long Id,
    long BloodUnitId,
    long PatientId,
    long? SpecimenId,
    AllocationStatus Status,
    DateTime AllocatedUtc,
    string AllocatedBy,
    DateTime? ExpiresUtc,
    string? ReleaseReason)
{
    public static AllocationDto From(Allocation a) => new(
        a.Id, a.BloodProductId, a.PatientId, a.SpecimenId, a.Status,
        a.AllocatedUtc, a.AllocatedBy, a.ExpiresUtc, a.ReleaseReason);
}

/// <summary>Several reservations released to stock with one reason (Ready to issue, Release to stock).</summary>
public sealed record ReleaseAllocationsRequest(IReadOnlyList<long> AllocationIds, string Reason);

/// <summary>One reservation in a batch release: released, or why not.</summary>
public sealed record ReleaseAllocationOutcomeDto(long AllocationId, bool Released, string? Error);
