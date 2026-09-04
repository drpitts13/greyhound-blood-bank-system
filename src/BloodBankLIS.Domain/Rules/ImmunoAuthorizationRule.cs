namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for immunohematology writes that change current type,
/// antibody history, antigen profiles, or special requirements used at issue.
/// </summary>
public static class ImmunoAuthorizationRule
{
    public const string ManualBloodTypeCode = "IH-ABO-PERM";
    public const string AntibodyAddCode = "IH-AB-ADD-PERM";
    public const string AntibodyDeactivateCode = "IH-AB-DEACT-PERM";
    public const string AntigenProfileCode = "IH-AG-PERM";
    public const string SpecialRequirementAddCode = "SR-ADD-PERM";
    public const string SpecialRequirementDeactivateCode = "SR-DEACT-PERM";

    public static RuleResult EvaluateManualBloodType(bool hasImmunoOverride) =>
        Require(hasImmunoOverride, ManualBloodTypeCode,
            "Manually recording ABO/Rh requires the immuno.override permission.");

    public static RuleResult EvaluateAntibodyAdd(bool hasImmunoRecord) =>
        Require(hasImmunoRecord, AntibodyAddCode,
            "Recording an antibody requires the immuno.record permission.");

    public static RuleResult EvaluateAntibodyDeactivate(bool hasImmunoOverride) =>
        Require(hasImmunoOverride, AntibodyDeactivateCode,
            "Deactivating an antibody requires the immuno.override permission.");

    public static RuleResult EvaluateAntigenProfile(bool hasImmunoRecord) =>
        Require(hasImmunoRecord, AntigenProfileCode,
            "Recording an antigen profile requires the immuno.record permission.");

    public static RuleResult EvaluateSpecialRequirementAdd(bool hasImmunoRecord) =>
        Require(hasImmunoRecord, SpecialRequirementAddCode,
            "Adding a special transfusion requirement requires the immuno.record permission.");

    public static RuleResult EvaluateSpecialRequirementDeactivate(bool hasImmunoOverride) =>
        Require(hasImmunoOverride, SpecialRequirementDeactivateCode,
            "Deactivating a special transfusion requirement requires the immuno.override permission.");

    private static RuleResult Require(bool granted, string code, string message) =>
        granted ? RuleResult.Pass(code) : RuleResult.HardStop(code, message);
}
