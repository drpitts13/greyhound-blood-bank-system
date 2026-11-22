using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

public static partial class DatabaseSeeder
{
    /// <summary>
    /// Two O-negative demo mothers so the RhIG tab, list, home card, handoff, lot trace, and timeline have something to
    /// show: MRN0025 with an antepartum dose given (lot RHIG-DEMO-01) and a delivery completed as a candidate but not yet
    /// given; MRN0026 with this morning's delivery still undecided. The decisions and vials are demo entries, not a dosing
    /// rule (OCD-069). Added once.
    /// </summary>
    private static async Task EnsureRhigDemoAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.Patients.AnyAsync(p => p.MedicalRecordNumber == "MRN0025" || p.MedicalRecordNumber == "MRN0026", ct))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var given = new Patient
        {
            MedicalRecordNumber = "MRN0025",
            LastName = "DEMO-RHIG",
            FirstName = "Maya",
            DateOfBirth = new DateOnly(1994, 3, 14),
            Sex = Sex.Female,
            RecentPregnancyUtc = now.AddDays(-1),
            Comment = "Demo: RhIG given antepartum; delivery evaluation completed, RhIG still to give."
        };
        var open = new Patient
        {
            MedicalRecordNumber = "MRN0026",
            LastName = "DEMO-RHIG",
            FirstName = "Noor",
            DateOfBirth = new DateOnly(1998, 8, 2),
            Sex = Sex.Female,
            RecentPregnancyUtc = now.AddHours(-6),
            Comment = "Demo: delivery this morning; RhIG evaluation not yet decided."
        };
        context.Patients.AddRange(given, open);
        await context.SaveChangesAsync(ct);

        foreach (var mother in new[] { given, open })
        {
            // The delivery admission on labor and delivery.
            context.Encounters.Add(new Encounter
            {
                PatientId = mother.Id,
                VisitNumber = $"VIS-{mother.MedicalRecordNumber}",
                EncounterType = EncounterType.Inpatient,
                Status = EncounterStatus.Active,
                AdmitUtc = mother == given ? now.AddDays(-1).AddHours(-4) : now.AddHours(-10),
                CurrentLocation = "Labor and Delivery"
            });
            context.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
            {
                PatientId = mother.Id,
                Abo = AboGroup.O,
                RhD = RhType.Negative,
                Source = BloodTypeSource.HistoricalImport,
                IsCurrent = true,
                Reason = "Demo: historical type on file."
            });
        }

        context.RhigEvaluations.AddRange(
            new RhigEvaluation
            {
                PatientId = given.Id,
                Context = RhigContext.Antepartum,
                EventUtc = now.AddDays(-84),
                InfantRh = RhType.Unknown,
                FetalBleedScreen = FetalBleedScreen.NotDone,
                Decision = RhigDecision.Candidate,
                VialsRecommended = 1,
                DecisionNote = "Demo: antepartum dose per facility procedure.",
                RecordedBy = "DEMO_TECH",
                RecordedUtc = now.AddDays(-84),
                CompletedBy = "DEMO_LEAD",
                CompletedUtc = now.AddDays(-84).AddHours(1),
                GivenLot = "RHIG-DEMO-01",
                GivenVials = 1,
                GivenUtc = now.AddDays(-84).AddHours(2),
                GivenBy = "DEMO_TECH"
            },
            new RhigEvaluation
            {
                PatientId = given.Id,
                Context = RhigContext.Postpartum,
                EventUtc = now.AddDays(-1),
                InfantRh = RhType.Positive,
                FetalBleedScreen = FetalBleedScreen.Negative,
                Decision = RhigDecision.Candidate,
                VialsRecommended = 1,
                RecordedBy = "DEMO_TECH",
                RecordedUtc = now.AddHours(-20),
                CompletedBy = "DEMO_LEAD",
                CompletedUtc = now.AddHours(-18)
            },
            new RhigEvaluation
            {
                PatientId = open.Id,
                Context = RhigContext.Postpartum,
                EventUtc = now.AddHours(-6),
                InfantRh = RhType.Positive,
                FetalBleedScreen = FetalBleedScreen.NotDone,
                Decision = RhigDecision.Undetermined,
                RecordedBy = "DEMO_TECH",
                RecordedUtc = now.AddHours(-5)
            });
        await context.SaveChangesAsync(ct);
    }
}
