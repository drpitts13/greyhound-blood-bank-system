namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for immunohematology writes that change current type,
/// antibody history, or antigen profiles used at compatibility/issue.
/// </summary>
public static class ImmunoAuthorizationRule
{
    public const string ManualBloodTypeCode = "IH-ABO-PERM";
    public const string AntibodyAddCode = "IH-AB-ADD-PERM";
    public const string AntibodyDeactivateCode = "IH-AB-DEACT-PERM";
    public const string AntigenProfileCode = "IH-AG-PERM";

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

    private static RuleResult Require(bool granted, string code, string message) =>
        granted ? RuleResult.Pass(code) : RuleResult.HardStop(code, message);
}
