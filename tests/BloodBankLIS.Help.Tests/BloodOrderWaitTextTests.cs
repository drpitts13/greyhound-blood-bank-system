using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class BloodOrderWaitTextTests
{
    // TEST-BB-1190: a blood order still to prepare says how long it has waited (end-user testing pass 167).
    [Fact]
    public void Text_SaysHowLongTheOrderHasWaited()
    {
        var ordered = new DateTime(2026, 11, 8, 12, 27, 0, DateTimeKind.Utc);
        var text = BloodOrderWaitText.Text(ordered, ordered.AddDays(8).AddHours(2));
        Assert.StartsWith($"ordered {ordered.ToLocalTime():yyyy-MM-dd HH:mm} · waiting ", text);
        Assert.EndsWith("8 d", text);
        Assert.EndsWith("· waiting 40 min", BloodOrderWaitText.Text(ordered, ordered.AddMinutes(40)));

        // TEST-BB-1191: the Test Worklist says how long a pending test has waited (pass 168).
        Assert.Equal("waiting 10 d", BloodOrderWaitText.Waiting(ordered, ordered.AddDays(10).AddHours(1)));
    }
}
