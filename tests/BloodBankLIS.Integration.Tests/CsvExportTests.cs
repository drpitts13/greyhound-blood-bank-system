using System.Text;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Audit;
using BloodBankLIS.Application.Reporting;
using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Reporting;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using BloodBankLIS.Infrastructure.Reporting;
using BloodBankLIS.Printing.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-300: CSV export from the report service and of the audit trail.
public class CsvExportTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task ReportExport_Csv_HasTheGridColumnsRows_AndIsAudited()
    {
        await using var db = _factory.Create();
        var product = new ProductType { ProductCode = "RBC-CSV", Name = "Red cells, \"leukoreduced\"" };
        db.ProductTypes.Add(product);
        await db.SaveChangesAsync();
        db.BloodUnits.Add(new BloodUnit
        {
            UnitNumber = "U-CSV-1",
            ProductTypeId = product.Id,
            Abo = AboGroup.A,
            RhD = RhType.Negative,
            ExpiresUtc = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            Status = UnitStatus.Discarded,
            DiscardReasonCode = "OTHER",
            DiscardReason = "=CMD() typed by mistake"
        });
        db.ReportDefinitions.AddRange(ReportSeedCatalog.Create());
        await db.SaveChangesAsync();
        var discards = await db.ReportDefinitions.SingleAsync(r => r.Code == "RPT-DISCARDS");

        var service = Service(db);
        var file = await service.ExportAsync(discards.Id, "CSV", null);

        Assert.True(file.Succeeded, file.Error);
        Assert.Equal(CsvFormat.ContentType, file.Value!.ContentType);
        Assert.Equal("RPT-DISCARDS.csv", file.Value.FileName);
        var lines = Text(file.Value.Bytes).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("unit_number,product_name,abo,rh,status,discard_reason_code,discard_reason,discarded_utc,expires_utc", lines[0]);
        Assert.StartsWith("U-CSV-1,\"Red cells, \"\"leukoreduced\"\"\",A,Negative,Discarded,OTHER,'=CMD() typed by mistake,", lines[1]);
        Assert.True(await db.AuditEvents.AnyAsync(e => e.EventType == AuditEventType.Export && e.EntityId == discards.Id));

        var bad = await service.ExportAsync(discards.Id, "txt", null);
        Assert.False(bad.Succeeded);
        Assert.Contains("csv", bad.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuditCsv_KeepsJsonAndHashes_AndGuardsFormulas()
    {
        await using var db = _factory.Create();
        var writer = new AuditWriter(db, _factory.Clock, _factory.CurrentUser);
        writer.Record(AuditEventType.Update, "BloodUnit", 7, new { Status = "Available" }, new { Status = "Discarded", Note = "a,b" }, "@risky reason");
        await db.SaveChangesAsync();
        var events = await db.AuditEvents.AsNoTracking().Where(e => e.EntityType == "BloodUnit" && e.EntityId == 7).ToListAsync();

        var lines = Text(AuditCsvExport.Write(events)).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(string.Join(",", AuditCsvExport.Columns), lines[0]);
        var row = lines[1];
        Assert.Contains(",Update,BloodUnit,7,tech-test,", row);
        Assert.Contains(",'@risky reason,", row);
        Assert.Contains("\"{\"\"Status\"\":\"\"Discarded\"\",\"\"Note\"\":\"\"a,b\"\"}\"", row);
        var hash = events.Single().RecordHash;
        Assert.False(string.IsNullOrEmpty(hash));
        Assert.EndsWith("," + hash, row);
    }

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

    private ReportService Service(BloodBankDbContext db) =>
        new(
            new ReportDefinitionStore(db),
            new EfReportSourceReader(db),
            new AdoReportSqlExecutor(db),
            new TabularReportRenderer(),
            new AuditWriter(db, _factory.Clock, _factory.CurrentUser),
            db,
            _factory.CurrentUser,
            new AllowAll());

    private sealed class AllowAll : IPermissionEvaluator
    {
        public Task<IReadOnlySet<string>> GetPermissionsAsync(string userName, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

        public Task<bool> HasPermissionAsync(string userName, string permissionCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<int> GetMaxSecurityLevelAsync(string userName, CancellationToken cancellationToken = default) =>
            Task.FromResult(100);
    }
}
