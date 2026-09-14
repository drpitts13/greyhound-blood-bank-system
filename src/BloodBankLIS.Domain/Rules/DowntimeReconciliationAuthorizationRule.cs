namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Read-only downtime reconciliation snapshot (gap 16). Does not invent a
/// paper SOP or purge records (OCD-007).
/// </summary>
public static class DowntimeReconciliationAuthorizationRule
{
    public const string PermissionCode = "DT-RECON-PERM";

    public static RuleResult Evaluate(bool hasAuditRead) =>
        hasAuditRead
            ? RuleResult.Pass(PermissionCode)
            : RuleResult.HardStop(PermissionCode, "Viewing the downtime reconciliation snapshot requires audit.read.");
}
