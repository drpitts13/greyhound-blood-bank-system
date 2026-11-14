using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// OCD-054 (decided 2026-11-04): back-entry of an issue made on paper during downtime is allowed only for a role that
/// grants <c>issue.downtime-backentry</c>. A back-entry is a late entry (the paper record's earlier issue time, with a
/// reason) whose time-sensitive checks are judged as of that time: a unit or specimen that was in date when the blood
/// left the bank, and has expired since, does not block it. Everything else in the gate runs unchanged. The bag is
/// usually gone, so the fresh unit scan is replaced by the DIN typed from the paper record, which must match the unit.
/// </summary>
public static class DowntimeBackEntryRule
{
    public const string PermissionCode = "ISS-DOWNTIME-PERM";
    public const string TimeCode = "ISS-DOWNTIME-TIME";
    public const string DinCode = "ISS-DOWNTIME-DIN";

    public static RuleResult Evaluate(bool requested, bool hasPermission, DateTime statedUtc, DateTime nowUtc)
    {
        if (!requested)
        {
            return RuleResult.Pass(PermissionCode);
        }

        if (!hasPermission)
        {
            return RuleResult.HardStop(PermissionCode,
                "Downtime back-entry requires the issue.downtime-backentry permission. Ask someone whose role allows it.");
        }

        return IssueTimeRule.IsLateEntry(statedUtc, nowUtc)
            ? RuleResult.Pass(TimeCode)
            : RuleResult.HardStop(TimeCode,
                "Downtime back-entry needs the issue time from the paper record, which must be earlier than now.");
    }

    /// <summary>The time the gate judges expiration and validity against.</summary>
    public static DateTime GateTime(bool backEntry, DateTime statedUtc, DateTime nowUtc) =>
        backEntry ? statedUtc : nowUtc;

    /// <summary>
    /// A unit the expiry sweep marked Expired after it was issued on paper is judged by the status it had at the
    /// stated time: still in date then means it was issuable. Any other status is left alone.
    /// </summary>
    public static UnitStatus StatusForGate(bool backEntry, UnitStatus status, DateTime? unitExpiresUtc, DateTime statedUtc) =>
        backEntry && status == UnitStatus.Expired && unitExpiresUtc is { } expires && expires > statedUtc
            ? UnitStatus.Available
            : status;

    /// <summary>A reservation the expiry sweep ended counts if it was made before the stated issue time.</summary>
    public static bool ReservationCounts(bool backEntry, AllocationStatus status, DateTime allocatedUtc, DateTime statedUtc) =>
        status == AllocationStatus.Reserved
        || (backEntry && status == AllocationStatus.Expired && allocatedUtc <= statedUtc);

    /// <summary>The DIN typed from the paper record must be the unit's, ignoring spaces and case.</summary>
    public static RuleResult ConfirmDin(string? typedDin, string unitNumber, string? din)
    {
        var typed = Normalize(typedDin);
        if (typed.Length == 0)
        {
            return RuleResult.HardStop(DinCode, "Type the unit number (DIN) from the paper issue record.");
        }

        return typed == Normalize(unitNumber) || (din is not null && typed == Normalize(din))
            ? RuleResult.Pass(DinCode)
            : RuleResult.HardStop(DinCode, $"The unit number typed from the paper record does not match unit {unitNumber}.");
    }

    public const string NotBackEntryCode = "ISS-DOWNTIME-NOT-BACKENTRY";

    /// <summary>How a unit identified from the paper record, not a scan, is recorded on ward receipt and transfusion.</summary>
    public const string PaperDinMethod = "PaperRecordDin";

    /// <summary>
    /// Ward receipt and bedside documentation of an issue that was itself back-entered may identify the unit by the DIN
    /// on the paper record instead of a fresh scan, by a role that allows back-entry. Any other issue must be scanned.
    /// </summary>
    public static RuleResult EvaluateFollowOn(bool issueIsBackEntry, bool hasPermission, string? typedDin, string unitNumber, string? din)
    {
        if (!issueIsBackEntry)
        {
            return RuleResult.HardStop(NotBackEntryCode,
                "Only an issue entered as a downtime back-entry can be identified from the paper record. Scan the unit.");
        }

        return hasPermission
            ? ConfirmDin(typedDin, unitNumber, din)
            : RuleResult.HardStop(PermissionCode,
                "Identifying the unit from the paper record requires the issue.downtime-backentry permission.");
    }

    public const string ReceiptTimeCode = "ISS-DOWNTIME-RECEIPT-TIME";

    /// <summary>
    /// A ward receipt back-entered from the paper record carries the receipt time written on it: not before the issue,
    /// and not in the future.
    /// </summary>
    public static RuleResult EvaluateReceiptTime(DateTime? receivedUtc, DateTime issuedUtc, DateTime nowUtc)
    {
        if (receivedUtc is not { } received)
        {
            return RuleResult.HardStop(ReceiptTimeCode, "Enter the receipt time from the paper record.");
        }

        if (received < issuedUtc)
        {
            return RuleResult.HardStop(ReceiptTimeCode,
                $"The receipt time is before the unit was issued at {issuedUtc:yyyy-MM-dd HH:mm} UTC. Check the paper record.");
        }

        return received > nowUtc + IssueTimeRule.ClockAllowance
            ? RuleResult.HardStop(ReceiptTimeCode, "The receipt time is in the future. Check the paper record.")
            : RuleResult.Pass(ReceiptTimeCode);
    }

    private static string Normalize(string? value) =>
        new string((value ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
}
