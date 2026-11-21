using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class IssuedUnitWhereaboutsTests
{
    // TEST-BB-1156: an overdue final-disposition row says where the unit was sent, in which cooler, to whom, and whether
    // the ward received it (end-user testing pass 134: the board did not say where to chase the unit).
    [Fact]
    public void Text_SaysWhereTheUnitWent_AndWhetherTheWardReceivedIt()
    {
        var received = new DateTime(2026, 11, 6, 13, 57, 0, DateTimeKind.Utc);
        Assert.Equal(
            $"ICU 4 · Cooler M1 · to RN Demo · received on the ward {ClinicalTimeText.Local(received)} by RN Demo",
            IssuedUnitWhereabouts.Text("ICU 4", "Cooler M1", "RN Demo", received, "RN Demo"));
        Assert.Equal("Trauma Bay 1 · not received on the ward", IssuedUnitWhereabouts.Text(" Trauma Bay 1 ", null, " ", null, null));
        Assert.Equal("Location not recorded · not received on the ward", IssuedUnitWhereabouts.Text(null, null, null, null, null));
    }
}
