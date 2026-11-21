using BloodBankLIS.Application.Patients;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class MotherCandidatesTests
{
    private static PatientDto Patient(long id, string last, Sex sex, DateOnly born, PatientStatus status = PatientStatus.Active, long? mergedInto = null) =>
        new(id, $"MRN{id:0000}", last, "Pat", null, born, sex, status, DateTime.UtcNow, "tech", MergedIntoPatientId: mergedInto);

    // TEST-BB-1328 (end-user testing pass 301): linking an infant's mother offers female (or sex unknown) patients born
    // before the infant, not merged, by name; not the infant, a male patient, another newborn, or a merged chart.
    [Fact]
    public void From_OffersPossibleMothersByName()
    {
        var born = new DateOnly(2026, 11, 6);
        var patients = new[]
        {
            Patient(2, "Newborn", Sex.Male, born),
            Patient(7, "Gravid", Sex.Female, new DateOnly(1994, 3, 8)),
            Patient(17, "Unidentified", Sex.Unknown, new DateOnly(1986, 1, 1)),
            Patient(5, "Trauma", Sex.Male, new DateOnly(1988, 6, 30)),
            Patient(27, "Twin", Sex.Female, born),
            Patient(24, "Merged", Sex.Female, new DateOnly(1990, 1, 1), PatientStatus.Merged, mergedInto: 7),
            Patient(25, "Alpha", Sex.Female, new DateOnly(1994, 3, 14))
        };

        Assert.Equal([25L, 7, 17], MotherCandidates.From(patients, 2, born).Select(p => p.Id));
    }
}
