namespace BloodBankLIS.Application.PatientWorkspace;

public sealed record ApplyAutoThawRequest(DateTime ThawUtc);

public sealed record AutoThawResultDto(
    long AllocationId,
    long BloodUnitId,
    string ProductCode,
    DateTime PreviousExpiresUtc,
    DateTime ExpiresUtc,
    bool PrintSucceeded = true,
    string? PrintError = null);
