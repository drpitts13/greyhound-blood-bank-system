using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

public static partial class DatabaseSeeder
{
    /// <summary>
    /// Mother and infant demo: the RhIG demo mother (MRN0025, O NEG, delivered yesterday) has her newborn linked
    /// (MRN0027, O POS from cord blood), so each chart shows the other and the RhIG decision can be checked against the
    /// infant's type. Added once.
    /// </summary>
    private static async Task EnsureMotherInfantDemoAsync(BloodBankDbContext context, CancellationToken ct)
    {
        var mother = await context.Patients.FirstOrDefaultAsync(p => p.MedicalRecordNumber == "MRN0025", ct);
        if (mother is null || await context.Patients.AnyAsync(p => p.MedicalRecordNumber == "MRN0027", ct))
        {
            return;
        }

        var infant = new Patient
        {
            MedicalRecordNumber = "MRN0027",
            LastName = "DEMO-RHIG",
            FirstName = "Baby Girl Maya",
            DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            Sex = Sex.Female,
            MotherPatientId = mother.Id,
            Comment = "Demo: newborn of MRN0025; cord blood typed O POS."
        };
        context.Patients.Add(infant);
        await context.SaveChangesAsync(ct);
        context.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
        {
            PatientId = infant.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            Source = BloodTypeSource.HistoricalImport,
            IsCurrent = true,
            Reason = "Demo: cord blood type."
        });
        await context.SaveChangesAsync(ct);
    }
}
