using BloodBankLIS.Application.Patients;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Tests;

public class PatientSearchTests
{
    private static readonly PatientDto Kline = new(10, "MRN0010", "Kline", "Nora", null, new DateOnly(1966, 9, 3), Sex.Female, PatientStatus.Active, DateTime.UtcNow, "test");
    private static readonly PatientDto Rhesus = new(3, "MRN0003", "Rhesus", "Nora", null, new DateOnly(1992, 9, 3), Sex.Female, PatientStatus.Active, DateTime.UtcNow, "test");

    // TEST-BB-1113 (end-user testing pass 99): the patient search takes a name as written on a requisition ("Kline, Nora"),
    // a name with a date of birth (yyyy-mm-dd, mm/dd/yyyy, or the year), and still single words; every word must match.
    [Fact]
    public void MatchesSearch_NameAsWritten_AndDateOfBirth()
    {
        Assert.True(Kline.MatchesSearch("Kline, Nora"));
        Assert.True(Kline.MatchesSearch("nora kline"));
        Assert.True(Kline.MatchesSearch("Nora 1966-09-03"));
        Assert.False(Rhesus.MatchesSearch("Nora 1966-09-03"));
        Assert.True(Kline.MatchesSearch("nora 09/03/1966"));
        Assert.True(Kline.MatchesSearch("nora 9/3/1966"));
        Assert.True(Rhesus.MatchesSearch("nora 1992"));
        Assert.False(Kline.MatchesSearch("nora 1992"));
        Assert.True(Kline.MatchesSearch("MRN0010"));
        Assert.True(Kline.MatchesSearch("1966-09-03"));
        Assert.False(Kline.MatchesSearch("Kline, Patricia"));
    }
}
