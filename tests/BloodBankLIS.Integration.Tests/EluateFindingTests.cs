using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-317 (OCD-042): eluate alloantibodies reach antibody history only when a second person verifies.
public class EluateFindingTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private DatElutionWorkupService Service(BloodBankDbContext c, string user)
    {
        var who = new TestCurrentUser(user, "WS-1");
        return new(new EfRepository<DatElutionWorkup>(c), new EfRepository<Patient>(c), new EfRepository<Specimen>(c),
            c, _factory.Clock, who,
            audit: new AuditWriter(c, _factory.Clock, who, new StaticEnvironmentInfo("Development", isDevMode: false)),
            findings: new EfRepository<DatElutionEluateFinding>(c),
            antibodies: new EfRepository<AntibodyHistory>(c),
            attributes: new EfRepository<BloodAttributeDefinition>(c));
    }

    // TEST-BB-1014: a DAT workup keeps the polyspecific, anti-IgG, anti-C3 and control grades, and refuses grades that do
    // not match the result (end-user testing pass 9).
    [Fact]
    public async Task DatGrades_AreKept_AndMustMatchTheResult()
    {
        var (patientId, workupId) = await SeedOpenAsync();
        await using var c = _factory.Create();
        var saved = await Service(c, "tech1").UpdateAsync(workupId, new UpdateDatElutionWorkupRequest(
            DatResult: DatWorkupResult.Positive, ElutionResult: "Reactive",
            PolyspecificGrade: ReactionGrade.TwoPlus, IgGGrade: ReactionGrade.TwoPlus, C3Grade: ReactionGrade.Negative,
            ControlGrade: ReactionGrade.Negative));
        Assert.True(saved.Succeeded, saved.Error);
        var dto = Assert.Single(await Service(c, "tech1").ListForPatientAsync(patientId));
        Assert.Equal((ReactionGrade.TwoPlus, ReactionGrade.TwoPlus, ReactionGrade.Negative, ReactionGrade.Negative),
            (dto.PolyspecificGrade, dto.IgGGrade, dto.C3Grade, dto.ControlGrade));

        var refused = await Service(c, "tech1").UpdateAsync(workupId, new UpdateDatElutionWorkupRequest(
            DatResult: DatWorkupResult.Negative, IgGGrade: ReactionGrade.OnePlus));
        Assert.False(refused.Succeeded);
        Assert.Contains("recorded Negative but anti-IgG 1+ reacted", refused.Error);
        await using var v = _factory.Create();
        Assert.Equal(ReactionGrade.TwoPlus, (await v.DatElutionWorkups.FindAsync(workupId))!.IgGGrade);
    }

    [Fact]
    public async Task Verified_PostsAlloantibodiesOnly_WithSourceAndAudit()
    {
        var (patientId, workupId, antiE) = await SeedCompletedWithFindingsAsync(
            new EluateFindingInput(EluateFindingKind.Alloantibody, "anti-E"),
            new EluateFindingInput(EluateFindingKind.Panreactive, "warm autoantibody pattern"));

        await using (var c = _factory.Create())
        {
            var r = await Service(c, "sup1").VerifyAsync(workupId);
            Assert.True(r.Succeeded, r.Error);
        }

        await using var v = _factory.Create();
        var history = Assert.Single(await v.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());
        Assert.Equal("anti-E", history.AntibodySpecificity);
        Assert.Equal(antiE, history.BloodAttributeDefinitionId);
        Assert.Contains("eluate", history.Comment, StringComparison.OrdinalIgnoreCase);
        var findings = await v.DatElutionEluateFindings.Where(f => f.WorkupId == workupId && f.IsActive).ToListAsync();
        Assert.Equal(history.Id, findings.Single(f => f.Kind == EluateFindingKind.Alloantibody).PostedAntibodyHistoryId);
        Assert.Null(findings.Single(f => f.Kind == EluateFindingKind.Panreactive).PostedAntibodyHistoryId);
        Assert.Equal("sup1", (await v.DatElutionWorkups.FindAsync(workupId))!.VerifiedBy);
        Assert.True(await v.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(AntibodyHistory) && a.EntityId == history.Id && a.NewValueJson!.Contains("Eluate")));
    }

    [Fact]
    public async Task TheCompleter_CannotVerify_AndNothingPosts()
    {
        var (patientId, workupId, _) = await SeedCompletedWithFindingsAsync(new EluateFindingInput(EluateFindingKind.Alloantibody, "anti-E"));

        await using var c = _factory.Create();
        var r = await Service(c, "tech1").VerifyAsync(workupId);

        Assert.False(r.Succeeded);
        Assert.Contains("A different person must verify", r.Error);
        Assert.False(await c.AntibodyHistory.AnyAsync(a => a.PatientId == patientId));
    }

    [Fact]
    public async Task OpenWorkup_CannotBeVerified()
    {
        var (patientId, workupId) = await SeedOpenAsync();

        await using var c = _factory.Create();
        var r = await Service(c, "sup1").VerifyAsync(workupId);

        Assert.False(r.Succeeded);
        Assert.Contains("Complete the DAT/elution workup", r.Error);
        Assert.False(await c.AntibodyHistory.AnyAsync(a => a.PatientId == patientId));
    }

    [Fact]
    public async Task UnmatchedAlloantibody_IsRefused_AndPreviousFindingsStay()
    {
        var (_, workupId) = await SeedOpenAsync();
        await EnsureAntiEAsync();
        await using (var c = _factory.Create())
        {
            Assert.True((await Service(c, "tech1").RecordEluateFindingsAsync(workupId, new RecordEluateFindingsRequest(
                [new EluateFindingInput(EluateFindingKind.Alloantibody, "anti-E")]))).Succeeded);
        }

        await using var c2 = _factory.Create();
        var r = await Service(c2, "tech1").RecordEluateFindingsAsync(workupId, new RecordEluateFindingsRequest(
            [new EluateFindingInput(EluateFindingKind.Alloantibody, "anti-Notreal")]));

        Assert.False(r.Succeeded);
        Assert.Contains("not a catalog antibody", r.Error);
        Assert.Equal("anti-E", (await c2.DatElutionEluateFindings.SingleAsync(f => f.WorkupId == workupId && f.IsActive)).Specificity);
    }

    [Fact]
    public async Task ReplacingFindings_RetiresTheOldRows_NeverDeletes()
    {
        var (_, workupId) = await SeedOpenAsync();
        await EnsureAntiEAsync();
        await using (var c = _factory.Create())
        {
            await Service(c, "tech1").RecordEluateFindingsAsync(workupId, new RecordEluateFindingsRequest(
                [new EluateFindingInput(EluateFindingKind.Alloantibody, "anti-E")]));
        }

        await using (var c = _factory.Create())
        {
            await Service(c, "tech1").RecordEluateFindingsAsync(workupId, new RecordEluateFindingsRequest(
                [new EluateFindingInput(EluateFindingKind.Nonspecific, null)]));
        }

        await using var v = _factory.Create();
        var all = await v.DatElutionEluateFindings.Where(f => f.WorkupId == workupId).ToListAsync();
        Assert.Equal(2, all.Count);
        Assert.False(all.Single(f => f.Kind == EluateFindingKind.Alloantibody).IsActive);
        Assert.True(all.Single(f => f.Kind == EluateFindingKind.Nonspecific).IsActive);
    }

    [Fact]
    public async Task AntibodyAlreadyOnFile_IsNotPostedTwice()
    {
        var (patientId, workupId, antiE) = await SeedCompletedWithFindingsAsync(new EluateFindingInput(EluateFindingKind.Alloantibody, "anti-E"));
        await using (var c = _factory.Create())
        {
            c.AntibodyHistory.Add(new AntibodyHistory
            {
                PatientId = patientId, BloodAttributeDefinitionId = antiE, AntibodySpecificity = "anti-E",
                Status = AntibodyStatus.Identified, IsActive = true
            });
            await c.SaveChangesAsync();
        }

        await using (var c = _factory.Create())
        {
            Assert.True((await Service(c, "sup1").VerifyAsync(workupId)).Succeeded);
        }

        await using var v = _factory.Create();
        Assert.Single(await v.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());
        Assert.Null((await v.DatElutionEluateFindings.SingleAsync(f => f.WorkupId == workupId && f.IsActive)).PostedAntibodyHistoryId);
    }

    [Fact]
    public async Task VerifiedWorkup_CannotBeVoided_OrHaveFindingsChanged()
    {
        var (_, workupId, _) = await SeedCompletedWithFindingsAsync(new EluateFindingInput(EluateFindingKind.Alloantibody, "anti-E"));
        await using (var c = _factory.Create())
        {
            Assert.True((await Service(c, "sup1").VerifyAsync(workupId)).Succeeded);
        }

        await using var c2 = _factory.Create();
        var voided = await Service(c2, "tech1").VoidAsync(workupId, new VoidDatElutionWorkupRequest("Wrong patient"));
        Assert.False(voided.Succeeded);
        Assert.Contains("cannot be voided", voided.Error);
        var changed = await Service(c2, "tech1").RecordEluateFindingsAsync(workupId, new RecordEluateFindingsRequest([]));
        Assert.False(changed.Succeeded);
        Assert.Contains("cannot change after the workup is verified", changed.Error);
    }

    private async Task<long> EnsureAntiEAsync()
    {
        await using var c = _factory.Create();
        var existing = await c.BloodAttributeDefinitions.FirstOrDefaultAsync(a => a.Code == "E");
        if (existing is not null)
        {
            return existing.Id;
        }

        var attr = new BloodAttributeDefinition
        {
            Code = "E", Name = "E", AntibodyName = "anti-E", IsClinicallySignificant = true, SortOrder = 20,
            IsActive = true, IsDraft = false, EffectiveUtc = _factory.Clock.UtcNow, Version = 1
        };
        c.BloodAttributeDefinitions.Add(attr);
        await c.SaveChangesAsync();
        return attr.Id;
    }

    private async Task<(long PatientId, long WorkupId)> SeedOpenAsync()
    {
        await using var c = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = "MRN-ELU-" + Guid.NewGuid().ToString("N")[..6], LastName = "Eluate", FirstName = "Eli",
            DateOfBirth = new DateOnly(1960, 2, 2), Sex = Sex.Male
        };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        var created = await Service(c, "tech1").CreateAsync(patient.Id, new CreateDatElutionWorkupRequest(
            DatResult: DatWorkupResult.Positive, DatMethod: "Polyspecific AHG", ElutionResult: "Reactive"));
        Assert.True(created.Succeeded, created.Error);
        return (patient.Id, created.Value!.Id);
    }

    private async Task<(long PatientId, long WorkupId, long AntiE)> SeedCompletedWithFindingsAsync(params EluateFindingInput[] findings)
    {
        var antiE = await EnsureAntiEAsync();
        var (patientId, workupId) = await SeedOpenAsync();
        await using var c = _factory.Create();
        var recorded = await Service(c, "tech1").RecordEluateFindingsAsync(workupId, new RecordEluateFindingsRequest(findings));
        Assert.True(recorded.Succeeded, recorded.Error);
        var completed = await Service(c, "tech1").CompleteAsync(workupId);
        Assert.True(completed.Succeeded, completed.Error);
        return (patientId, workupId, antiE);
    }
}
