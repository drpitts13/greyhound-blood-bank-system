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
    string? Comment = null);

public sealed record AllocateUnitRequest(
    long BloodUnitId,
    long PatientId,
    long? SpecimenId = null,
    DateTime? ExpiresUtc = null,
    bool AntigenNegOverrideAuthorized = false);

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
