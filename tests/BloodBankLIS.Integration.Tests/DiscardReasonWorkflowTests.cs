using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Reporting;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Reporting;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using BloodBankLIS.Infrastructure.Reporting;
using BloodBankLIS.Printing.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-297: discard-reason catalog administration and the wastage report.
public class DiscardReasonWorkflowTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private UnitDiscardReasonAdminService Admin(BloodBankDbContext context)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new UnitDiscardReasonAdminService(
            new EfRepository<UnitDiscardReason>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(context, _factory.Clock, _factory.CurrentUser, env));
    }

    [Fact]
    public async Task Catalog_NormalizesCode_RejectsDuplicates_AndKeepsTheCommentFlag()
    {
        await using var context = _factory.Create();
        var admin = Admin(context);

        var created = await admin.CreateAsync(new SaveUnitDiscardReasonRequest(" other ", "Other", 90, RequiresComment: true));
        Assert.True(created.Succeeded);
        Assert.Equal("OTHER", created.Value!.Code);
        Assert.True(created.Value.RequiresComment);

        var duplicate = await admin.CreateAsync(new SaveUnitDiscardReasonRequest("OTHER", "Something else", 91));
        Assert.False(duplicate.Succeeded);
        Assert.Contains(duplicate.Evaluation!.HardStops, r => r.Code == UnitDiscardReasonValidator.DuplicateCode);

        var updated = await admin.UpdateAsync(created.Value.Id, new SaveUnitDiscardReasonRequest("OTHER", "Other reason", 90, RequiresComment: false));
        Assert.True(updated.Succeeded);
        Assert.False(updated.Value!.RequiresComment);

        var inactive = await admin.SetActiveAsync(created.Value.Id, false);
        Assert.True(inactive.Succeeded);
        Assert.DoesNotContain(await admin.ListAsync(includeInactive: false), r => r.Code == "OTHER");
        Assert.True(await context.ConfigurationChangeHistory.AnyAsync(h => h.EntityType == nameof(UnitDiscardReason)));
    }

    [Fact]
    public async Task WastageReport_CountsDiscardsByMonthProductAndCode()
    {
        await using var db = _factory.Create();
        var product = new ProductType { ProductCode = "RBC-W", Name = "Red cells" };
        db.ProductTypes.Add(product);
        await db.SaveChangesAsync();

        var may = new DateTime(2026, 5, 10, 9, 0, 0, DateTimeKind.Utc);
        var april = new DateTime(2026, 4, 2, 9, 0, 0, DateTimeKind.Utc);
        AddDiscard(db, product.Id, "U-W1", "EXPIRED", may);
        AddDiscard(db, product.Id, "U-W2", "EXPIRED", may.AddDays(3));
        AddDiscard(db, product.Id, "U-W3", "BAG", may);
        AddDiscard(db, product.Id, "U-W4", "EXPIRED", april);
        db.BloodUnits.Add(Unit(product.Id, "U-W5", UnitStatus.Available, null));
        await db.SaveChangesAsync();
        ReportViewSql.Apply(sql => db.Database.ExecuteSqlRaw(sql), db.Database.ProviderName ?? string.Empty);

        db.ReportDefinitions.AddRange(ReportSeedCatalog.Create());
        await db.SaveChangesAsync();
        var wastage = await db.ReportDefinitions.SingleAsync(r => r.Code == "RPT-WASTAGE");
        var service = Service(db);

        var run = await service.RunAsync(wastage.Id, new Dictionary<string, string?>());
        Assert.True(run.Succeeded, run.Error);
        Assert.Equal(["discarded_month", "product_name", "discard_reason_code", "units"], run.Value!.Columns);
        var rows = run.Value.Rows.Select(r => string.Join("|", r)).ToList();
        Assert.Equal(["2026-05|Red cells|BAG|1", "2026-05|Red cells|EXPIRED|2", "2026-04|Red cells|EXPIRED|1"], rows);

        // The guarded-SQL view exposes the same columns as the visual source.
        var view = await service.PreviewAsync(new PreviewReportRequest
        {
            Name = "Discard view",
            Kind = ReportKind.Sql,
            SqlText = "SELECT unit_number, discard_reason_code, discarded_month FROM rpt_discards ORDER BY unit_number"
        });
        Assert.True(view.Succeeded, view.Error);
        Assert.Equal(["U-W1", "EXPIRED", "2026-05"], view.Value!.Rows[0]);
        Assert.Equal(["U-W4", "EXPIRED", "2026-04"], view.Value.Rows[3]);
    }

    private static void AddDiscard(BloodBankDbContext db, long productTypeId, string number, string code, DateTime whenUtc)
    {
        var unit = Unit(productTypeId, number, UnitStatus.Discarded, code);
        db.BloodUnits.Add(unit);
        db.SaveChanges();
        db.InventoryStatusHistory.Add(new InventoryStatusHistory
        {
            BloodProductId = unit.Id,
            FromStatus = UnitStatus.Available,
            ToStatus = UnitStatus.Discarded,
            Reason = code,
            ChangedBy = "tech-test",
            ChangedUtc = whenUtc
        });
    }

    private static BloodUnit Unit(long productTypeId, string number, UnitStatus status, string? code) => new()
    {
        UnitNumber = number,
        ProductTypeId = productTypeId,
        Abo = AboGroup.O,
        RhD = RhType.Positive,
        ExpiresUtc = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        Status = status,
        DiscardReasonCode = code,
        DiscardReason = code is null ? null : $"{code} text"
    };

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

    private sealed class AllowAll : Application.Abstractions.IPermissionEvaluator
    {
        public Task<IReadOnlySet<string>> GetPermissionsAsync(string userName, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

        public Task<bool> HasPermissionAsync(string userName, string permissionCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<int> GetMaxSecurityLevelAsync(string userName, CancellationToken cancellationToken = default) =>
            Task.FromResult(100);
    }
}
