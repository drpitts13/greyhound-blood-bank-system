using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

/// <summary>
/// Demo data for walking an antibody identification (Cycle 642): MRN0004 has an accepted tube with a workup in progress,
/// part read, so the antigram, Rest 0 and assistance have something to show on the development data.
/// </summary>
public class SeederAntibodyWorkupDemoTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public SeederAntibodyWorkupDemoTests(SqliteContextFactory factory) => _factory = factory;

    // TEST-BB-783: the seed adds ACC-DEMO-ABID1 (accepted, in date) for MRN0004 with one workup in progress on the
    // teaching panel: IS negative on every cell, AHG 2+ on cells 2 and 5 only, 37C not read, nothing interpreted.
    [Fact]
    public async Task KellermanHasAWorkupInProgress()
    {
        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        await using var verify = _factory.Create();
        var patient = await verify.Patients.SingleAsync(p => p.MedicalRecordNumber == "MRN0004");
        var specimen = await verify.Specimens.SingleAsync(s => s.AccessionNumber == "ACC-DEMO-ABID1");
        Assert.Equal((patient.Id, SpecimenStatus.Accepted), (specimen.PatientId, specimen.Status));
        Assert.True(specimen.ExpiresUtc > DateTime.UtcNow);

        var workup = await verify.AntibodyIdentificationWorkups.SingleAsync(w => w.SpecimenId == specimen.Id);
        Assert.Equal((patient.Id, AntibodyWorkupStatus.InProgress), (workup.PatientId, workup.Status));
        Assert.Null(workup.TechnologistInterpretation);
        var lot = await verify.AntibodyPanelLots.SingleAsync(l => l.Id == workup.PrimaryLotId);
        Assert.Equal("GHP-ABID-2026A", lot.LotNumber);
        Assert.True(await verify.AntibodyIdentificationWorkupLots.AnyAsync(l => l.WorkupId == workup.Id && l.LotId == lot.Id && l.IsPrimary));

        var cells = await verify.AntibodyPanelCells.Where(c => c.LotId == lot.Id).ToDictionaryAsync(c => c.Id, c => c.CellNumber);
        var reactions = await verify.AntibodyIdentificationReactions.Where(r => r.WorkupId == workup.Id).ToListAsync();
        Assert.Equal(cells.Count, reactions.Count(r => r.PhaseCode == "IS" && r.Strength == ReactionGrade.Negative));
        Assert.Equal(["2", "5"], reactions.Where(r => r.PhaseCode == "AHG").Select(r => cells[r.CellId]).Order().ToArray());
        Assert.All(reactions.Where(r => r.PhaseCode == "AHG"), r => Assert.Equal(ReactionGrade.TwoPlus, r.Strength));
        Assert.DoesNotContain(reactions, r => r.PhaseCode == "37C");
        Assert.True(await verify.AuditEvents.AnyAsync(a => a.EntityType == "AntibodyIdentificationWorkup" && a.EntityId == workup.Id));
    }

    // TEST-BB-784: seeding again adds nothing, and a workup the bench has since completed is not reopened or replaced.
    [Fact]
    public async Task SeedingAgain_AddsNothing()
    {
        using var factory = new SqliteContextFactory();
        await using (var context = factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        await using (var context = factory.Create())
        {
            var workup = await context.AntibodyIdentificationWorkups
                .SingleAsync(w => w.Comment != null && w.Comment.StartsWith("Demo: IS read"));
            workup.Status = AntibodyWorkupStatus.Completed;
            await context.SaveChangesAsync();
        }

        await using (var context = factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        await using var verify = factory.Create();
        Assert.Equal(1, await verify.Specimens.CountAsync(s => s.AccessionNumber == "ACC-DEMO-ABID1"));
        var specimenId = (await verify.Specimens.SingleAsync(s => s.AccessionNumber == "ACC-DEMO-ABID1")).Id;
        var workups = await verify.AntibodyIdentificationWorkups.Where(w => w.SpecimenId == specimenId).ToListAsync();
        Assert.Equal(AntibodyWorkupStatus.Completed, Assert.Single(workups).Status);
    }
}
