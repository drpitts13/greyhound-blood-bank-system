namespace BloodBankLIS.Web.Services;

/// <summary>
/// How long something has been active, read at a glance (end-user testing pass 195: the MTP banner read "MTP #1, 15487
/// min" for a protocol opened ten days earlier). Minutes under two hours, hours and minutes under two days, then days and
/// hours.
/// </summary>
public static class ElapsedText
{
    public static string FromMinutes(int minutes)
    {
        if (minutes < 0) minutes = 0;
        if (minutes < 120) return $"{minutes} min";
        if (minutes < 48 * 60) return minutes % 60 == 0 ? $"{minutes / 60} h" : $"{minutes / 60} h {minutes % 60} min";
        var hours = minutes / 60;
        return hours % 24 == 0 ? $"{hours / 24} d" : $"{hours / 24} d {hours % 24} h";
    }
}
