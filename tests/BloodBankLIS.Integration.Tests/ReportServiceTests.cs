using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Reporting;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Reporting;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Reporting;
using BloodBankLIS.Printing.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class ReportServiceTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task SeededReports_Exist_Run_AndCannotBeDeleted()
    {
        await using var db = _factory.Create();
        db.ReportDefinitions.AddRange(ReportSeedCatalog.Create());
        await db.SaveChangesAsync();

        var service = Service(db);
        var list = await service.ListAsync();
        Assert.True(list.Succeeded);
        Assert.Equal(8, list.Value!.Count);
        Assert.All(list.Value, report => Assert.True(report.IsSystemOwned));

        foreach (var report in list.Value)
        {
            var run = await service.RunAsync(report.Id, new Dictionary<string, string?>());
            Assert.True(run.Succeeded, run.Error);
        }

        var blocked = await service.DeleteAsync(list.Value[0].Id);
        Assert.False(blocked.Succeeded);
        Assert.Contains("cannot be deleted", blocked.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SqlReport_ReadsOnlyTheReportingView()
    {
        await using var db = _factory.Create();
        ReportViewSql.Apply(sql => db.Database.ExecuteSqlRaw(sql), db.Database.ProviderName ?? string.Empty);

        var service = Service(db);
        var saved = await service.SaveAsync(null, new SaveReportRequest
        {
            Code = "RPT-SQL-INV",
            Name = "SQL inventory",
            Kind = Domain.Reporting.ReportKind.Sql,
            SqlText = "SELECT unit_number, product_name FROM rpt_inventory_on_hand"
        });
        Assert.True(saved.Succeeded, saved.Error);

        var run = await service.RunAsync(saved.Value!.Id, null);
        Assert.True(run.Succeeded, run.Error);
        Assert.Contains("unit_number", run.Value!.Columns, StringComparer.OrdinalIgnoreCase);

        var file = await service.ExportAsync(saved.Value.Id, "pdf", null);
        Assert.True(file.Succeeded, file.Error);
        Assert.Equal("application/pdf", file.Value!.ContentType);
        Assert.True(file.Value.Bytes.Length > 4);

        var excel = await service.ExportAsync(saved.Value.Id, "xlsx", null);
        Assert.True(excel.Succeeded, excel.Error);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excel.Value!.ContentType);
        Assert.Equal((byte)'P', excel.Value.Bytes[0]);
    }

    [Fact]
    public async Task LabResultsSource_RunsVisualAndViewColumns()
    {
        await using var db = _factory.Create();
        var entered = new DateTime(2026, 9, 22, 15, 4, 0, DateTimeKind.Utc);
        var patient = new Patient
        {
            MedicalRecordNumber = "MRN-LAB",
            LastName = "Grey",
            FirstName = "Hound",
            DateOfBirth = new DateOnly(2014, 3, 8),
            Sex = Sex.Female,
            Status = PatientStatus.Active
        };
        db.Patients.Add(patient);
        db.TestDefinitions.Add(new TestDefinition
        {
            Code = "ABORH",
            Name = "Retired type",
            Category = TestCategory.Other,
            IsActive = false,
            IsDraft = false
        });
        db.TestDefinitions.Add(new TestDefinition
        {
            Code = "ABORH",
            Name = "ABO/Rh",
            Category = TestCategory.AboRh,
            IsActive = true,
            IsDraft = false
        });
        await db.SaveChangesAsync();

        var specimen = new Specimen
        {
            AccessionNumber = "ACC-LAB",
            PatientId = patient.Id,
            SpecimenType = "EDTA",
            CollectedUtc = entered.AddHours(-1),
            ReceivedUtc = entered,
            Status = SpecimenStatus.Accepted
        };
        db.Specimens.Add(specimen);
        await db.SaveChangesAsync();

        db.TestResults.Add(new TestResult
        {
            SpecimenId = specimen.Id,
            PatientId = patient.Id,
            TestCode = "ABORH",
            Value = "O Pos",
            Units = null,
            Interpretation = "Compatible",
            Status = ResultStatus.Verified,
            EnteredUtc = entered,
            VerifiedUtc = entered.AddMinutes(12)
        });
        await db.SaveChangesAsync();
        ReportViewSql.Apply(sql => db.Database.ExecuteSqlRaw(sql), db.Database.ProviderName ?? string.Empty);

        var service = Service(db);
        var source = service.Sources().Single(item => item.Code == ReportSourceCatalog.LabResults);
        Assert.Equal(ReportViewCatalog.LabResults, source.ViewName);
        Assert.Contains(source.Fields, field => field.Code == "mrn");
        Assert.Contains(source.Fields, field => field.Code == "accession_number");
        Assert.Contains(source.Fields, field => field.Code == "test_name");
        Assert.Contains(source.Fields, field => field.Code == "result_value");

        var preview = await service.PreviewAsync(new PreviewReportRequest
        {
            Name = "Lab preview",
            Kind = ReportKind.Visual,
            Layout = new ReportLayout
            {
                SourceCode = ReportSourceCatalog.LabResults,
                Columns = ["mrn", "patient_name", "date_of_birth", "sex", "accession_number", "test_code", "test_name", "test_category", "result_value", "result_status"]
            }
        });
        Assert.True(preview.Succeeded, preview.Error);
        Assert.Equal(
            ["mrn", "patient_name", "date_of_birth", "sex", "accession_number", "test_code", "test_name", "test_category", "result_value", "result_status"],
            preview.Value!.Columns);
        var row = Assert.Single(preview.Value.Rows);
        Assert.Equal("MRN-LAB", row[0]);
        Assert.Equal("Grey, Hound", row[1]);
        Assert.Equal("2014-03-08", row[2]);
        Assert.Equal("Female", row[3]);
        Assert.Equal("ACC-LAB", row[4]);
        Assert.Equal("ABORH", row[5]);
        Assert.Equal("ABO/Rh", row[6]);
        Assert.Equal("AboRh", row[7]);
        Assert.Equal("O Pos", row[8]);
        Assert.Equal("Verified", row[9]);

        var saved = await service.SaveAsync(null, new SaveReportRequest
        {
            Code = "RPT-LAB-SQL",
            Name = "SQL lab results",
            Kind = ReportKind.Sql,
            SqlText = "SELECT mrn, patient_name, date_of_birth, test_name, test_category, result_value FROM rpt_lab_results"
        });
        Assert.True(saved.Succeeded, saved.Error);
        var run = await service.RunAsync(saved.Value!.Id, null);
        Assert.True(run.Succeeded, run.Error);
        var sqlRow = Assert.Single(run.Value!.Rows);
        Assert.Equal("MRN-LAB", sqlRow[0]);
        Assert.Equal("Grey, Hound", sqlRow[1]);
        Assert.Equal("2014-03-08", sqlRow[2]);
        Assert.Equal("ABO/Rh", sqlRow[3]);
        Assert.Equal("AboRh", sqlRow[4]);
        Assert.Equal("O Pos", sqlRow[5]);
    }

    private ReportService Service(Infrastructure.Persistence.BloodBankDbContext db) =>
        new(
            new ReportDefinitionStore(db),
            new EfReportSourceReader(db),
            new AdoReportSqlExecutor(db),
            new TabularReportRenderer(),
            new AuditWriter(db, _factory.Clock, _factory.CurrentUser),
            db,
            _factory.CurrentUser,
            new AllowReports());

    private sealed class AllowReports : IPermissionEvaluator
    {
        public Task<IReadOnlySet<string>> GetPermissionsAsync(string userName, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal)
            {
                PermissionCodes.ReportRun,
                PermissionCodes.ReportDesign
            });

        public Task<bool> HasPermissionAsync(string userName, string permissionCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(permissionCode is PermissionCodes.ReportRun or PermissionCodes.ReportDesign);

        public Task<int> GetMaxSecurityLevelAsync(string userName, CancellationToken cancellationToken = default) =>
            Task.FromResult(3);
    }
}
