using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Patients;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Integration.Tests;

public class PatientMergeServiceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public PatientMergeServiceTests(SqliteContextFactory factory) => _factory = factory;

    [Fact]
    public async Task Merge_ReassignsHistory_AndRetiresDuplicate()
    {
        await using var c = _factory.Create();
        var (survivor, duplicate) = await SeedPairAsync(c, "MAN-OK");
        c.AntibodyHistory.Add(new AntibodyHistory
        {
            PatientId = duplicate.Id,
            AntibodySpecificity = "anti-E",
            Status = AntibodyStatus.Identified,
            IsActive = true
        });
        c.Encounters.Add(new Encounter
        {
            PatientId = duplicate.Id,
            VisitNumber = "VIS-MAN-OK",
            Status = EncounterStatus.Active,
            AdmitUtc = _factory.Clock.UtcNow
        });
        await c.SaveChangesAsync();

        var result = await Merge(c).MergeAsync(survivor.Id, duplicate.Id, "Same person; registration duplicate.");
        Assert.True(result.Succeeded);
        Assert.Equal(PatientStatus.Merged, (await c.Patients.FindAsync(duplicate.Id))!.Status);
        Assert.Equal(survivor.Id, (await c.Patients.FindAsync(duplicate.Id))!.MergedIntoPatientId);
        Assert.True(c.AntibodyHistory.Any(a => a.PatientId == survivor.Id && a.AntibodySpecificity == "anti-E"));
        Assert.True(c.Encounters.Any(e => e.VisitNumber == "VIS-MAN-OK" && e.PatientId == survivor.Id));
        Assert.True(c.PatientIdentifiers.Any(i =>
            i.PatientId == survivor.Id
            && i.IdentifierType == IdentityTokenType.PriorMedicalRecordNumber
            && i.Value == duplicate.MedicalRecordNumber));
    }

    [Fact]
    public async Task Merge_DiscordantAbo_IsHardStopped()
    {
        await using var c = _factory.Create();
        var (survivor, duplicate) = await SeedPairAsync(c, "MAN-ABO");
        c.PatientBloodTypeHistory.AddRange(
            new PatientBloodTypeHistory
            {
                PatientId = survivor.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                IsCurrent = true,
                Source = BloodTypeSource.TestResult
            },
            new PatientBloodTypeHistory
            {
                PatientId = duplicate.Id,
                Abo = AboGroup.A,
                RhD = RhType.Positive,
                IsCurrent = true,
                Source = BloodTypeSource.TestResult
            });
        await c.SaveChangesAsync();

        var result = await Merge(c).MergeAsync(survivor.Id, duplicate.Id, "Attempted merge.");
        Assert.False(result.Succeeded);
        Assert.Contains("ABO", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(PatientStatus.Active, (await c.Patients.FindAsync(duplicate.Id))!.Status);
    }

    [Fact]
    public async Task Merge_ReassignsAntibodyIdWorkup_AndWarnsWhenOpen()
    {
        await using var c = _factory.Create();
        var (survivor, duplicate) = await SeedPairAsync(c, "MAN-ABID");
        c.AntibodyIdentificationWorkups.Add(new AntibodyIdentificationWorkup
        {
            PatientId = duplicate.Id,
            PrimaryLotId = 1,
            Status = AntibodyWorkupStatus.InProgress
        });
        await c.SaveChangesAsync();

        var result = await Merge(c).MergeAsync(survivor.Id, duplicate.Id, "Same person; open identification follows.");
        Assert.True(result.Succeeded, result.Error);
        Assert.Contains(
            result.Warnings,
            w => w.Code == AntibodyIdentificationWorkupScopeRule.MergeWorkupCode);
        Assert.True(c.AntibodyIdentificationWorkups.Any(w =>
            w.PatientId == survivor.Id && w.Status == AntibodyWorkupStatus.InProgress));
        Assert.False(c.AntibodyIdentificationWorkups.Any(w =>
            w.PatientId == duplicate.Id));
    }

    [Fact]
    public async Task Merge_BothOpenAntibodyIdWorkups_IsHardStopped()
    {
        await using var c = _factory.Create();
        var (survivor, duplicate) = await SeedPairAsync(c, "MAN-ABID2");
        c.AntibodyIdentificationWorkups.AddRange(
            new AntibodyIdentificationWorkup
            {
                PatientId = survivor.Id,
                PrimaryLotId = 1,
                Status = AntibodyWorkupStatus.PendingInterpretation
            },
            new AntibodyIdentificationWorkup
            {
                PatientId = duplicate.Id,
                PrimaryLotId = 1,
                Status = AntibodyWorkupStatus.InProgress
            });
        await c.SaveChangesAsync();

        var result = await Merge(c).MergeAsync(survivor.Id, duplicate.Id, "Attempted merge with two open IDs.");
        Assert.False(result.Succeeded);
        Assert.Contains("identification of record", result.Error);
        Assert.Equal(PatientStatus.Active, (await c.Patients.FindAsync(duplicate.Id))!.Status);
        Assert.Equal(1, c.AntibodyIdentificationWorkups.Count(w => w.PatientId == survivor.Id));
        Assert.Equal(1, c.AntibodyIdentificationWorkups.Count(w => w.PatientId == duplicate.Id));
    }

    [Fact]
    public async Task Merge_Self_IsHardStopped()
    {
        await using var c = _factory.Create();
        var (survivor, _) = await SeedPairAsync(c, "MAN-SELF");

        var result = await Merge(c).MergeAsync(survivor.Id, survivor.Id, "Self.");
        Assert.False(result.Succeeded);
        Assert.Contains("cannot be merged into itself", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FindByMrn_FollowsMergedPointer()
    {
        await using var c = _factory.Create();
        var (survivor, duplicate) = await SeedPairAsync(c, "MAN-FOL");
        Assert.True((await Merge(c).MergeAsync(survivor.Id, duplicate.Id, "Follow test.")).Succeeded);

        var resolved = await Merge(c).FindByMrnAsync(duplicate.MedicalRecordNumber, followMerge: true);
        Assert.NotNull(resolved);
        Assert.Equal(survivor.Id, resolved!.Id);
    }

    [Fact]
    public async Task Merge_WithoutPatientMerge_IsRejected()
    {
        await using var c = _factory.Create();
        var (survivor, duplicate) = await SeedPairAsync(c, "MAN-PERM");

        var denied = await Merge(c, new FixedPermissionEvaluator(1, PermissionCodes.PatientWrite))
            .MergeAsync(survivor.Id, duplicate.Id, "Unauthorized merge.");
        Assert.False(denied.Succeeded);
        Assert.Contains("patient.merge", denied.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(PatientStatus.Active, (await c.Patients.FindAsync(duplicate.Id))!.Status);

        var allowed = await Merge(c, new FixedPermissionEvaluator(1, PermissionCodes.PatientMerge))
            .MergeAsync(survivor.Id, duplicate.Id, "Authorized merge.");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(PatientStatus.Merged, (await c.Patients.FindAsync(duplicate.Id))!.Status);
    }

    [Fact]
    public async Task MergeFromHl7_DoesNotRequirePatientMerge()
    {
        await using var c = _factory.Create();
        var (survivor, duplicate) = await SeedPairAsync(c, "MAN-HL7");

        var result = await Merge(c, new FixedPermissionEvaluator(1, PermissionCodes.PatientWrite))
            .MergeFromHl7Async(survivor.Id, duplicate.Id, "HL7 ADT^A40 merge.");
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(PatientStatus.Merged, (await c.Patients.FindAsync(duplicate.Id))!.Status);
    }

    [Fact]
    public async Task Unmerge_RestoresOrdersSpecimensAntibodiesAndElution()
    {
        await using var c = _factory.Create();
        var (survivor, duplicate) = await SeedPairAsync(c, "MAN-UNDO");
        var location = new OrderingLocation { Code = "ED-UNDO", Name = "Emergency" };
        c.OrderingLocations.Add(location);
        await c.SaveChangesAsync();
        var encounter = new Encounter
        {
            PatientId = duplicate.Id,
            VisitNumber = "VIS-UNDO",
            Status = EncounterStatus.Active,
            AdmitUtc = _factory.Clock.UtcNow
        };
        c.Encounters.Add(encounter);
        await c.SaveChangesAsync();
        c.Orders.Add(new Order
        {
            OrderNumber = "ORD-UNDO",
            PatientId = duplicate.Id,
            EncounterId = encounter.Id,
            OrderingLocationId = location.Id,
            OrderName = "Type and screen"
        });
        c.Specimens.Add(new Specimen
        {
            AccessionNumber = "ACC-UNDO",
            PatientId = duplicate.Id,
            SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow,
            Status = SpecimenStatus.Accepted
        });
        c.AntibodyHistory.Add(new AntibodyHistory
        {
            PatientId = duplicate.Id,
            AntibodySpecificity = "anti-E",
            Status = AntibodyStatus.Identified,
            IsActive = true
        });
        var elution = new DatElutionWorkup { PatientId = duplicate.Id };
        c.DatElutionWorkups.Add(elution);
        await c.SaveChangesAsync();
        var antibodyId = c.AntibodyHistory.Single(a => a.PatientId == duplicate.Id && a.AntibodySpecificity == "anti-E").Id;
        var elutionId = elution.Id;

        Assert.True((await Merge(c).MergeAsync(survivor.Id, duplicate.Id, "Same person.")).Succeeded, "merge");
        Assert.Equal(survivor.Id, c.Orders.Single(o => o.OrderNumber == "ORD-UNDO").PatientId);

        var undone = await Merge(c).UnmergeAsync(duplicate.Id, "Registration error; not the same person.");
        Assert.True(undone.Succeeded, undone.Error);
        Assert.Equal(PatientStatus.Active, (await c.Patients.FindAsync(duplicate.Id))!.Status);
        Assert.Null((await c.Patients.FindAsync(duplicate.Id))!.MergedIntoPatientId);
        Assert.Equal(duplicate.Id, c.Orders.Single(o => o.OrderNumber == "ORD-UNDO").PatientId);
        Assert.Equal(duplicate.Id, c.Specimens.Single(s => s.AccessionNumber == "ACC-UNDO").PatientId);
        Assert.Equal(duplicate.Id, (await c.AntibodyHistory.FindAsync(antibodyId))!.PatientId);
        Assert.Equal(duplicate.Id, (await c.DatElutionWorkups.FindAsync(elutionId))!.PatientId);
        Assert.DoesNotContain(
            c.PatientIdentifiers,
            i => i.PatientId == survivor.Id
                 && i.IdentifierType == IdentityTokenType.PriorMedicalRecordNumber
                 && i.Value == duplicate.MedicalRecordNumber
                 && i.IsActive);
        Assert.Equal(
            PatientMergeStatus.Unmerged,
            c.PatientMerges.Single(m => m.DuplicatePatientId == duplicate.Id).Status);
    }

    [Fact]
    public async Task Unmerge_SkippedDuplicateAntibody_StaysOnTheDuplicate()
    {
        await using var c = _factory.Create();
        var (survivor, duplicate) = await SeedPairAsync(c, "MAN-SKIP");
        c.AntibodyHistory.AddRange(
            new AntibodyHistory
            {
                PatientId = survivor.Id,
                AntibodySpecificity = "anti-K",
                Status = AntibodyStatus.Identified,
                IsActive = true
            },
            new AntibodyHistory
            {
                PatientId = duplicate.Id,
                AntibodySpecificity = "anti-K",
                Status = AntibodyStatus.Identified,
                IsActive = true
            });
        await c.SaveChangesAsync();
        var duplicateAntibodyId = c.AntibodyHistory.Single(a => a.PatientId == duplicate.Id).Id;

        Assert.True((await Merge(c).MergeAsync(survivor.Id, duplicate.Id, "Same person.")).Succeeded);
        Assert.Equal(duplicate.Id, (await c.AntibodyHistory.FindAsync(duplicateAntibodyId))!.PatientId);

        var undone = await Merge(c).UnmergeAsync(duplicate.Id, "Not the same person.");
        Assert.True(undone.Succeeded, undone.Error);
        Assert.Equal(duplicate.Id, (await c.AntibodyHistory.FindAsync(duplicateAntibodyId))!.PatientId);
        Assert.Equal(1, c.AntibodyHistory.Count(a => a.PatientId == survivor.Id && a.AntibodySpecificity == "anti-K"));
    }

    [Fact]
    public async Task Unmerge_BlankReason_IsHardStopped()
    {
        await using var c = _factory.Create();
        var (survivor, duplicate) = await SeedPairAsync(c, "MAN-RSN");
        Assert.True((await Merge(c).MergeAsync(survivor.Id, duplicate.Id, "Same person.")).Succeeded);

        var undone = await Merge(c).UnmergeAsync(duplicate.Id, "  ");
        Assert.False(undone.Succeeded);
        Assert.Contains("reason", undone.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(PatientStatus.Merged, (await c.Patients.FindAsync(duplicate.Id))!.Status);
    }

    [Fact]
    public async Task Unmerge_WithoutPatientMerge_IsRejected()
    {
        await using var c = _factory.Create();
        var (survivor, duplicate) = await SeedPairAsync(c, "MAN-UPERM");
        Assert.True((await Merge(c).MergeAsync(survivor.Id, duplicate.Id, "Same person.")).Succeeded);

        var undone = await Merge(c, new FixedPermissionEvaluator(1, PermissionCodes.PatientWrite))
            .UnmergeAsync(duplicate.Id, "Wrong patient.");
        Assert.False(undone.Succeeded);
        Assert.Contains("patient.merge", undone.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(PatientStatus.Merged, (await c.Patients.FindAsync(duplicate.Id))!.Status);
    }

    [Fact]
    public async Task Unmerge_SecondTime_IsHardStopped()
    {
        await using var c = _factory.Create();
        var (survivor, duplicate) = await SeedPairAsync(c, "MAN-TWICE");
        Assert.True((await Merge(c).MergeAsync(survivor.Id, duplicate.Id, "Same person.")).Succeeded);
        Assert.True((await Merge(c).UnmergeAsync(duplicate.Id, "Not the same person.")).Succeeded);

        var again = await Merge(c).UnmergeAsync(duplicate.Id, "Again.");
        Assert.False(again.Succeeded);
        Assert.Contains("already been undone", again.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unmerge_OrderCreatedAfterMerge_IsHardStopped()
    {
        await using var c = _factory.Create();
        var original = _factory.Clock.UtcNow;
        try
        {
            var (survivor, duplicate) = await SeedPairAsync(c, "MAN-ACT");
            Assert.True((await Merge(c).MergeAsync(survivor.Id, duplicate.Id, "Same person.")).Succeeded);

            _factory.Clock.UtcNow = original.AddMinutes(5);
            var location = new OrderingLocation { Code = "ED-ACT", Name = "Emergency" };
            c.OrderingLocations.Add(location);
            await c.SaveChangesAsync();
            var encounter = new Encounter
            {
                PatientId = survivor.Id,
                VisitNumber = "VIS-ACT",
                Status = EncounterStatus.Active,
                AdmitUtc = _factory.Clock.UtcNow
            };
            c.Encounters.Add(encounter);
            await c.SaveChangesAsync();
            c.Orders.Add(new Order
            {
                OrderNumber = "ORD-ACT",
                PatientId = survivor.Id,
                EncounterId = encounter.Id,
                OrderingLocationId = location.Id,
                OrderName = "Type and screen"
            });
            await c.SaveChangesAsync();

            var undone = await Merge(c).UnmergeAsync(duplicate.Id, "Not the same person.");
            Assert.False(undone.Succeeded);
            Assert.Contains("since the merge", undone.Error, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(PatientStatus.Merged, (await c.Patients.FindAsync(duplicate.Id))!.Status);
        }
        finally
        {
            _factory.Clock.UtcNow = original;
        }
    }

    [Fact]
    public async Task Unmerge_WithoutLedger_IsHardStopped()
    {
        await using var c = _factory.Create();
        var (survivor, duplicate) = await SeedPairAsync(c, "MAN-OLD");
        duplicate.Status = PatientStatus.Merged;
        duplicate.MergedIntoPatientId = survivor.Id;
        await c.SaveChangesAsync();

        var undone = await Merge(c).UnmergeAsync(duplicate.Id, "Old merge.");
        Assert.False(undone.Succeeded);
        Assert.Contains("before unmerge was available", undone.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(PatientStatus.Merged, (await c.Patients.FindAsync(duplicate.Id))!.Status);
    }

    [Fact]
    public async Task Unmerge_RestoresDemotedCurrentBloodType()
    {
        await using var c = _factory.Create();
        var (survivor, duplicate) = await SeedPairAsync(c, "MAN-BT");
        c.PatientBloodTypeHistory.AddRange(
            new PatientBloodTypeHistory
            {
                PatientId = survivor.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                IsCurrent = true,
                Source = BloodTypeSource.TestResult
            },
            new PatientBloodTypeHistory
            {
                PatientId = duplicate.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                IsCurrent = true,
                Source = BloodTypeSource.TestResult
            });
        await c.SaveChangesAsync();
        var duplicateTypeId = c.PatientBloodTypeHistory.Single(h => h.PatientId == duplicate.Id).Id;

        Assert.True((await Merge(c).MergeAsync(survivor.Id, duplicate.Id, "Same person.")).Succeeded);
        Assert.False((await c.PatientBloodTypeHistory.FindAsync(duplicateTypeId))!.IsCurrent);

        var undone = await Merge(c).UnmergeAsync(duplicate.Id, "Not the same person.");
        Assert.True(undone.Succeeded, undone.Error);
        var restored = await c.PatientBloodTypeHistory.FindAsync(duplicateTypeId);
        Assert.Equal(duplicate.Id, restored!.PatientId);
        Assert.True(restored.IsCurrent);
    }

    private PatientMergeService Merge(
        BloodBankDbContext c,
        IPermissionEvaluator? permissions = null) =>
        new(
            new EfRepository<Patient>(c),
            new EfRepository<PatientIdentifier>(c),
            new EfRepository<AntibodyHistory>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<Encounter>(c),
            new EfRepository<Order>(c),
            new EfRepository<Specimen>(c),
            new EfRepository<Allocation>(c),
            new EfRepository<Issue>(c),
            new EfRepository<Crossmatch>(c),
            new EfRepository<BloodUnit>(c),
            new EfRepository<SpecialTransfusionRequirement>(c),
            new EfRepository<TransfusionEvent>(c),
            new EfRepository<ReactionInvestigation>(c),
            new EfRepository<AntigenProfile>(c),
            new EfRepository<BillingEvent>(c),
            new EfRepository<TestResult>(c),
            c,
            currentUser: _factory.CurrentUser,
            permissions: permissions,
            workups: new EfRepository<AntibodyIdentificationWorkup>(c),
            datElutions: new EfRepository<DatElutionWorkup>(c),
            ptags: new EfRepository<PtagIdentifier>(c),
            decisions: new EfRepository<BloodComponentCompatibilityDecision>(c),
            mergeLedger: new EfRepository<PatientMerge>(c),
            mergeMoves: new EfRepository<PatientMergeMove>(c),
            clock: _factory.Clock);

    private static async Task<(Patient Survivor, Patient Duplicate)> SeedPairAsync(BloodBankDbContext c, string key)
    {
        var survivor = new Patient
        {
            MedicalRecordNumber = $"MRN-{key}-S",
            LastName = "Survivor",
            FirstName = "Pat",
            DateOfBirth = new DateOnly(1975, 3, 3)
        };
        var duplicate = new Patient
        {
            MedicalRecordNumber = $"MRN-{key}-D",
            LastName = "Duplicate",
            FirstName = "Pat",
            DateOfBirth = new DateOnly(1975, 3, 3)
        };
        c.Patients.AddRange(survivor, duplicate);
        await c.SaveChangesAsync();
        return (survivor, duplicate);
    }
}
