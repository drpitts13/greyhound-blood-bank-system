using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

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
    string? AutoThawProductCode = null,
    bool OrderSpecimenLinked = false,
    bool UnitOutdated = false,
    DateTime? UnitExpiresUtc = null,
    string? UnitBloodType = null,
    string? StorageLocationName = null,
    DateTime? CrossmatchExpiresUtc = null,
    // What an unissued reserved unit lacks for the patient's special requirements; null when it meets them.
    string? SpecialRequirementNotice = null,
    // The ward's open request to issue this unit, and where to.
    DateTime? IssueRequestedUtc = null,
    string? IssueRequestedTo = null,
    // The thawed shelf life of the auto-thaw code, when the unit can be thawed in place.
    int? AutoThawShelfLifeHours = null,
    // Cycle 685: the code's open-system and pooled shelf lives (null when not set) and whether the unit is a pool, so the
    // thaw prompt states the time that will apply and refuses before the thaw rather than after.
    int? AutoThawOpenSystemShelfLifeHours = null,
    int? AutoThawPooledShelfLifeHours = null,
    bool AutoThawPooled = false);

/// <summary>Cycle 685: what the thaw prompt says before Yes, for the open-system box as it stands.</summary>
public static class AutoThawPreview
{
    public static (int? Hours, string? Refusal) For(PatientAllocationRowDto row, bool openSystem)
    {
        var code = string.IsNullOrWhiteSpace(row.AutoThawProductCode) ? row.ProductCode : row.AutoThawProductCode;
        var (hours, result) = ThawedExpirationRule.ShelfLifeFor(
            code, row.AutoThawShelfLifeHours ?? ThawedExpirationRule.DefaultHours, row.AutoThawOpenSystemShelfLifeHours,
            row.AutoThawPooledShelfLifeHours, openSystem, row.AutoThawPooled);
        return (hours, hours is null ? result.Message : null);
    }
}

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
    DateTime ExpiresUtc,
    string? AntigenNegFlag = null,
    string? AntigenNegDetail = null,
    DonationRestriction DonationRestriction = DonationRestriction.Allogeneic,
    string? SelectionCue = null,
    bool ExpiresSoon = false,
    bool CrossmatchCompatible = false,
    bool CrossmatchIncompatible = false,
    string? StorageLocationName = null,
    DateTime? CrossmatchExpiresUtc = null,
    CrossmatchResult? LatestCrossmatchResult = null);

public sealed record EnsureManualCrossmatchLineRequest(string TestCode);

public sealed record ManualCrossmatchLineDto(
    long OrderId,
    long OrderLineId,
    long SpecimenId,
    string TestCode);

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
