using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using BloodBankLIS.Printing;
using BloodBankLIS.Printing.Rendering;
using BloodBankLIS.Printing.Templates;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class Phase6PrintingTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public Phase6PrintingTests(SqliteContextFactory factory) => _factory = factory;

    private PrintService Printing(BloodBankDbContext c) =>
        new(new EfRepository<PrintJob>(c), new EfRepository<Specimen>(c), new EfRepository<Patient>(c),
            new EfRepository<Issue>(c), new EfRepository<BloodUnit>(c), new EfRepository<ProductType>(c),
            new EfRepository<Crossmatch>(c), new EfRepository<PatientBloodTypeHistory>(c),
            c, _factory.Clock, _factory.CurrentUser, new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            new ILabelRenderer[] { new ZplLabelRenderer(), new PreviewLabelRenderer() });

    private async Task<long> CreatePatientAsync(BloodBankDbContext c, string mrn)
    {
        var patient = new Patient
        {
            MedicalRecordNumber = mrn,
            LastName = "Print",
            FirstName = "Test",
            DateOfBirth = new DateOnly(1985, 3, 4),
            Sex = Sex.Male
        };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        return patient.Id;
    }

    [Fact]
    public async Task PrintSpecimenLabel_CreatesPrintedJobWithZplAndPayload()
    {
        long specimenId;
        await using (var setup = _factory.Create())
        {
            var patientId = await CreatePatientAsync(setup, "PRT-100");
            var specimen = new Specimen
            {
                AccessionNumber = "ACC-PRT-1",
                PatientId = patientId,
                SpecimenType = "EDTA",
                CollectedUtc = _factory.Clock.UtcNow.AddHours(-1),
                Status = SpecimenStatus.Accepted
            };
            setup.Specimens.Add(specimen);
            await setup.SaveChangesAsync();
            specimenId = specimen.Id;
        }

        await using var context = _factory.Create();
        var result = await Printing(context).PrintSpecimenLabelAsync(specimenId, new PrintRequest());

        Assert.True(result.Succeeded);
        var job = result.Value!;
        Assert.Equal(PrintJobType.SpecimenLabel, job.JobType);
        Assert.Equal(PrintJobStatus.Printed, job.Status);
        Assert.False(job.IsReprint);
        Assert.StartsWith("^XA", job.RenderedZpl);
        Assert.Contains("ACC-PRT-1", job.RenderedZpl);
        Assert.Contains("ACC-PRT-1", job.PayloadJson);
    }

    [Fact]
    public async Task PrintSpecimenLabel_PreviewFormat_RendersHumanReadableProof()
    {
        long specimenId;
        await using (var setup = _factory.Create())
        {
            var patientId = await CreatePatientAsync(setup, "PRT-150");
            var specimen = new Specimen
            {
                AccessionNumber = "ACC-PRT-150",
                PatientId = patientId,
                SpecimenType = "Serum",
                CollectedUtc = _factory.Clock.UtcNow.AddHours(-1),
                Status = SpecimenStatus.Accepted
            };
            setup.Specimens.Add(specimen);
            await setup.SaveChangesAsync();
            specimenId = specimen.Id;
        }

        await using var context = _factory.Create();
        var result = await Printing(context).PrintSpecimenLabelAsync(specimenId, new PrintRequest(LabelFormat.Preview));

        Assert.True(result.Succeeded);
        Assert.Equal(LabelFormat.Preview, result.Value!.Format);
        Assert.Contains("LABEL", result.Value.RenderedZpl);
        Assert.DoesNotContain("^XA", result.Value.RenderedZpl);
    }

    [Fact]
    public async Task PrintSpecimenLabel_MissingSpecimen_Fails()
    {
        await using var context = _factory.Create();
        var result = await Printing(context).PrintSpecimenLabelAsync(999999, new PrintRequest());

        Assert.False(result.Succeeded);
        Assert.Contains("not found", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PrintCompatibilityTag_BuildsPtagFromIssueRecord()
    {
        long issueId;
        await using (var setup = _factory.Create())
        {
            var patientId = await CreatePatientAsync(setup, "PRT-200");
            var (unitId, _) = await CreateUnitAsync(setup, "W-PRT-200");
            var issue = new Issue
            {
                BloodProductId = unitId,
                PatientId = patientId,
                IssuedUtc = _factory.Clock.UtcNow,
                IssuedBy = "tech",
                IssueType = IssueType.Standard,
                Status = IssueStatus.Issued
            };
            setup.Issues.Add(issue);
            await setup.SaveChangesAsync();
            issueId = issue.Id;
        }

        await using var context = _factory.Create();
        var result = await Printing(context).PrintCompatibilityTagAsync(issueId, new PrintRequest());

        Assert.True(result.Succeeded);
        Assert.Equal(PrintJobType.CompatibilityTag, result.Value!.JobType);
        Assert.Contains("W-PRT-200", result.Value.RenderedZpl);
        Assert.Equal(nameof(Issue), result.Value.ContextType);
    }

    [Fact]
    public async Task PrintComponentLabel_IncludesDinAndProductAndSupportsReprint()
    {
        long unitId;
        await using (var setup = _factory.Create())
        {
            var (id, _) = await CreateUnitAsync(setup, "U-ISBT-1");
            var unit = await setup.BloodUnits.FindAsync(id);
            unit!.Din = "W000011234567";
            unit.ProductCodeData = "E0206V00";
            unit.AboRhdCode = "62";
            await setup.SaveChangesAsync();
            unitId = id;
        }

        long jobId;
        await using (var context = _factory.Create())
        {
            var result = await Printing(context).PrintComponentLabelAsync(unitId, new PrintRequest());
            Assert.True(result.Succeeded);
            Assert.Equal(PrintJobType.ProductLabel, result.Value!.JobType);
            Assert.Equal(ComponentLabelTemplate.TemplateCode, result.Value.TemplateCode);
            Assert.Contains("W000011234567", result.Value.RenderedZpl);
            Assert.Contains("E0206V00", result.Value.RenderedZpl);
            Assert.Equal(nameof(BloodUnit), result.Value.ContextType);
            jobId = result.Value.Id;
        }

        await using var reprintCtx = _factory.Create();
        var reprint = await Printing(reprintCtx).ReprintAsync(jobId, "Torn bag label");
        Assert.True(reprint.Succeeded);
        Assert.True(reprint.Value!.IsReprint);
        Assert.Contains("W000011234567", reprint.Value.RenderedZpl);
    }

    [Fact]
    public async Task Reprint_WithoutReason_IsRejected()
    {
        long jobId = await PrintASpecimenLabelAsync("PRT-300", "ACC-PRT-300");

        await using var context = _factory.Create();
        var result = await Printing(context).ReprintAsync(jobId, "   ");

        Assert.False(result.Succeeded);
        Assert.Contains("reason", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reprint_WithReason_CreatesReprintJobAndAuditEvent()
    {
        long jobId = await PrintASpecimenLabelAsync("PRT-400", "ACC-PRT-400");

        await using (var context = _factory.Create())
        {
            var result = await Printing(context).ReprintAsync(jobId, "Label jammed in printer");
            Assert.True(result.Succeeded);
            Assert.True(result.Value!.IsReprint);
            Assert.Equal("Label jammed in printer", result.Value.ReprintReason);
            Assert.NotEqual(jobId, result.Value.Id);
        }

        await using var verify = _factory.Create();
        var reprintEvent = await verify.AuditEvents.FirstOrDefaultAsync(
            e => e.EntityType == nameof(PrintJob) && e.EventType == AuditEventType.Reprint && e.EntityId == jobId);
        Assert.NotNull(reprintEvent);
        Assert.Contains("jammed", reprintEvent!.Reason!);
    }

    private async Task<long> PrintASpecimenLabelAsync(string mrn, string accession)
    {
        long specimenId;
        await using (var setup = _factory.Create())
        {
            var patientId = await CreatePatientAsync(setup, mrn);
            var specimen = new Specimen
            {
                AccessionNumber = accession,
                PatientId = patientId,
                SpecimenType = "EDTA",
                CollectedUtc = _factory.Clock.UtcNow.AddHours(-1),
                Status = SpecimenStatus.Accepted
            };
            setup.Specimens.Add(specimen);
            await setup.SaveChangesAsync();
            specimenId = specimen.Id;
        }

        await using var context = _factory.Create();
        var result = await Printing(context).PrintSpecimenLabelAsync(specimenId, new PrintRequest());
        Assert.True(result.Succeeded);
        return result.Value!.Id;
    }

    private static async Task<(long unitId, long productTypeId)> CreateUnitAsync(BloodBankDbContext c, string unitNumber)
    {
        var product = new ProductType
        {
            ProductCode = $"PC-{unitNumber}",
            Name = "Red Blood Cells",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        c.ProductTypes.Add(product);
        await c.SaveChangesAsync();

        var unit = new BloodUnit
        {
            UnitNumber = unitNumber,
            ProductTypeId = product.Id,
            Abo = AboGroup.O,
            RhD = RhType.Negative,
            ExpiresUtc = DateTime.UtcNow.AddDays(20),
            Status = UnitStatus.Available
        };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();
        return (unit.Id, product.Id);
    }
}
