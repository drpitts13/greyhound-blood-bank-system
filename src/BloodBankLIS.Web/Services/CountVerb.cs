namespace BloodBankLIS.Web.Services;

/// <summary>
/// The verb agreeing with a count in a worklist summary (end-user testing pass 303: the antibody identification worklist
/// read "1 need interpretation" and "1 have an expired specimen").
/// </summary>
public static class CountVerb
{
    public static string For(int count, string singular, string plural) => count == 1 ? singular : plural;
}
