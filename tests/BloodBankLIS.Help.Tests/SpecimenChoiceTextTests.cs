using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class SpecimenChoiceTextTests
{
    // TEST-BB-1247: the Compatibility specimen pickers say when an expired tube expired, and a usable tube's status and
    // when it expires (end-user testing pass 225: "Expired expires …").
    [Fact]
    public void For_WordsExpiredAndUsableTubes()
    {
        var now = new DateTime(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);
        var past = now.AddDays(-8);
        var future = now.AddDays(2);
        string Local(DateTime d) => d.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        Assert.Equal($"ACC0022 — EDTA (expired {Local(past)})", SpecimenChoiceText.For("ACC0022", "EDTA", SpecimenStatus.Expired, past, now));
        Assert.Equal($"ACC1 — EDTA (expired {Local(past)})", SpecimenChoiceText.For("ACC1", "EDTA", SpecimenStatus.Accepted, past, now));
        Assert.Equal($"ACC2 — EDTA (Accepted, expires {Local(future)})", SpecimenChoiceText.For("ACC2", "EDTA", SpecimenStatus.Accepted, future, now));
        Assert.Equal("ACC3 — EDTA (Received)", SpecimenChoiceText.For("ACC3", "EDTA", SpecimenStatus.Received, null, now));
    }
}
