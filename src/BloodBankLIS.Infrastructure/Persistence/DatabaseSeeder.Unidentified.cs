using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

public static partial class DatabaseSeeder
{
    /// <summary>
    /// Unidentified patient demo: one trauma patient registered without identification (temporary UNK- MRN for the seeding
    /// day, estimated age 40), so the Patients list, the chart banner, and the home card show the workflow. Added once:
    /// skipped when any unidentified chart exists, merged or not.
    /// </summary>
    private static async Task EnsureUnidentifiedPatientDemoAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.Patients.AnyAsync(p => p.Unidentified, ct))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var prefix = UnidentifiedPatientRule.DayPrefix(now);
        var taken = await context.Patients.CountAsync(p => p.MedicalRecordNumber.StartsWith(prefix), ct);
        var sequence = taken + 1;
        while (await context.Patients.AnyAsync(p => p.MedicalRecordNumber == UnidentifiedPatientRule.Mrn(now, sequence), ct))
        {
            sequence++;
        }

        context.Patients.Add(new Patient
        {
            MedicalRecordNumber = UnidentifiedPatientRule.Mrn(now, sequence),
            LastName = UnidentifiedPatientRule.LastName,
            FirstName = UnidentifiedPatientRule.FirstName(now, sequence),
            DateOfBirth = UnidentifiedPatientRule.EstimatedBirthDate(DateOnly.FromDateTime(now), 40),
            Sex = Sex.Male,
            Unidentified = true,
            Comment = "Unidentified; estimated age 40 years. Demo: trauma bay 1, arrived by EMS."
        });
        await context.SaveChangesAsync(ct);
    }
}
