namespace BloodBankLIS.Web.Services;

/// <summary>
/// A stored UTC moment as a short local time that never hides its day (Cycle 618): the time alone for today, "yesterday"
/// or "tomorrow" with the time, and the date for anything further away. A bare "07:30" for a needed-by time tomorrow, or
/// a transfusion started yesterday, read as today.
/// </summary>
public static class ClockText
{
    public static string Short(DateTime utc, bool seconds = false) => Short(utc, DateTime.Now, seconds);

    public static string Short(DateTime utc, DateTime localNow, bool seconds = false)
    {
        var local = utc.Kind == DateTimeKind.Local ? utc : utc.ToLocalTime();
        var time = local.ToString(seconds ? "HH:mm:ss" : "HH:mm");
        var days = (local.Date - localNow.Date).Days;
        return days switch
        {
            0 => time,
            -1 => $"yesterday {time}",
            1 => $"tomorrow {time}",
            _ => local.ToString("yyyy-MM-dd HH:mm")
        };
    }
}
