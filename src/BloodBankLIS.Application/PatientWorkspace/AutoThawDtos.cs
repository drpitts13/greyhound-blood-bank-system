namespace BloodBankLIS.Application.PatientWorkspace;

/// <summary>OpenSystem: the person thawing says the unit was entered (opened) during or after the thaw (OCD-068).</summary>
public sealed record ApplyAutoThawRequest(DateTime ThawUtc, bool OpenSystem = false);

public sealed record AutoThawResultDto(
    long AllocationId,
    long BloodUnitId,
    string ProductCode,
    DateTime PreviousExpiresUtc,
    DateTime ExpiresUtc,
    bool PrintSucceeded = true,
    string? PrintError = null);
