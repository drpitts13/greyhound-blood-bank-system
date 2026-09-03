namespace BloodBankLIS.Domain.Isbt128;

/// <summary>
/// Machine-readable error codes for ISBT 128 parsing, identity, and component workflows.
/// Messages for end users must be human-readable; never expose stack traces.
/// </summary>
public static class IsbtErrorCodes
{
    public const string UnsupportedDataStructure = "ISBT_UNSUPPORTED_DATA_STRUCTURE";
    public const string InvalidDinLength = "ISBT_INVALID_DIN_LENGTH";
    public const string InvalidDinCharacter = "ISBT_INVALID_DIN_CHARACTER";
    public const string DinCheckMismatch = "ISBT_DIN_CHECK_MISMATCH";
    public const string InvalidFlagLength = "ISBT_INVALID_FLAG_LENGTH";
    public const string UnknownAboRhdCode = "ISBT_UNKNOWN_ABO_RHD_CODE";
    public const string UnknownProductCode = "ISBT_UNKNOWN_PRODUCT_CODE";
    public const string RetiredProductNotAllowed = "ISBT_RETIRED_PRODUCT_NOT_ALLOWED";
    public const string ExtendedDivisionRequired = "ISBT_EXTENDED_DIVISION_REQUIRED";
    public const string InvalidExpiration = "ISBT_INVALID_EXPIRATION";
    public const string ConflictingScan = "ISBT_CONFLICTING_SCAN";
    public const string DuplicateScanAccepted = "ISBT_DUPLICATE_SCAN_ACCEPTED";
    public const string IncompleteScanSession = "ISBT_INCOMPLETE_SCAN_SESSION";
    public const string MixedUnitSession = "ISBT_MIXED_UNIT_SESSION";

    public const string ComponentDuplicate = "COMPONENT_DUPLICATE";
    public const string ComponentExpired = "COMPONENT_EXPIRED";
    public const string ComponentQuarantined = "COMPONENT_QUARANTINED";
    public const string ComponentOnHold = "COMPONENT_ON_HOLD";
    public const string ComponentMissing = "COMPONENT_MISSING";
    public const string ComponentDamaged = "COMPONENT_DAMAGED";
    public const string ComponentRecalled = "COMPONENT_RECALLED";
    public const string ComponentAlreadyIssued = "COMPONENT_ALREADY_ISSUED";
    public const string ComponentAlreadyTransfused = "COMPONENT_ALREADY_TRANSFUSED";
    public const string ComponentIdentityLocked = "COMPONENT_IDENTITY_LOCKED";

    public const string PatientMismatch = "PATIENT_MISMATCH";
    public const string OrderInactive = "ORDER_INACTIVE";
    public const string CompatibilityHardStop = "COMPATIBILITY_HARD_STOP";
    public const string CrossmatchRequired = "CROSSMATCH_REQUIRED";
    public const string CrossmatchExpired = "CROSSMATCH_EXPIRED";
    public const string SpecimenExpired = "SPECIMEN_EXPIRED";
    public const string EmergencyReleaseAuthorizationRequired = "EMERGENCY_RELEASE_AUTHORIZATION_REQUIRED";
    public const string InvalidStatusTransition = "INVALID_STATUS_TRANSITION";
    public const string UnauthorizedOverride = "UNAUTHORIZED_OVERRIDE";
    public const string UnitScanMismatch = "UNIT_SCAN_MISMATCH";
}
