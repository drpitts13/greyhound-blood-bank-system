namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Equipment register checks and when each scheduled check is due (Cycle 708). A check is due again its interval
/// after it last passed; a check never performed is due now; a check whose latest record failed stays failed until
/// it passes again. The intervals are the facility's; none is built in. Operational only.
/// </summary>
public static class EquipmentCheckRule
{
    public const string SaveCode = "EQUIP-SAVE";
    public const string CheckCode = "EQUIP-CHECK";
    public const string RecordCode = "EQUIP-RECORD";
    public const string ServiceCode = "EQUIP-SERVICE";

    /// <summary>The longest interval accepted (ten years), so a typing slip does not hide a check for centuries.</summary>
    public const int MaxIntervalDays = 3660;

    /// <summary>Kinds the page offers; any other kind may be typed.</summary>
    public static readonly IReadOnlyList<string> CommonKinds =
        ["Centrifuge", "Cell washer", "Heat block", "Water bath", "Irradiator", "Plasma thawer", "Platelet agitator", "Thermometer", "Timer", "Pipette", "Other"];

    public enum State
    {
        Current,
        DueToday,
        Overdue,
        NeverDone,
        Failed
    }

    public sealed record Status(State State, DateOnly? DueOn, int? DaysOverdue)
    {
        /// <summary>Needs doing now: failed, overdue, due today, or never done.</summary>
        public bool NeedsAction => State != EquipmentCheckRule.State.Current;
    }

    /// <param name="lastPassedUtc">The latest passing record, if any.</param>
    /// <param name="latestFailed">Whether the latest record of any result failed.</param>
    public static Status Evaluate(int intervalDays, DateTime? lastPassedUtc, bool latestFailed, DateTime nowUtc)
    {
        var today = DateOnly.FromDateTime(nowUtc);
        if (latestFailed)
        {
            return new Status(State.Failed, today, null);
        }

        if (lastPassedUtc is null)
        {
            return new Status(State.NeverDone, today, null);
        }

        var due = DateOnly.FromDateTime(lastPassedUtc.Value).AddDays(Math.Max(1, intervalDays));
        if (due > today)
        {
            return new Status(State.Current, due, null);
        }

        return due == today
            ? new Status(State.DueToday, due, null)
            : new Status(State.Overdue, due, today.DayNumber - due.DayNumber);
    }

    public static string Describe(Status status) => status.State switch
    {
        State.Failed => "Failed: record a pass once it is fixed",
        State.NeverDone => "Never done",
        State.DueToday => "Due today",
        State.Overdue => $"Overdue {status.DaysOverdue} day{(status.DaysOverdue == 1 ? "" : "s")} (due {status.DueOn:yyyy-MM-dd})",
        _ => $"Next due {status.DueOn:yyyy-MM-dd}"
    };

    public static IReadOnlyList<RuleResult> EvaluateSave(string? code, string? name, string? kind, bool codeInUse)
    {
        var stops = new List<RuleResult>();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(kind))
        {
            stops.Add(RuleResult.HardStop(SaveCode, "Equipment needs a code (asset tag), a name and a kind."));
        }
        else if (code.Trim().Length > 40)
        {
            stops.Add(RuleResult.HardStop(SaveCode, "The equipment code is at most 40 characters."));
        }

        if (codeInUse)
        {
            stops.Add(RuleResult.HardStop(SaveCode, $"Equipment {code?.Trim().ToUpperInvariant()} is already registered."));
        }

        return stops;
    }

    public static RuleResult EvaluateCheck(string? name, int intervalDays) =>
        string.IsNullOrWhiteSpace(name)
            ? RuleResult.HardStop(CheckCode, "Name the check (for example \"Daily QC\" or \"Annual calibration\").")
            : intervalDays < 1 || intervalDays > MaxIntervalDays
                ? RuleResult.HardStop(CheckCode, $"Enter how often the check is due, from 1 to {MaxIntervalDays} days, as the facility's SOP or the manufacturer states.")
                : RuleResult.Pass(CheckCode);

    /// <summary>A failed check needs a note saying what was found or done; inactive equipment or checks take no records.</summary>
    public static RuleResult EvaluateRecord(bool equipmentActive, bool checkActive, bool passed, string? note) =>
        !equipmentActive || !checkActive
            ? RuleResult.HardStop(RecordCode, "This equipment or check is no longer active.")
            : !passed && string.IsNullOrWhiteSpace(note)
                ? RuleResult.HardStop(RecordCode, "Say what failed and what was done (for example, taken out of service, service called).")
                : RuleResult.Pass(RecordCode);

    public static RuleResult EvaluateService(string? reason, bool takingOut, bool alreadyThatWay) =>
        alreadyThatWay
            ? RuleResult.HardStop(ServiceCode, takingOut ? "This equipment is already out of service." : "This equipment is already in service.")
            : string.IsNullOrWhiteSpace(reason)
                ? RuleResult.HardStop(ServiceCode, takingOut
                    ? "Give the reason it is out of service (for example, failed QC, broken lid latch)."
                    : "Say why it can be used again (for example, repaired and QC passed, vendor report).")
                : RuleResult.Pass(ServiceCode);
}
