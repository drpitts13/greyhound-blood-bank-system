using BloodBankLIS.Application.Patients;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Tests;

// TEST-BB-1272 (end-user testing pass 248): the Patients list counts the possible-duplicate pairs and marks each chart in
// a pair with the other chart's MRN.
public class PossibleDuplicateFlagsTests
{
    private static PatientDto Chart(long id, string mrn) =>
        new(id, mrn, "Demo-Multi", "Riley", null, new DateOnly(1958, 11, 21), Sex.Unknown, PatientStatus.Active, DateTime.UtcNow, "test");

    [Fact]
    public void EachChartInAPair_NamesTheOther()
    {
        var a = Chart(1, "MRN0022");
        var b = Chart(2, "MRN9022");
        var c = Chart(3, "MRN0030");
        var others = PossibleDuplicateFlags.OthersByPatient([new PossibleDuplicatePairDto(a, b, "Same name"), new PossibleDuplicatePairDto(c, a, "Same id")]);

        Assert.Equal(new[] { "MRN0030", "MRN9022" }, others[1]);
        Assert.Equal(new[] { "MRN0022" }, others[2]);
        Assert.Equal(new[] { "MRN0022" }, others[3]);
        Assert.Equal("Possible duplicate of MRN0030, MRN9022", PossibleDuplicateFlags.Badge(others[1]));
    }

    [Fact]
    public void LinkText_CountsThePairs()
    {
        Assert.Equal("Possible duplicates", PossibleDuplicateFlags.LinkText(0));
        Assert.Equal("Possible duplicates (1 pair to review)", PossibleDuplicateFlags.LinkText(1));
        Assert.Equal("Possible duplicates (3 pairs to review)", PossibleDuplicateFlags.LinkText(3));
    }
}
