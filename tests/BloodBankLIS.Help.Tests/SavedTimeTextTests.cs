using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class SavedTimeTextTests
{
    // TEST-BB-1329 (end-user testing pass 302): a saved text's UTC times are shown in local time, every one, with the rest
    // of the text unchanged; a time without "UTC" and an empty text are left alone.
    [Fact]
    public void Local_ShowsSavedUtcTimesInLocalTime()
    {
        var reading = new DateTime(2026, 11, 12, 16, 43, 0, DateTimeKind.Utc);
        var quarantined = new DateTime(2026, 11, 12, 17, 22, 0, DateTimeKind.Utc);
        Assert.Equal(
            $"FRIDGE-1 read 8.5 °C at {ClinicalTimeText.Local(reading)} against a range of 1.0 to 6.0 °C. {ClinicalTimeText.Local(quarantined)}: 30 unit(s) quarantined.",
            SavedTimeText.Local("FRIDGE-1 read 8.5 °C at 2026-11-12 16:43 UTC against a range of 1.0 to 6.0 °C. 2026-11-12 17:22 UTC: 30 unit(s) quarantined."));
        Assert.Equal("Swept at 2026-11-17 07:01.", SavedTimeText.Local("Swept at 2026-11-17 07:01."));
        Assert.Equal(string.Empty, SavedTimeText.Local(null));
    }
}
