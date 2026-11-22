namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// How long something has gone on, read at a glance: minutes under two hours, hours and minutes under two days, then days
/// and hours (end-user testing passes 195–196: an MTP read "15487 min" and "no unit issued for 258 h").
/// </summary>
public static class ElapsedWords
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
