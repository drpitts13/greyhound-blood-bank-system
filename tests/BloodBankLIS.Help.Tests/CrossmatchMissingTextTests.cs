using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class CrossmatchMissingTextTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 15, 0, 0, DateTimeKind.Utc);

    private static SpecimenDto Tube(string accession, DateTime? expires, SpecimenStatus status = SpecimenStatus.Accepted) =>
        new(1, accession, 22, "EDTA", null, null, null, null, Now.AddDays(-5), Now.AddDays(-5), expires, status, null);

    // TEST-BB-1313 (end-user testing pass 287): the Record crossmatch hint joins what is missing with "and", and when the
    // patient's only specimens are expired it names the latest and says to collect a new one, instead of "accession one
    // if none is listed" while two were listed.
    [Fact]
    public void Missing_JoinsWithAnd_AndNamesTheLatestExpiredSpecimen()
    {
        Assert.Equal("Choose a patient, a specimen and a unit first.", CrossmatchMissingText.For(false, false, false, [], Now));
        Assert.Equal("Choose a unit first.", CrossmatchMissingText.For(true, true, false, [Tube("ACC1", Now.AddDays(1))], Now));
        Assert.Null(CrossmatchMissingText.For(true, true, true, [], Now));

        var expired = Now.AddDays(-7);
        var tubes = new[] { Tube("ACC0022", Now.AddDays(-10)), Tube("ACC-DEMO-RCV1", expired) };
        Assert.Equal(
            $"Choose a specimen (this patient has no current one: the latest, ACC-DEMO-RCV1, expired {ClinicalTimeText.Local(expired)}; collect a new one and accession it on the chart) and a unit first.",
            CrossmatchMissingText.For(true, false, false, tubes, Now));
        Assert.True(CrossmatchMissingText.NoCurrentSpecimen(tubes, Now));

        Assert.Equal("Choose a specimen (this patient has none: collect one and accession it on the chart) first.",
            CrossmatchMissingText.For(true, false, true, [], Now));
        Assert.Equal("Choose a specimen first.", CrossmatchMissingText.For(true, false, true, [Tube("ACC2", Now.AddDays(2))], Now));
        Assert.False(CrossmatchMissingText.NoCurrentSpecimen([Tube("ACC2", Now.AddDays(2))], Now));
    }
}
