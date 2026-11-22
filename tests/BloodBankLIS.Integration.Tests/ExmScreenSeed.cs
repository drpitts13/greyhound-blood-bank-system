using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Integration.Tests;

/// <summary>
/// A verified negative antibody screen on an accepted, unexpired specimen: what electronic crossmatch needs as the
/// current screen (Cycle 595). Adds it to the given specimen, or to a new one.
/// </summary>
internal static class ExmScreenSeed
{
    public static async Task<long> AddVerifiedNegativeScreenAsync(
        BloodBankDbContext c, long patientId, DateTime nowUtc, long? specimenId = null)
    {
        if (specimenId is null)
        {
            var specimen = new Specimen
            {
                AccessionNumber = $"ACC-SCR-{Guid.NewGuid():N}"[..20],
                PatientId = patientId,
                SpecimenType = "EDTA",
                CollectedUtc = nowUtc.AddHours(-2),
                ReceivedUtc = nowUtc.AddHours(-1),
                ExpiresUtc = nowUtc.AddDays(2),
                Status = SpecimenStatus.Accepted
            };
            c.Specimens.Add(specimen);
            await c.SaveChangesAsync();
            specimenId = specimen.Id;
        }

        c.TestResults.Add(new TestResult
        {
            SpecimenId = specimenId.Value,
            PatientId = patientId,
            TestCode = "ABSC",
            Value = "Negative",
            Interpretation = "Negative",
            Status = ResultStatus.Verified,
            EnteredBy = "tech-a",
            EnteredUtc = nowUtc.AddMinutes(-30),
            VerifiedBy = "tech-b",
            VerifiedUtc = nowUtc.AddMinutes(-20)
        });
        await c.SaveChangesAsync();
        return specimenId.Value;
    }
}
