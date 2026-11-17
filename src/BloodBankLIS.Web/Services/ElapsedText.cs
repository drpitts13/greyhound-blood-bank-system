namespace BloodBankLIS.Web.Services;

/// <summary>How long something has been active, read at a glance (end-user testing pass 195); see <see cref="BloodBankLIS.Domain.Rules.ElapsedWords"/>.</summary>
public static class ElapsedText
{
    public static string FromMinutes(int minutes) => BloodBankLIS.Domain.Rules.ElapsedWords.FromMinutes(minutes);
}
