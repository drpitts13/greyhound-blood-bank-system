namespace BloodBankLIS.Web.Services;

/// <summary>The Audit Trail opened on one record (the entity type and id the audit rows store).</summary>
public static class AuditUrls
{
    public static string For(string entityType, long entityId) =>
        $"/audit?entityType={Uri.EscapeDataString(entityType)}&entityId={entityId}";
}
