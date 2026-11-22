namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Discarding a stored tube. A tube still on an unfinished order or on an open reaction investigation is kept (hard stop,
/// no reason releases it). The facility sets how many days a tube is kept after its expiration (Tube storage days after expiration,
/// OCD-070); before then, or when no period is set, a discard needs a reason and is recorded as early.
/// </summary>
public static class SpecimenDiscardRule
{
    public const string Code = "SPECIMEN-DISCARD";
    public const int MaxReasonLength = 500;

    /// <summary>Who discarded a tube the system discarded when its storage days ended (Facility policy: automatic discard).</summary>
    public const string AutomaticBy = "system (automatic discard)";

    public const string AutomaticNote = "Automatic: storage days after expiration ended";

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
        string? reason,
        bool onOpenAntibodyWorkup = false,
        bool onOpenDatWorkup = false)
    {
        if (discardedUtc is DateTime discarded)
        {
            return RuleResult.HardStop(Code, $"The tube was already discarded on {ClinicalTimeText.Local(discarded)}.");
        }

        // Pass 336: the tube an antibody identification is being worked on is kept until the workup is completed or voided.
        if (onOpenAntibodyWorkup)
        {
            return RuleResult.HardStop(Code, "The tube is the specimen of an antibody identification that is still open; keep it until the workup is completed or voided.");
        }

        // Pass 337: and the tube of a DAT/elution workup still open (the eluate may need to be repeated or tested further).
        if (onOpenDatWorkup)
        {
            return RuleResult.HardStop(Code, "The tube is the specimen of a DAT/elution workup that is still open; keep it until the workup is completed or voided.");
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
                ? $"The tube is kept until {ClinicalTimeText.Local(keep)}; give a reason to discard it early."
                : "No retention period is set (Facility policy: Tube storage days after expiration); give a reason to discard the tube.");
        }

        return RuleResult.Warning(Code, keepUntilUtc is DateTime until
            ? $"Discarded before {ClinicalTimeText.Local(until)}: {why}"
            : $"Discarded with no retention period set: {why}");
    }
}
