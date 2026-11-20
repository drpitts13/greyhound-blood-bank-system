using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class CoolerMissingTextTests
{
    // TEST-BB-1339 (end-user testing pass 312): a unit its cooler's last check found missing says so on its own row; the
    // units packed with it, and a cooler never checked, say nothing. The missing units come from the check's record.
    [Fact]
    public void For_NamesTheMissingUnitOnItsRow()
    {
        var checkedUtc = new DateTime(2026, 11, 6, 14, 11, 0, DateTimeKind.Utc);
        var check = new AuditEvent { NewValueJson = """{"Document":"Cooler check","Missing":["W0001230009101"]}""" };
        var cooler = new OpenCoolerDto("Cooler 1", 4, ["Demo-Massive, Morgan"], ["Trauma Bay 1"], checkedUtc, checkedUtc, false)
        {
            MissingUnits = CoolerCheckService.CheckMissing(check)
        };

        Assert.Equal($"Missing from Cooler 1 when it was checked ({ClinicalTimeText.Local(checkedUtc)}): find the unit.",
            CoolerMissingText.For(cooler, "W0001230009101"));
        Assert.Null(CoolerMissingText.For(cooler, "W0001230009102"));
        Assert.Null(CoolerMissingText.For(cooler with { LastCheckedUtc = null }, "W0001230009101"));
        Assert.Null(CoolerMissingText.For(null, "W0001230009101"));
        Assert.Empty(CoolerCheckService.CheckMissing(new AuditEvent { NewValueJson = "not json" }));
    }
}
