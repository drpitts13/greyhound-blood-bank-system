using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ClinicalTimeTextTests
{
    // TEST-BB-717: an on-screen message uses the local time the pages show; saved text says UTC. A stored time read back
    // without a kind is treated as UTC.
    [Fact]
    public void Times_AreLocalOnScreen_AndLabelledUtcWhenSaved()
    {
        var utc = new DateTime(2026, 11, 9, 12, 49, 0, DateTimeKind.Utc);
        var unspecified = DateTime.SpecifyKind(utc, DateTimeKind.Unspecified);

        Assert.Equal(utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), ClinicalTimeText.Local(utc));
        Assert.Equal(ClinicalTimeText.Local(utc), ClinicalTimeText.Local(unspecified));
        Assert.Equal("2026-11-09 12:49 UTC", ClinicalTimeText.Utc(utc));
        Assert.Equal("2026-11-09 12:49 UTC", ClinicalTimeText.Utc(unspecified));
        Assert.DoesNotContain("Z", ClinicalTimeText.Local(utc));
    }
}
