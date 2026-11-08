namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Discarding a stored tube. A tube still on an unfinished order or on an open reaction investigation is kept (hard stop,
/// no reason releases it). The facility sets how many days a tube is kept after its expiration (Specimen retention days,
/// OCD-070); before then, or when no period is set, a discard needs a reason and is recorded as early.
/// </summary>
public static class SpecimenDiscardRule
{
    public const string Code = "SPECIMEN-DISCARD";
    public const int MaxReasonLength = 500;

    /// <summary>When the tube may be discarded: its expiration (or collection when it has none) plus the retention days; null when no period is set.</summary>
    public static DateTime? KeepUntil(DateTime collectedUtc, DateTime? expiresUtc, int retentionDays) =>
        retentionDays <= 0 ? null : (expiresUtc ?? collectedUtc).AddDays(retentionDays);

    public static bool Due(DateTime? keepUntilUtc, DateTime nowUtc) => keepUntilUtc is DateTime k && k <= nowUtc;

    public static RuleResult Evaluate(
        DateTime? discardedUtc,
        IReadOnlyCollection<string> openOrders,
        bool onOpenInvestigation,
        DateTime? keepUntilUtc,
        DateTime nowUtc,
        string? reason)
    {
        if (discardedUtc is DateTime discarded)
        {
            return RuleResult.HardStop(Code, $"The tube was already discarded on {discarded:yyyy-MM-dd HH:mm} UTC.");
        }

        if (onOpenInvestigation)
        {
            return RuleResult.HardStop(Code, "The tube is on a transfusion reaction investigation that is still open; keep it until the investigation is closed.");
        }

        if (openOrders.Count > 0)
        {
            return RuleResult.HardStop(Code,
                $"{(openOrders.Count == 1 ? "Order" : "Orders")} {string.Join(", ", openOrders)} on this tube {(openOrders.Count == 1 ? "is" : "are")} not finished; finish or cancel {(openOrders.Count == 1 ? "it" : "them")} before discarding the tube.");
        }

        var why = reason?.Trim() ?? string.Empty;
        if (why.Length > MaxReasonLength)
        {
            return RuleResult.HardStop(Code, $"A reason is at most {MaxReasonLength} characters.");
        }

        if (Due(keepUntilUtc, nowUtc))
        {
            return RuleResult.Pass(Code);
        }

        if (why.Length == 0)
        {
            return RuleResult.HardStop(Code, keepUntilUtc is DateTime keep
                ? $"The tube is kept until {keep:yyyy-MM-dd HH:mm} UTC; give a reason to discard it early."
                : "No retention period is set (Facility policy: Specimen retention days); give a reason to discard the tube.");
        }

        return RuleResult.Warning(Code, keepUntilUtc is DateTime until
            ? $"Discarded before {until:yyyy-MM-dd HH:mm} UTC: {why}"
            : $"Discarded with no retention period set: {why}");
    }
}
