using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.PatientWorkspace;

public sealed record PatientAllocationRowDto(
    long AllocationId,
    long BloodUnitId,
    string UnitNumber,
    string ProductCode,
    string ProductName,
    bool RequiresCrossmatch,
    ProductAllocationDisplayStatus DisplayStatus,
    AllocationStatus AllocationStatus,
    CrossmatchResult? CrossmatchResult,
    string? CrossmatchTestCode,
    long? OrderId,
    long? EncounterId,
    long? SpecimenId,
    DateTime AllocatedUtc,
    string AllocatedBy,
    DateTime? ExpiresUtc,
    IReadOnlyList<OrderActionDto>? PendingActions = null,
    UnitStatus UnitStatus = UnitStatus.Assigned,
    long? IssueId = null,
    IssueStatus? IssueStatus = null,
    bool PendingAutoThaw = false,
    string? AutoThawProductCode = null);

public sealed record CompatibleUnitDto(
    long BloodUnitId,
    string UnitNumber,
    long ProductTypeId,
    string ProductCode,
    string ProductName,
    bool RequiresCrossmatch,
    AboGroup Abo,
    RhType RhD,
    string BloodType,
    DateTime ExpiresUtc);

public sealed record CrossmatchTestOptionDto(
    string Code,
    string Name,
    ResultValueType ResultValueType);

public sealed record AllocatePatientUnitRequest(
    long BloodUnitId,
    long? EncounterId = null,
    long? SpecimenId = null,
    long? OrderingLocationId = null,
    DateTime? ExpiresUtc = null,
    string? CrossmatchTestCode = null,
    string? OverrideReason = null,
    string? AuthorizedBy = null,
    long? OrderId = null);

public sealed record AllocatableOrderDto(
    long OrderId,
    long PatientId,
    string MedicalRecordNumber,
    string PatientName,
    string OrderNumber,
    string OrderName,
    long ProductTypeId,
    long? SpecimenId,
    string? AccessionNumber,
    DateTime OrderedUtc);

public sealed record AllocatePatientUnitResultDto(
    PatientAllocationRowDto Allocation,
    long? CrossmatchOrderId,
    string? CrossmatchTestCode,
    bool AntibodyHistoryOverrideApplied,
    bool AutoThawEligible = false);
