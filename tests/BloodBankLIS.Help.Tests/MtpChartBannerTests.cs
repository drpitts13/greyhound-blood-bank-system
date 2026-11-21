using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class MtpChartBannerTests
{
    // TEST-BB-1344 (end-user testing pass 317): the chart says the patient's massive transfusion protocol is open: its
    // number, when it was called and by whom, and how long it has been open.
    [Fact]
    public void For_SaysTheProtocolIsOpen()
    {
        var now = new DateTime(2026, 11, 20, 14, 0, 0, DateTimeKind.Utc);
        var activated = now.AddDays(-13).AddHours(-4);
        var mtp = new MtpActivationDto(1, 11, "MRN0021", "Demo-Massive, Morgan", MtpActivationStatus.Active, "Dr. Demo Trauma", "Haemorrhage",
            "Trauma Bay 1", activated, "tech", null, null, null, 0, [], []);
        Assert.Equal($"Massive transfusion protocol #1 active: called {ClinicalTimeText.Local(activated)} by Dr. Demo Trauma, open 13 d.",
            MtpChartBanner.For(mtp, now));
    }
}
