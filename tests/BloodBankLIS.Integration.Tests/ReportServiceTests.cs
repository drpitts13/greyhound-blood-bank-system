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
        Assert.Equal(15, list.Value!.Count);
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

    // TEST-BB-335: rejections are counted by month, reason code, draw location, and collector.
    [Fact]
    public async Task RejectionsByCause_GroupsRejectedSpecimens_AndSkipsOthers()
    {
        await using var db = _factory.Create();
        db.ReportDefinitions.AddRange(ReportSeedCatalog.Create());
        var patient = new Patient { MedicalRecordNumber = "MRN-RPT-REJ", LastName = "Report", FirstName = "Reject", DateOfBirth = new DateOnly(1970, 1, 1), Sex = Sex.Unknown };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var rejectedAt = new DateTime(2026, 10, 15, 8, 0, 0, DateTimeKind.Utc);
        Specimen Make(string accession, SpecimenStatus status, string? code, string location, string collector, DateTime? rejected) => new()
        {
            AccessionNumber = accession,
            PatientId = patient.Id,
            SpecimenType = "EDTA",
            CollectedUtc = rejectedAt.AddHours(-2),
            DrawLocation = location,
            Collector = collector,
            Status = status,
            RejectionReasonCode = code,
            RejectionReason = code is null ? (status == SpecimenStatus.Rejected ? "legacy free text" : null) : code + " text",
            RejectedUtc = rejected,
            RejectedBy = rejected is null ? null : "tech-test"
        };
        db.Specimens.AddRange(
            Make("RPT-REJ-1", SpecimenStatus.Rejected, "WBIT", "4 West", "RN Lee", rejectedAt),
            Make("RPT-REJ-2", SpecimenStatus.Rejected, "WBIT", "4 West", "RN Lee", rejectedAt.AddDays(1)),
            Make("RPT-REJ-3", SpecimenStatus.Rejected, "HEMOLYZED", "ED", "RN Diaz", rejectedAt),
            Make("RPT-REJ-4", SpecimenStatus.Rejected, null, "ED", "RN Diaz", rejectedAt),
            Make("RPT-OK-5", SpecimenStatus.Accepted, null, "4 West", "RN Lee", null));
        await db.SaveChangesAsync();

        var service = Service(db);
        var list = await service.ListAsync();
        var byCause = list.Value!.Single(r => r.Code == "RPT-REJECTIONS-BY-CAUSE");
        var run = await service.RunAsync(byCause.Id, new Dictionary<string, string?>());
        Assert.True(run.Succeeded, run.Error);
        var rows = run.Value!.Rows;
        Assert.Equal(3, rows.Count);
        Assert.Contains(rows, r => r.Contains("2026-10") && r.Contains("WBIT") && r.Contains("4 West") && r.Contains("RN Lee") && r.Contains("2"));
        Assert.Contains(rows, r => r.Contains("HEMOLYZED") && r.Contains("ED") && r.Contains("1"));
        Assert.Contains(rows, r => r.Contains("UNCODED") && r.Contains("ED") && r.Contains("1"));

        var detail = list.Value!.Single(r => r.Code == "RPT-SPECIMEN-REJECTIONS");
        var detailRun = await service.RunAsync(detail.Id, new Dictionary<string, string?>());
        Assert.True(detailRun.Succeeded, detailRun.Error);
        Assert.Equal(4, detailRun.Value!.Rows.Count);
        Assert.DoesNotContain(detailRun.Value.Rows, r => r.Contains("RPT-OK-5"));
    }

    // TEST-BB-477: turnaround counts verified current results on an order (not superseded or unverified ones), in
    // minutes from order and from receipt; the by-priority report groups them with the average and longest; the
    // SQL view gives the same minutes.
    [Fact]
    public async Task TestTurnaround_MinutesFromOrderAndReceipt_ByPriority()
    {
        await using var db = _factory.Create();
        db.ReportDefinitions.AddRange(ReportSeedCatalog.Create());
        var patient = new Patient { MedicalRecordNumber = "MRN-TAT", LastName = "Turn", FirstName = "Around", DateOfBirth = new DateOnly(1970, 1, 1), Sex = Sex.Unknown };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        var t0 = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);
        var specimen = new Specimen { AccessionNumber = "ACC-TAT", PatientId = patient.Id, SpecimenType = "EDTA", CollectedUtc = t0, ReceivedUtc = t0.AddMinutes(15), Status = SpecimenStatus.Accepted };
        var encounter = new Encounter { PatientId = patient.Id, VisitNumber = "VIS-TAT", EncounterType = EncounterType.Inpatient, Status = EncounterStatus.Active, AdmitUtc = t0.AddDays(-1) };
        var location = new OrderingLocation { Code = "LOC-TAT", Name = "ED", IsActive = true };
        db.AddRange(specimen, encounter, location);
        await db.SaveChangesAsync();
        Order NewOrder(string number, OrderPriority priority) => new()
        {
            OrderNumber = number, PatientId = patient.Id, EncounterId = encounter.Id, OrderingLocationId = location.Id, OrderCategory = OrderCategory.Test, OrderName = "Type and Screen",
            OrderType = OrderType.TypeAndScreen, Priority = priority, Status = OrderStatus.Completed, Source = OrderSource.Manual, OrderedUtc = t0
        };
        var stat = NewOrder("ORD-TAT-S", OrderPriority.Stat);
        var routine = NewOrder("ORD-TAT-R", OrderPriority.Routine);
        db.Orders.AddRange(stat, routine);
        await db.SaveChangesAsync();
        TestResult Result(Order order, string code, int minutes, ResultStatus status = ResultStatus.Verified) => new()
        {
            SpecimenId = specimen.Id, PatientId = patient.Id, OrderId = order.Id, TestCode = code, Value = "Negative",
            Status = status, EnteredUtc = t0.AddMinutes(minutes - 5), VerifiedUtc = status == ResultStatus.Verified ? t0.AddMinutes(minutes) : null
        };
        var superseded = Result(stat, "ABSC", 500);
        db.TestResults.AddRange(Result(stat, "ABORH", 40), Result(stat, "ABORH", 60), superseded, Result(routine, "ABORH", 240),
            Result(routine, "ABSC", 300, ResultStatus.Entered));
        await db.SaveChangesAsync();
        var correction = Result(stat, "ABSC", 90);
        db.TestResults.Add(correction);
        await db.SaveChangesAsync();
        superseded.SupersededByResultId = correction.Id;
        await db.SaveChangesAsync();
        ReportViewSql.Apply(sql => db.Database.ExecuteSqlRaw(sql), db.Database.ProviderName ?? string.Empty);

        var service = Service(db);
        var list = (await service.ListAsync()).Value!;
        var detail = await service.RunAsync(list.Single(r => r.Code == "RPT-TAT").Id, new Dictionary<string, string?>());
        Assert.True(detail.Succeeded, detail.Error);
        Assert.Equal(4, detail.Value!.Rows.Count);
        var columns = detail.Value.Columns.ToList();
        var abscStat = detail.Value.Rows.Single(r => r[columns.IndexOf("test_code")] == "ABSC");
        Assert.Equal(("90", "75"), (abscStat[columns.IndexOf("minutes_order_to_verify")], abscStat[columns.IndexOf("minutes_receipt_to_verify")]));

        var byPriority = await service.RunAsync(list.Single(r => r.Code == "RPT-TAT-BY-PRIORITY").Id, new Dictionary<string, string?>());
        Assert.True(byPriority.Succeeded, byPriority.Error);
        Assert.Contains(byPriority.Value!.Rows, r => r.Contains("Stat") && r.Contains("ABORH") && r.Contains("2") && r.Contains("50") && r.Contains("60"));
        Assert.Contains(byPriority.Value.Rows, r => r.Contains("Routine") && r.Contains("ABORH") && r.Contains("1") && r.Contains("240"));

        var sql = await service.SaveAsync(null, new SaveReportRequest
        {
            Code = "RPT-TAT-SQL",
            Name = "SQL turnaround",
            Kind = ReportKind.Sql,
            SqlText = "SELECT test_code, priority, minutes_order_to_verify, minutes_receipt_to_verify FROM rpt_test_turnaround WHERE order_number = 'ORD-TAT-S' ORDER BY minutes_order_to_verify"
        });
        Assert.True(sql.Succeeded, sql.Error);
        var sqlRun = await service.RunAsync(sql.Value!.Id, null);
        Assert.True(sqlRun.Succeeded, sqlRun.Error);
        Assert.Equal(new[] { "40", "60", "90" }, sqlRun.Value!.Rows.Select(r => r[2] ?? "").ToArray());
        Assert.Equal(("Stat", "75"), (sqlRun.Value.Rows[2][1], sqlRun.Value.Rows[2][3]));
    }

    // TEST-BB-641: blood order turnaround has one row per blood order (cancelled ones left out) with the minutes from
    // order to the first unit reserved (released reservations count) and the first issued, and whether a unit was ready
    // by the needed-by time; the by-priority report averages them; the SQL view gives the same values.
    [Fact]
    public async Task BloodOrderTurnaround_MinutesToFirstUnit_ByPriority()
    {
        await using var db = _factory.Create();
        db.ReportDefinitions.AddRange(ReportSeedCatalog.Create());
        var patient = new Patient { MedicalRecordNumber = "MRN-BOT", LastName = "Order", FirstName = "Blood", DateOfBirth = new DateOnly(1970, 1, 1), Sex = Sex.Unknown };
        var product = new ProductType { ProductCode = "RBC-BOT", Name = "Red Blood Cells", ComponentClass = ComponentClass.RedBloodCells };
        var location = new OrderingLocation { Code = "LOC-BOT", Name = "OR", IsActive = true };
        db.AddRange(patient, product, location);
        await db.SaveChangesAsync();
        var t0 = new DateTime(2026, 10, 21, 8, 0, 0, DateTimeKind.Utc);
        var encounter = new Encounter { PatientId = patient.Id, VisitNumber = "VIS-BOT", EncounterType = EncounterType.Inpatient, Status = EncounterStatus.Active, AdmitUtc = t0.AddDays(-1) };
        db.Encounters.Add(encounter);
        await db.SaveChangesAsync();
        Order NewOrder(string number, OrderPriority priority, DateTime? neededBy, OrderStatus status = OrderStatus.InProcess) => new()
        {
            OrderNumber = number, PatientId = patient.Id, EncounterId = encounter.Id, OrderingLocationId = location.Id, OrderCategory = OrderCategory.Product,
            OrderName = "Red cells", OrderType = OrderType.Other, ProductTypeId = product.Id, Priority = priority, Status = status, Source = OrderSource.Manual,
            OrderedUtc = t0, NeededByUtc = neededBy
        };
        var early = NewOrder("BOT-1", OrderPriority.Stat, t0.AddMinutes(60));
        var late = NewOrder("BOT-2", OrderPriority.Stat, t0.AddMinutes(30));
        var waiting = NewOrder("BOT-3", OrderPriority.Routine, null);
        var cancelled = NewOrder("BOT-4", OrderPriority.Routine, null, OrderStatus.Cancelled);
        var testOnly = NewOrder("BOT-5", OrderPriority.Routine, null);
        db.Orders.AddRange(early, late, waiting, cancelled, testOnly);
        await db.SaveChangesAsync();
        foreach (var (order, n) in new[] { (early, 2), (late, 1), (waiting, 3), (cancelled, 1) })
        {
            for (var i = 1; i <= n; i++)
            {
                db.OrderLines.Add(new OrderLine { OrderId = order.Id, LineNumber = i, LineCategory = OrderCategory.Product, LineName = "Red Blood Cells", ProductTypeId = product.Id, OrderType = OrderType.Other, FulfillmentStatus = FulfillmentStatus.Ordered });
            }
        }

        db.OrderLines.Add(new OrderLine { OrderId = testOnly.Id, LineNumber = 1, LineCategory = OrderCategory.Test, LineName = "Type and Screen", TestCode = "ABORH", OrderType = OrderType.TypeAndScreen });
        var units = Enumerable.Range(1, 4).Select(i => new BloodUnit { UnitNumber = $"W-BOT-{i}", ProductTypeId = product.Id, Abo = AboGroup.O, RhD = RhType.Positive, ExpiresUtc = t0.AddDays(20), Status = UnitStatus.Assigned }).ToList();
        db.BloodUnits.AddRange(units);
        await db.SaveChangesAsync();
        db.Allocations.AddRange(
            new Allocation { BloodProductId = units[0].Id, PatientId = patient.Id, OrderId = early.Id, Status = AllocationStatus.Released, AllocatedUtc = t0.AddMinutes(20) },
            new Allocation { BloodProductId = units[1].Id, PatientId = patient.Id, OrderId = early.Id, Status = AllocationStatus.Reserved, AllocatedUtc = t0.AddMinutes(50) },
            new Allocation { BloodProductId = units[2].Id, PatientId = patient.Id, OrderId = late.Id, Status = AllocationStatus.Reserved, AllocatedUtc = t0.AddMinutes(45) });
        db.Issues.Add(new Issue { BloodProductId = units[1].Id, PatientId = patient.Id, OrderId = early.Id, IssuedUtc = t0.AddMinutes(80), IssuedBy = "tech" });
        await db.SaveChangesAsync();
        ReportViewSql.Apply(sql => db.Database.ExecuteSqlRaw(sql), db.Database.ProviderName ?? string.Empty);

        var service = Service(db);
        var list = (await service.ListAsync()).Value!;
        var detail = await service.RunAsync(list.Single(r => r.Code == "RPT-BLOOD-ORDER-TAT").Id, new Dictionary<string, string?>());
        Assert.True(detail.Succeeded, detail.Error);
        var columns = detail.Value!.Columns.ToList();
        string? Cell(string order, string column) => detail.Value.Rows.Single(r => r[columns.IndexOf("order_number")] == order)[columns.IndexOf(column)];
        Assert.Equal(new[] { "BOT-1", "BOT-2", "BOT-3" }, detail.Value.Rows.Select(r => r[columns.IndexOf("order_number")]).Order().ToArray());
        Assert.Equal(("2", "20", "80", "Yes"), (Cell("BOT-1", "units_ordered"), Cell("BOT-1", "minutes_order_to_reserve"), Cell("BOT-1", "minutes_order_to_issue"), Cell("BOT-1", "ready_by_needed_by")));
        Assert.Equal(("45", null, "No"), (Cell("BOT-2", "minutes_order_to_reserve"), Cell("BOT-2", "minutes_order_to_issue"), Cell("BOT-2", "ready_by_needed_by")));
        Assert.Equal(("3", null, null), (Cell("BOT-3", "units_ordered"), Cell("BOT-3", "minutes_order_to_reserve"), Cell("BOT-3", "ready_by_needed_by")));

        var byPriority = await service.RunAsync(list.Single(r => r.Code == "RPT-BLOOD-ORDER-TAT-BY-PRIORITY").Id, new Dictionary<string, string?>());
        Assert.True(byPriority.Succeeded, byPriority.Error);
        Assert.Contains(byPriority.Value!.Rows, r => r.Contains("Stat") && r.Contains("2") && r.Contains("32.5") && r.Contains("45") && r.Contains("80"));
        Assert.Contains(byPriority.Value.Rows, r => r.Contains("Routine") && r.Contains("1"));

        var sql = await service.SaveAsync(null, new SaveReportRequest
        {
            Code = "RPT-BOT-SQL",
            Name = "SQL blood order turnaround",
            Kind = ReportKind.Sql,
            SqlText = "SELECT order_number, units_ordered, minutes_order_to_reserve, minutes_order_to_issue, ready_by_needed_by, priority FROM rpt_blood_order_turnaround WHERE mrn = 'MRN-BOT' ORDER BY order_number"
        });
        Assert.True(sql.Succeeded, sql.Error);
        var sqlRun = await service.RunAsync(sql.Value!.Id, null);
        Assert.True(sqlRun.Succeeded, sqlRun.Error);
        Assert.Equal(new[] { "BOT-1", "BOT-2", "BOT-3" }, sqlRun.Value!.Rows.Select(r => r[0] ?? "").ToArray());
        Assert.Equal(new[] { "2", "20", "80", "Yes", "Stat" }, sqlRun.Value.Rows[0].Skip(1).ToArray());
        Assert.Equal(("45", "No"), (sqlRun.Value.Rows[1][2], sqlRun.Value.Rows[1][4]));
        Assert.True(string.IsNullOrEmpty(sqlRun.Value.Rows[2][2]));
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
