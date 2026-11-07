using BloodBankLIS.Application.Patients;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Tests;

public class PatientPickerLabelTests
{
    private static PatientDto Patient(string mrn = "MRN0004") =>
        new(4, mrn, "Kellerman", "Diane", null, new DateOnly(1957, 2, 18), Sex.Female, PatientStatus.Active,
            DateTime.UtcNow, "system");

    // TEST-BB-438: pickers name the patient with type and alerts; a merged-away record says so instead.
    [Fact]
    public void PickerLabel_ShowsTypeAndAlerts_AndMarksMergedRecords()
    {
        Assert.Equal("Kellerman, Diane (MRN0004) · no type on file", Patient().PickerLabel);

        var flagged = Patient() with
        {
            CurrentBloodType = "O+",
            ActiveAntibodies = ["anti-E", "anti-K"],
            ActiveSpecialRequirements = 2
        };
        Assert.Equal("Kellerman, Diane (MRN0004) · O+ · anti-E, anti-K · 2 special requirements", flagged.PickerLabel);
        Assert.EndsWith("· O+ · 1 special requirement", (Patient() with { CurrentBloodType = "O+", ActiveSpecialRequirements = 1 }).PickerLabel);

        Assert.EndsWith("· O+ · 2 notes", (Patient() with { CurrentBloodType = "O+", InformationalNotes = 2 }).PickerLabel);

        var merged = flagged with { Status = PatientStatus.Merged, MergedIntoPatientId = 9 };
        Assert.Equal("Kellerman, Diane (MRN0004) · merged into another record", merged.PickerLabel);
    }

    // TEST-BB-506: pickers say where the patient is after the name; a second open visit is flagged; a visit with no
    // recorded place gives its number; no open visit adds nothing; a merged-away record still says only that.
    [Fact]
    public void PickerLabel_SaysWhereThePatientIs()
    {
        var located = Patient() with { CurrentBloodType = "O+", CurrentVisitNumber = "V1", CurrentVisitWhere = "Inpatient · ICU 4" };
        Assert.Equal("Kellerman, Diane (MRN0004) · Inpatient · ICU 4 · O+", located.PickerLabel);
        Assert.Equal("Kellerman, Diane (MRN0004) · Inpatient · ICU 4 (+1 more open) · O+", (located with { OtherOpenVisits = 1 }).PickerLabel);
        Assert.Equal("Kellerman, Diane (MRN0004) · visit V1 · O+", (located with { CurrentVisitWhere = "" }).PickerLabel);
        Assert.Equal("Kellerman, Diane (MRN0004) · merged into another record",
            (located with { Status = PatientStatus.Merged, MergedIntoPatientId = 9 }).PickerLabel);
    }
}
