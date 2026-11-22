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

    /// <summary>
    /// Maternal antibody demo (end-user testing pass 110): a mother with anti-K on record (MRN0028) and her newborn linked
    /// (MRN0029), so the infant's chart shows the maternal antibody and offers the K-negative requirement. Added once.
    /// </summary>
    private static async Task EnsureMaternalAntibodyDemoAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.Patients.AnyAsync(p => p.MedicalRecordNumber == "MRN0028" || p.MedicalRecordNumber == "MRN0029", ct))
        {
            return;
        }

        var kell = await context.BloodAttributeDefinitions.FirstOrDefaultAsync(d => d.Code == "K" && d.IsActive, ct);
        if (kell is null)
        {
            return;
        }

        var mother = new Patient
        {
            MedicalRecordNumber = "MRN0028",
            LastName = "DEMO-MATAB",
            FirstName = "Lena",
            DateOfBirth = new DateOnly(1992, 4, 9),
            Sex = Sex.Female,
            Comment = "Demo: anti-K in pregnancy; newborn MRN0029."
        };
        context.Patients.Add(mother);
        await context.SaveChangesAsync(ct);
        var infant = new Patient
        {
            MedicalRecordNumber = "MRN0029",
            LastName = "DEMO-MATAB",
            FirstName = "Baby Boy Lena",
            DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            Sex = Sex.Male,
            MotherPatientId = mother.Id,
            Comment = "Demo: newborn of MRN0028 (maternal anti-K)."
        };
        context.Patients.Add(infant);
        await context.SaveChangesAsync(ct);
        context.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
        {
            PatientId = mother.Id,
            Abo = AboGroup.A,
            RhD = RhType.Positive,
            Source = BloodTypeSource.HistoricalImport,
            IsCurrent = true,
            Reason = "Demo: prenatal type."
        });
        context.AntibodyHistory.Add(new AntibodyHistory
        {
            PatientId = mother.Id,
            BloodAttributeDefinitionId = kell.Id,
            AntibodySpecificity = "anti-K",
            Status = AntibodyStatus.Identified,
            IsActive = true,
            Comment = "Demo: identified at the prenatal screen."
        });
        await context.SaveChangesAsync(ct);
    }
}
