using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Reporting;
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
