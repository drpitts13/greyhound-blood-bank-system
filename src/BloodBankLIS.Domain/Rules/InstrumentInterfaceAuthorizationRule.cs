namespace BloodBankLIS.Domain.Rules;

/// <summary>Privilege gates for instrument interface configuration mutations.</summary>
public static class InstrumentInterfaceAuthorizationRule
{
    public const string CreateCode = "INST-CREATE-PERM";
    public const string UpdateCode = "INST-UPD-PERM";
    public const string EnableCode = "INST-ENABLE-PERM";
    public const string DisableCode = "INST-DISABLE-PERM";

    public static RuleResult EvaluateCreate(bool hasManage) =>
        hasManage
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(CreateCode, "Creating an instrument interface requires the admin.instruments.manage permission.");

    public static RuleResult EvaluateUpdate(bool hasManage) =>
        hasManage
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(UpdateCode, "Updating an instrument interface requires the admin.instruments.manage permission.");

    public static RuleResult EvaluateEnable(bool hasManage) =>
        hasManage
            ? RuleResult.Pass(EnableCode)
            : RuleResult.HardStop(EnableCode, "Enabling an instrument interface requires the admin.instruments.manage permission.");

    public static RuleResult EvaluateDisable(bool hasManage) =>
        hasManage
            ? RuleResult.Pass(DisableCode)
            : RuleResult.HardStop(DisableCode, "Disabling an instrument interface requires the admin.instruments.manage permission.");
}
