using BloodBankLIS.Application.Patients;
using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Application.Tests;

// TEST-BB-669: the compact antibody list (patient list, find box, Blood orders to prepare, picker labels) names an
// antibody known only from another facility's report with that facility, as the downtime sheet does; one also found
// here reads plain; inactive records do not count; spellings differing only in case are one antibody.
public class ActiveAntibodiesLabelTests
{
    private static AntibodyHistory Row(string specificity, bool active = true, string? facility = null) =>
        new() { PatientId = 1, AntibodySpecificity = specificity, IsActive = active, ReportingFacility = facility };

    [Fact]
    public void OutsideReports_NameTheFacility_FoundHereReadsPlain()
    {
        var labels = PatientBloodBankFlags.ActiveAntibodies(
        [
            Row("anti-K"),
            Row("anti-Jka", facility: " St. Mary's Hospital "),
            Row("anti-E", facility: "St. Mary's Hospital"),
            Row("anti-E"),
            Row("anti-c", active: false, facility: "St. Mary's Hospital"),
            Row("Anti-Fya", facility: "County General"),
            Row("anti-Fya", facility: "County General")
        ]);

        Assert.Equal(new[]
        {
            "anti-E",
            "Anti-Fya (reported by County General)",
            "anti-Jka (reported by St. Mary's Hospital)",
            "anti-K"
        }, labels);
    }

    [Fact]
    public void NoActiveAntibodies_IsEmpty() =>
        Assert.Empty(PatientBloodBankFlags.ActiveAntibodies([Row("anti-c", active: false)]));
}
