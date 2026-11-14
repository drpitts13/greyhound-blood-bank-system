using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Printing;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using BloodBankLIS.Printing;
using BloodBankLIS.Printing.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

/// <summary>
/// Specimen label designs (Cycle 657): named tube and aliquot layouts, each its own size, managed on the admin designer
/// and chosen when printing; aliquot labels print one per aliquot.
/// </summary>
public class SpecimenLabelDesignTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private SpecimenLabelDesignAdminService Admin(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new SpecimenLabelDesignAdminService(new EfRepository<SpecimenLabelDesign>(c), c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env), permissions);
    }

    private PrintService Printing(BloodBankDbContext c) =>
        new(new EfRepository<PrintJob>(c), new EfRepository<Specimen>(c), new EfRepository<Patient>(c),
            new EfRepository<Issue>(c), new EfRepository<BloodUnit>(c), new EfRepository<ProductType>(c),
            new EfRepository<Crossmatch>(c), new EfRepository<PatientBloodTypeHistory>(c),
            c, _factory.Clock, _factory.CurrentUser, new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            new ILabelRenderer[] { new ZplLabelRenderer(), new PreviewLabelRenderer(), new PdfLabelRenderer() },
            layouts: new EfRepository<LabelLayout>(c), designs: new EfRepository<SpecimenLabelDesign>(c));

    private async Task<long> SpecimenAsync(BloodBankDbContext c, string accession = "ACC-LBL-1")
    {
        var patient = new Patient { MedicalRecordNumber = "MRN-LBL", LastName = "Label", FirstName = "Lee", DateOfBirth = new DateOnly(1980, 2, 3), Sex = Sex.Female };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        var specimen = new Specimen
        {
            AccessionNumber = accession, PatientId = patient.Id, SpecimenType = "EDTA", Collector = "RN Smith",
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-1), Status = SpecimenStatus.Accepted
        };
        c.Specimens.Add(specimen);
        await c.SaveChangesAsync();
        return specimen.Id;
    }

    // TEST-BB-821: creating a design: the first of a purpose is the default, a starter or a copy is laid out at its size,
    // codes are unique, sizes are bounded, and the creation is audited with a history snapshot.
    [Fact]
    public async Task Create_StartersCopiesAndRefusals()
    {
        await using var c = _factory.Create();
        var admin = Admin(c);
        var tube = await admin.CreateAsync(new CreateSpecimenLabelDesignRequest("tube-a", "Tube A", SpecimenLabelPurpose.Tube, 2m, 1m));
        Assert.True(tube.Succeeded, tube.Error);
        Assert.Equal(("TUBE-A", true, 406, 203), (tube.Value!.Code, tube.Value.IsDefault, tube.Value.WidthDots, tube.Value.HeightDots));

        var cryo = await admin.CreateAsync(new CreateSpecimenLabelDesignRequest("CRYO", "Cryovial", SpecimenLabelPurpose.Aliquot, 1.5m, 0.5m));
        Assert.True(cryo.Value!.IsDefault);
        Assert.Contains(cryo.Value.Document.Page1, f => f.Key == LabelFields.AliquotNumber);

        var second = await admin.CreateAsync(new CreateSpecimenLabelDesignRequest("TUBE-B", "Tube B", SpecimenLabelPurpose.Tube, 3m, 1m, CopyFromId: cryo.Value.Id));
        Assert.False(second.Value!.IsDefault);
        Assert.DoesNotContain(second.Value.Document.Page1, f => f.Key == LabelFields.AliquotNumber);
        Assert.Equal(609, second.Value.Document.WidthDots);

        Assert.False((await admin.CreateAsync(new CreateSpecimenLabelDesignRequest("TUBE-A", "Again", SpecimenLabelPurpose.Tube, 2m, 1m))).Succeeded);
        Assert.False((await admin.CreateAsync(new CreateSpecimenLabelDesignRequest("BIG", "Too big", SpecimenLabelPurpose.Tube, 5m, 1m))).Succeeded);
        Assert.False((await admin.CreateAsync(new CreateSpecimenLabelDesignRequest("bad code!", "x", SpecimenLabelPurpose.Tube, 2m, 1m))).Succeeded);

        Assert.True(await c.AuditEvents.AnyAsync(a => a.EntityType == "SpecimenLabelDesign" && a.EntityId == tube.Value.Id && a.EventType == AuditEventType.Create));
        Assert.True(await c.ConfigurationChangeHistory.AnyAsync(h => h.EntityType == "SpecimenLabelDesign" && h.EntityId == tube.Value.Id));
    }

    // TEST-BB-822: saving refuses a design without the specimen ID barcode, or a tube design with fewer than two patient
    // identifiers (OCD-080), saves one with warnings (returned, not blocking), keeps elements on the label, bumps the
    // version, and records the before and after.
    [Fact]
    public async Task Save_ChecksTheDesignAndRecordsTheChange()
    {
        await using var c = _factory.Create();
        var admin = Admin(c);
        var design = (await admin.CreateAsync(new CreateSpecimenLabelDesignRequest("SAVE-1", "Save", SpecimenLabelPurpose.Tube, 2m, 1m))).Value!;

        var noBarcode = new LabelLayoutDocument { Page1 = [new() { Key = LabelFields.PatientName }, new() { Key = LabelFields.Mrn }] };
        var refused = await admin.SaveAsync(design.Id, new SaveSpecimenLabelDesignRequest("Save", noBarcode));
        Assert.False(refused.Succeeded);
        Assert.Contains(refused.Evaluation!.HardStops, h => h.Message.Contains("specimen ID as a barcode"));

        var oneId = new LabelLayoutDocument { Page1 = [new() { Key = LabelFields.Accession, Barcode = true, Y = 100 }, new() { Key = LabelFields.PatientName }] };
        var refusedOneId = await admin.SaveAsync(design.Id, new SaveSpecimenLabelDesignRequest("Save", oneId));
        Assert.Contains(refusedOneId.Evaluation!.HardStops, h => h.Message == SpecimenLabelDesignRules.TwoIdentifiersError);

        var thin = new LabelLayoutDocument { Page1 = [new() { Key = LabelFields.Accession, Barcode = true, X = 2000, Y = 20 }, new() { Key = LabelFields.PatientName }, new() { Key = LabelFields.Mrn }] };
        var saved = await admin.SaveAsync(design.Id, new SaveSpecimenLabelDesignRequest("Renamed", thin));
        Assert.True(saved.Succeeded, saved.Error);
        Assert.NotEmpty(saved.Evaluation!.Warnings);
        Assert.Equal(("Renamed", 2), (saved.Value!.Name, saved.Value.Version));
        Assert.Equal(398, saved.Value.Document.Page1[0].X);
        Assert.True(await c.AuditEvents.AnyAsync(a => a.EntityType == "SpecimenLabelDesign" && a.EntityId == design.Id && a.EventType == AuditEventType.Update && a.OldValueJson != null));
    }

    // TEST-BB-823: one default per purpose: making another the default moves it; the default cannot be deactivated, an
    // inactive design cannot become the default, and a read-only user cannot change designs.
    [Fact]
    public async Task Default_AndActive_AndPermission()
    {
        await using var c = _factory.Create();
        var admin = Admin(c);
        var first = (await admin.CreateAsync(new CreateSpecimenLabelDesignRequest("D-1", "One", SpecimenLabelPurpose.Tube, 2m, 1m))).Value!;
        var second = (await admin.CreateAsync(new CreateSpecimenLabelDesignRequest("D-2", "Two", SpecimenLabelPurpose.Tube, 2m, 1m))).Value!;

        Assert.False((await admin.SetActiveAsync(first.Id, false)).Succeeded);
        Assert.True((await admin.MakeDefaultAsync(second.Id)).Succeeded);
        var list = await admin.ListAsync();
        Assert.Equal((false, true), (list.Single(d => d.Code == "D-1").IsDefault, list.Single(d => d.Code == "D-2").IsDefault));
        Assert.True((await admin.SetActiveAsync(first.Id, false)).Succeeded);
        Assert.False((await admin.MakeDefaultAsync(first.Id)).Succeeded);
        Assert.DoesNotContain(await admin.ListAsync(activeOnly: true), d => d.Code == "D-1");

        var readOnly = Admin(c, new FixedPermissionEvaluator(0, PermissionCodes.AdminConfigView));
        Assert.False((await readOnly.CreateAsync(new CreateSpecimenLabelDesignRequest("D-3", "Three", SpecimenLabelPurpose.Tube, 2m, 1m))).Succeeded);
        Assert.False((await readOnly.MakeDefaultAsync(first.Id)).Succeeded);
    }

    // TEST-BB-824: printing uses the default tube design (its code on the job) or the one named; an aliquot design is not
    // used for a tube label, and an unknown or inactive code is refused rather than printed on another design.
    [Fact]
    public async Task TubeLabel_PrintsOnTheChosenDesign()
    {
        await using var c = _factory.Create();
        var specimenId = await SpecimenAsync(c);
        var admin = Admin(c);
        var tube = (await admin.CreateAsync(new CreateSpecimenLabelDesignRequest("T-STD", "Standard", SpecimenLabelPurpose.Tube, 2m, 1m))).Value!;
        var wide = (await admin.CreateAsync(new CreateSpecimenLabelDesignRequest("T-WIDE", "Wide", SpecimenLabelPurpose.Tube, 3m, 1m))).Value!;
        await admin.CreateAsync(new CreateSpecimenLabelDesignRequest("A-STD", "Aliquot", SpecimenLabelPurpose.Aliquot, 1.5m, 0.5m));

        var byDefault = await Printing(c).PrintSpecimenLabelAsync(specimenId, new PrintRequest());
        Assert.True(byDefault.Succeeded, byDefault.Error);
        Assert.Equal("T-STD", byDefault.Value!.TemplateCode);
        Assert.Contains("^PW406", byDefault.Value.RenderedZpl);
        Assert.Contains("ACC-LBL-1", byDefault.Value.RenderedZpl);

        var named = await Printing(c).PrintSpecimenLabelAsync(specimenId, new PrintRequest(TemplateCode: "t-wide"));
        Assert.Equal(("T-WIDE", true), (named.Value!.TemplateCode, named.Value.RenderedZpl!.Contains("^PW609")));

        Assert.Contains("aliquot label design", (await Printing(c).PrintSpecimenLabelAsync(specimenId, new PrintRequest(TemplateCode: "A-STD"))).Error);
        Assert.False((await Printing(c).PrintSpecimenLabelAsync(specimenId, new PrintRequest(TemplateCode: "NOPE"))).Succeeded);
        await admin.SetActiveAsync(wide.Id, false);
        Assert.False((await Printing(c).PrintSpecimenLabelAsync(specimenId, new PrintRequest(TemplateCode: "T-WIDE"))).Succeeded);
        Assert.Equal("T-STD", tube.Code);
    }

    // TEST-BB-852: a tube design saved before two identifiers were required (OCD-080) does not print new tube labels and
    // says how to fix it; nothing is queued. The designer lists the error on it.
    [Fact]
    public async Task TubeLabel_RefusedOnADesignWithOneIdentifier()
    {
        await using var c = _factory.Create();
        var specimenId = await SpecimenAsync(c);
        var oneId = new LabelLayoutDocument
        {
            WidthDots = 406, HeightDots = 203,
            Page1 = [new() { Key = LabelFields.Accession, Barcode = true, X = 10, Y = 100 }, new() { Key = LabelFields.PatientName, X = 10, Y = 10 }]
        };
        c.SpecimenLabelDesigns.Add(new SpecimenLabelDesign
        {
            Code = "OLD-ONE", Name = "Old one identifier", Purpose = SpecimenLabelPurpose.Tube, WidthDots = 406, HeightDots = 203,
            DocumentJson = System.Text.Json.JsonSerializer.Serialize(oneId, LabelFields.JsonOptions),
            IsDefault = true, IsActive = true, Version = 1, CreatedBy = "tech-test", CreatedUtc = _factory.Clock.UtcNow
        });
        await c.SaveChangesAsync();

        var printed = await Printing(c).PrintSpecimenLabelAsync(specimenId, new PrintRequest());
        Assert.False(printed.Succeeded);
        Assert.Contains("fewer than two patient identifiers", printed.Error);
        Assert.Contains("Admin › Specimen labels", printed.Error);
        Assert.False(await c.PrintJobs.AnyAsync());
        var designId = (await Admin(c).ListAsync()).Single(d => d.Code == "OLD-ONE").Id;
        Assert.Contains(SpecimenLabelDesignRules.TwoIdentifiersError, (await Admin(c).GetAsync(designId))!.Warnings);
    }

    // TEST-BB-825: aliquot labels print one job per aliquot, each "Aliquot n of N" with the parent specimen ID as its
    // barcode and the design's bar width; the count is bounded, and a tube design or a missing default is refused. A
    // reprint uses the design the label was printed with.
    [Fact]
    public async Task AliquotLabels_OnePerAliquot()
    {
        await using var c = _factory.Create();
        var specimenId = await SpecimenAsync(c, "ACC-ALQ-9");

        var none = await Printing(c).PrintSpecimenAliquotLabelsAsync(specimenId, 2, new PrintRequest());
        Assert.Contains("No default aliquot label design", none.Error);

        var admin = Admin(c);
        await admin.CreateAsync(new CreateSpecimenLabelDesignRequest("T-1", "Tube", SpecimenLabelPurpose.Tube, 2m, 1m));
        await admin.CreateAsync(new CreateSpecimenLabelDesignRequest("A-1", "Aliquot", SpecimenLabelPurpose.Aliquot, 1.5m, 0.5m));

        var jobs = await Printing(c).PrintSpecimenAliquotLabelsAsync(specimenId, 3, new PrintRequest());
        Assert.True(jobs.Succeeded, jobs.Error);
        Assert.Equal(3, jobs.Value!.Count);
        Assert.All(jobs.Value, j => Assert.Equal(("A-1", PrintJobType.SpecimenLabel), (j.TemplateCode, j.JobType)));
        Assert.Contains("Aliquot 2 of 3", jobs.Value[1].RenderedZpl);
        Assert.Contains("^BY1", jobs.Value[1].RenderedZpl);
        Assert.Contains("^PW305", jobs.Value[1].RenderedZpl);
        Assert.Contains("ACC-ALQ-9", jobs.Value[1].RenderedZpl);
        Assert.Equal(3, await c.AuditEvents.CountAsync(a => a.EventType == AuditEventType.Print && a.EntityType == nameof(PrintJob)
            && jobs.Value.Select(j => (long?)j.Id).Contains(a.EntityId)));

        Assert.False((await Printing(c).PrintSpecimenAliquotLabelsAsync(specimenId, 0, new PrintRequest())).Succeeded);
        Assert.False((await Printing(c).PrintSpecimenAliquotLabelsAsync(specimenId, 51, new PrintRequest())).Succeeded);
        Assert.Contains("tube label design", (await Printing(c).PrintSpecimenAliquotLabelsAsync(specimenId, 2, new PrintRequest(TemplateCode: "T-1"))).Error);

        var reprint = await Printing(c).ReprintAsync(jobs.Value[1].Id, "Label smudged");
        Assert.True(reprint.Succeeded, reprint.Error);
        Assert.Contains("Aliquot 2 of 3", reprint.Value!.RenderedZpl);
        Assert.Contains("^PW305", reprint.Value.RenderedZpl);
    }

    // TEST-BB-826: the seed adds a default tube design (the saved single layout when there is one) and a default aliquot
    // design, once.
    [Fact]
    public async Task Seed_AddsTheStartingDesignsOnce()
    {
        await using (var c = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(c);
        }

        await using (var c = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(c);
        }

        await using var verify = _factory.Create();
        var designs = await verify.SpecimenLabelDesigns.ToListAsync();
        Assert.Equal(["ALIQ-STD", "TUBE-STD"], designs.Select(d => d.Code).Order());
        Assert.All(designs, d => Assert.True(d.IsDefault && d.IsActive));
        Assert.Equal((305, 102), (designs.Single(d => d.Code == "ALIQ-STD").WidthDots, designs.Single(d => d.Code == "ALIQ-STD").HeightDots));
    }
}
