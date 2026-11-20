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

    // End-user testing pass 78: the SQL report views' months are the local month too, as the visual reports' (pass 51).
    [Fact]
    public async Task ViewMonth_IsTheLocalMonth()
    {
        await using var db = _factory.Create();
        foreach (var utc in new[] { new DateTime(2026, 11, 1, 3, 30, 0, DateTimeKind.Utc), new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 30, 23, 30, 0, DateTimeKind.Utc) })
        {
            var stored = utc.ToString("yyyy-MM-dd HH:mm:ss.fffffff", System.Globalization.CultureInfo.InvariantCulture);
            var month = await db.Database.SqlQueryRaw<string>($"SELECT {ReportViewSql.Month($"'{stored}'", sqlServer: false)} AS Value").SingleAsync();
            Assert.Equal(EfReportSourceReader.MonthText(utc, TimeZoneInfo.Local), month);
        }
    }

    // End-user testing pass 51: a discard at 21:00 on 31 October Eastern counted in November.
    [Fact]
    public void MonthText_IsTheFacilityMonth()
    {
        var eastern = TimeZoneInfo.CreateCustomTimeZone("Test-4", TimeSpan.FromHours(-4), "Test-4", "Test-4");
        Assert.Equal("2026-10", EfReportSourceReader.MonthText(new DateTime(2026, 11, 1, 1, 0, 0, DateTimeKind.Utc), eastern));
        Assert.Equal("2026-11", EfReportSourceReader.MonthText(new DateTime(2026, 11, 1, 4, 0, 0, DateTimeKind.Utc), eastern));
        Assert.Null(EfReportSourceReader.MonthText(null, eastern));
    }

    [Fact]
    public async Task SeededReports_Exist_Run_AndCannotBeDeleted()
    {
        await using var db = _factory.Create();
        db.ReportDefinitions.AddRange(ReportSeedCatalog.Create());
        await db.SaveChangesAsync();

        var service = Service(db);
        var list = await service.ListAsync();
        Assert.True(list.Succeeded);
        Assert.Equal(20, list.Value!.Count);
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

    // TEST-BB-1019: crossmatches and transfusions are counted by month and product (the C:T ratio's numbers); the
    // reporting view carries the month too (end-user testing pass 11).
    [Fact]
    public async Task CrossmatchesAndTransfusionsByMonth_RunsAndTheViewHasTheMonth()
    {
        await using var db = _factory.Create();
        ReportViewSql.Apply(sql => db.Database.ExecuteSqlRaw(sql), db.Database.ProviderName ?? string.Empty);
        db.ReportDefinitions.AddRange(ReportSeedCatalog.Create());
        await db.SaveChangesAsync();

        var service = Service(db);
        var byMonth = (await service.ListAsync()).Value!.Single(r => r.Code == "RPT-XM-TX-BY-MONTH");
        var run = await service.RunAsync(byMonth.Id, new Dictionary<string, string?>());
        Assert.True(run.Succeeded, run.Error);

        var saved = await service.SaveAsync(null, new SaveReportRequest
        {
            Code = "RPT-SQL-XM-MONTH",
            Name = "SQL crossmatch month",
            Kind = Domain.Reporting.ReportKind.Sql,
            SqlText = "SELECT occurred_month, event_type FROM rpt_crossmatch_transfusion"
        });
        Assert.True(saved.Succeeded, saved.Error);
        var sqlRun = await service.RunAsync(saved.Value!.Id, null);
        Assert.True(sqlRun.Succeeded, sqlRun.Error);
        Assert.Contains("occurred_month", sqlRun.Value!.Columns, StringComparer.OrdinalIgnoreCase);
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

    // TEST-BB-1304 (end-user testing pass 278): a test ordered on a tube received days earlier (a reaction workup on a
    // stored tube) counts from the order, not from the tube's receipt, in the visual report and in rpt_test_turnaround.
    [Fact]
    public async Task TestTurnaround_StoredTube_CountsFromTheOrder()
    {
        await using var db = _factory.Create();
        db.ReportDefinitions.AddRange(ReportSeedCatalog.Create());
        var patient = new Patient { MedicalRecordNumber = "MRN-TAT2", LastName = "Stored", FirstName = "Tube", DateOfBirth = new DateOnly(1970, 1, 1), Sex = Sex.Unknown };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        var t0 = new DateTime(2026, 11, 6, 2, 10, 0, DateTimeKind.Utc);
        var specimen = new Specimen { AccessionNumber = "ACC-TAT2", PatientId = patient.Id, SpecimenType = "EDTA", CollectedUtc = t0.AddMinutes(-20), ReceivedUtc = t0, Status = SpecimenStatus.Accepted };
        var encounter = new Encounter { PatientId = patient.Id, VisitNumber = "VIS-TAT2", EncounterType = EncounterType.Inpatient, Status = EncounterStatus.Active, AdmitUtc = t0.AddDays(-1) };
        var location = new OrderingLocation { Code = "LOC-TAT2", Name = "3N", IsActive = true };
        db.AddRange(specimen, encounter, location);
        await db.SaveChangesAsync();
        var ordered = t0.AddDays(6).AddHours(17);
        var order = new Order
        {
            OrderNumber = "ORD-TAT2", PatientId = patient.Id, EncounterId = encounter.Id, OrderingLocationId = location.Id, OrderCategory = OrderCategory.Test, OrderName = "Reaction workup",
            OrderType = OrderType.TransfusionReactionWorkup, Priority = OrderPriority.Stat, Status = OrderStatus.Completed, Source = OrderSource.Manual, OrderedUtc = ordered
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        db.TestResults.Add(new TestResult
        {
            SpecimenId = specimen.Id, PatientId = patient.Id, OrderId = order.Id, TestCode = "DAT", Value = "Negative",
            Status = ResultStatus.Verified, EnteredUtc = ordered.AddMinutes(10), VerifiedUtc = ordered.AddMinutes(18)
        });
        await db.SaveChangesAsync();
        ReportViewSql.Apply(sql => db.Database.ExecuteSqlRaw(sql), db.Database.ProviderName ?? string.Empty);

        var service = Service(db);
        var list = (await service.ListAsync()).Value!;
        var detail = await service.RunAsync(list.Single(r => r.Code == "RPT-TAT").Id, new Dictionary<string, string?>());
        Assert.True(detail.Succeeded, detail.Error);
        var columns = detail.Value!.Columns.ToList();
        var row = detail.Value.Rows.Single(r => r[columns.IndexOf("order_number")] == "ORD-TAT2");
        Assert.Equal(("18", "18"), (row[columns.IndexOf("minutes_order_to_verify")], row[columns.IndexOf("minutes_receipt_to_verify")]));

        var sql = await service.SaveAsync(null, new SaveReportRequest
        {
            Code = "RPT-TAT2-SQL",
            Name = "SQL stored tube turnaround",
            Kind = ReportKind.Sql,
            SqlText = "SELECT minutes_receipt_to_verify FROM rpt_test_turnaround WHERE order_number = 'ORD-TAT2'"
        });
        Assert.True(sql.Succeeded, sql.Error);
        var sqlRun = await service.RunAsync(sql.Value!.Id, null);
        Assert.True(sqlRun.Succeeded, sqlRun.Error);
        Assert.Equal("18", sqlRun.Value!.Rows.Single()[0]);
    }

    // TEST-BB-1162: the equipment check record lists every check recorded across equipment, with the check, pass or
    // fail, who performed it and the note; the SQL view gives the same (end-user testing pass 140: checks were listed one
    // piece of equipment at a time, the last 50).
    [Fact]
    public async Task EquipmentCheckReport_ListsEveryCheck()
    {
        await using var db = _factory.Create();
        db.ReportDefinitions.AddRange(ReportSeedCatalog.Create());
        var block = new Equipment { Code = "HB-RQ", Name = "Heat block (report)", Kind = "Heat block", Location = "Bench 9", SerialNumber = "SN-RQ" };
        db.Equipment.Add(block);
        await db.SaveChangesAsync();
        var check = new EquipmentCheck { EquipmentId = block.Id, Name = "Temperature check", IntervalDays = 1 };
        db.EquipmentChecks.Add(check);
        await db.SaveChangesAsync();
        var t0 = new DateTime(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);
        db.EquipmentCheckRecords.AddRange(
            new EquipmentCheckRecord { EquipmentCheckId = check.Id, EquipmentId = block.Id, PerformedUtc = t0, PerformedBy = "tech", Passed = false, Note = "33.0 C" },
            new EquipmentCheckRecord { EquipmentCheckId = check.Id, EquipmentId = block.Id, PerformedUtc = t0.AddDays(1), PerformedBy = "tech2", Passed = true });
        await db.SaveChangesAsync();
        ReportViewSql.Apply(sql => db.Database.ExecuteSqlRaw(sql), db.Database.ProviderName ?? string.Empty);

        var service = Service(db);
        var list = (await service.ListAsync()).Value!;
        var visual = await service.RunAsync(list.Single(r => r.Code == "RPT-EQUIPMENT-CHECKS").Id, new Dictionary<string, string?>());
        Assert.True(visual.Succeeded, visual.Error);
        var columns = visual.Value!.Columns.ToList();
        var failed = visual.Value.Rows.Single(r => r[columns.IndexOf("equipment_code")] == "HB-RQ" && r[columns.IndexOf("result")] == "Fail");
        Assert.Equal(("Temperature check", "tech", "33.0 C"),
            (failed[columns.IndexOf("check_name")], failed[columns.IndexOf("performed_by")], failed[columns.IndexOf("note")]));

        var sql = await service.SaveAsync(null, new SaveReportRequest
        {
            Code = "RPT-EQUIPMENT-SQL",
            Name = "SQL equipment checks",
            Kind = ReportKind.Sql,
            SqlText = "SELECT equipment_code, check_name, result, performed_by, serial_number FROM rpt_equipment_checks WHERE equipment_code = 'HB-RQ' ORDER BY performed_utc"
        });
        Assert.True(sql.Succeeded, sql.Error);
        var sqlRun = await service.RunAsync(sql.Value!.Id, null);
        Assert.True(sqlRun.Succeeded, sqlRun.Error);
        Assert.Equal(new[] { "HB-RQ", "Temperature check", "Fail", "tech", "SN-RQ" }, sqlRun.Value!.Rows[0].ToArray());
        Assert.Equal("Pass", sqlRun.Value.Rows[1][2]);
    }

    // TEST-BB-1161: the reagent QC record lists every reading with its reagent, lot, control, expected and observed
    // reactions, pass or fail, and who performed and reviewed it, for any period; the SQL view gives the same (end-user
    // testing pass 139: the Reagents page showed 14 days and no report listed QC).
    [Fact]
    public async Task ReagentQcReport_ListsEveryReading()
    {
        await using var db = _factory.Create();
        db.ReportDefinitions.AddRange(ReportSeedCatalog.Create());
        var reagent = new Reagent { Code = "ANTI-D-RQ", Name = "Anti-D (report)" };
        db.Reagents.Add(reagent);
        await db.SaveChangesAsync();
        var lot = new ReagentLot { ReagentId = reagent.Id, LotNumber = "AD-RQ-1", ReceivedUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), ExpiresUtc = new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc), State = ReagentLotState.Opened };
        db.ReagentLots.Add(lot);
        await db.SaveChangesAsync();
        var old = new DateTime(2026, 8, 20, 7, 0, 0, DateTimeKind.Utc);
        db.ReagentDailyQcs.AddRange(
            new ReagentDailyQc { ReagentLotId = lot.Id, QcDateUtc = old.Date, ExpectedResult = "3+", ObservedResult = "0", Control = "D+ cells", Passed = false, PerformedBy = "tech", PerformedUtc = old, ReviewedBy = "lead", ReviewedUtc = old.AddHours(4) },
            new ReagentDailyQc { ReagentLotId = lot.Id, QcDateUtc = old.Date.AddDays(1), ExpectedResult = "3+", ObservedResult = "3+", Passed = true, PerformedBy = "tech", PerformedUtc = old.AddDays(1) });
        await db.SaveChangesAsync();
        ReportViewSql.Apply(sql => db.Database.ExecuteSqlRaw(sql), db.Database.ProviderName ?? string.Empty);

        var service = Service(db);
        var list = (await service.ListAsync()).Value!;
        var visual = await service.RunAsync(list.Single(r => r.Code == "RPT-REAGENT-QC").Id, new Dictionary<string, string?>());
        Assert.True(visual.Succeeded, visual.Error);
        var columns = visual.Value!.Columns.ToList();
        var failed = visual.Value.Rows.Single(r => r[columns.IndexOf("lot_number")] == "AD-RQ-1" && r[columns.IndexOf("result")] == "Fail");
        Assert.Equal(("ANTI-D-RQ", "D+ cells", "3+", "0", "tech", "lead"),
            (failed[columns.IndexOf("reagent_code")], failed[columns.IndexOf("control")], failed[columns.IndexOf("expected_result")],
             failed[columns.IndexOf("observed_result")], failed[columns.IndexOf("performed_by")], failed[columns.IndexOf("reviewed_by")]));
        var passed = visual.Value.Rows.Single(r => r[columns.IndexOf("lot_number")] == "AD-RQ-1" && r[columns.IndexOf("result")] == "Pass");
        Assert.True(string.IsNullOrEmpty(passed[columns.IndexOf("reviewed_by")]));

        var sql = await service.SaveAsync(null, new SaveReportRequest
        {
            Code = "RPT-REAGENT-QC-SQL",
            Name = "SQL reagent QC",
            Kind = ReportKind.Sql,
            SqlText = "SELECT reagent_code, observed_result, result, reviewed_by FROM rpt_reagent_qc WHERE lot_number = 'AD-RQ-1' ORDER BY performed_utc"
        });
        Assert.True(sql.Succeeded, sql.Error);
        var sqlRun = await service.RunAsync(sql.Value!.Id, null);
        Assert.True(sqlRun.Succeeded, sqlRun.Error);
        Assert.Equal(new[] { "ANTI-D-RQ", "0", "Fail", "lead" }, sqlRun.Value!.Rows[0].ToArray());
        Assert.Equal("Pass", sqlRun.Value.Rows[1][2]);
    }

    // TEST-BB-1141: the RhIG report lists each evaluation with its decision and, when given, the time, vials, lot and
    // whole hours from the event; one not given has no dose; the SQL view gives the same (end-user testing pass 121).
    [Fact]
    public async Task RhigReport_ListsEvaluationsAndDoses()
    {
        await using var db = _factory.Create();
        db.ReportDefinitions.AddRange(ReportSeedCatalog.Create());
        var patient = new Patient { MedicalRecordNumber = "MRN-RHR", LastName = "Rhig", FirstName = "Mom", DateOfBirth = new DateOnly(1992, 1, 1), Sex = Sex.Female };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        var t0 = new DateTime(2026, 10, 1, 6, 0, 0, DateTimeKind.Utc);
        db.RhigEvaluations.AddRange(
            new RhigEvaluation
            {
                PatientId = patient.Id, Context = RhigContext.Postpartum, EventUtc = t0, Decision = RhigDecision.Candidate, VialsRecommended = 1,
                RecordedBy = "tech", RecordedUtc = t0.AddHours(1), CompletedBy = "sup", CompletedUtc = t0.AddHours(2),
                GivenUtc = t0.AddHours(30), GivenVials = 1, GivenLot = "LOT-R1", GivenBy = "nurse"
            },
            new RhigEvaluation
            {
                PatientId = patient.Id, Context = RhigContext.Antepartum, EventUtc = t0.AddDays(5), Decision = RhigDecision.Undetermined,
                RecordedBy = "tech", RecordedUtc = t0.AddDays(5)
            });
        await db.SaveChangesAsync();
        ReportViewSql.Apply(sql => db.Database.ExecuteSqlRaw(sql), db.Database.ProviderName ?? string.Empty);

        var service = Service(db);
        var list = (await service.ListAsync()).Value!;
        var visual = await service.RunAsync(list.Single(r => r.Code == "RPT-RHIG").Id, new Dictionary<string, string?>());
        Assert.True(visual.Succeeded, visual.Error);
        var columns = visual.Value!.Columns.ToList();
        var given = visual.Value.Rows.Single(r => r[columns.IndexOf("given_lot")] == "LOT-R1");
        Assert.Equal(("Postpartum", "Candidate", "1", "30"),
            (given[columns.IndexOf("context")], given[columns.IndexOf("decision")], given[columns.IndexOf("given_vials")], given[columns.IndexOf("hours_event_to_given")]));
        var open = visual.Value.Rows.Single(r => r[columns.IndexOf("context")] == "Antepartum");
        Assert.True(string.IsNullOrEmpty(open[columns.IndexOf("hours_event_to_given")]));

        var sql = await service.SaveAsync(null, new SaveReportRequest
        {
            Code = "RPT-RHIG-SQL",
            Name = "SQL RhIG",
            Kind = ReportKind.Sql,
            SqlText = "SELECT context, decision, given_lot, hours_event_to_given FROM rpt_rhig WHERE mrn = 'MRN-RHR' ORDER BY event_utc"
        });
        Assert.True(sql.Succeeded, sql.Error);
        var sqlRun = await service.RunAsync(sql.Value!.Id, null);
        Assert.True(sqlRun.Succeeded, sqlRun.Error);
        Assert.Equal(new[] { "Postpartum", "Candidate", "LOT-R1", "30" }, sqlRun.Value!.Rows[0].ToArray());
        Assert.Equal("Antepartum", sqlRun.Value.Rows[1][0]);
        Assert.True(string.IsNullOrEmpty(sqlRun.Value.Rows[1][3]));
    }

    // TEST-BB-1367 (end-user testing pass 328): the patient antibody record lists every antibody on file with whether it
    // is active and where it was identified; the SQL view gives the same.
    [Fact]
    public async Task AntibodiesReport_ListsThePatientAntibodyRecord()
    {
        await using var db = _factory.Create();
        db.ReportDefinitions.AddRange(ReportSeedCatalog.Create());
        var patient = new Patient { MedicalRecordNumber = "MRN-ABR", LastName = "Abrecord", FirstName = "Pat", DateOfBirth = new DateOnly(1962, 5, 6), Sex = Sex.Female };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        db.AntibodyHistory.AddRange(
            new AntibodyHistory { PatientId = patient.Id, AntibodySpecificity = "anti-K", Status = AntibodyStatus.Identified },
            new AntibodyHistory { PatientId = patient.Id, AntibodySpecificity = "anti-Jka", Status = AntibodyStatus.Identified, ReportingFacility = "County General", IsActive = false, DeactivationReason = "Entered on the wrong chart" });
        await db.SaveChangesAsync();
        ReportViewSql.Apply(sql => db.Database.ExecuteSqlRaw(sql), db.Database.ProviderName ?? string.Empty);

        var service = Service(db);
        var list = (await service.ListAsync()).Value!;
        var visual = await service.RunAsync(list.Single(r => r.Code == "RPT-ANTIBODIES").Id, new Dictionary<string, string?>());
        Assert.True(visual.Succeeded, visual.Error);
        var columns = visual.Value!.Columns.ToList();
        var mine = visual.Value.Rows.Where(r => r[columns.IndexOf("mrn")] == "MRN-ABR").ToList();
        Assert.Equal(2, mine.Count);
        var outside = mine.Single(r => r[columns.IndexOf("antibody")] == "anti-Jka");
        Assert.Equal(("County General", "No", "1962-05-06"),
            (outside[columns.IndexOf("identified_by")], outside[columns.IndexOf("active")], outside[columns.IndexOf("date_of_birth")]));
        Assert.Equal(("This laboratory", "Yes"), (mine.Single(r => r[columns.IndexOf("antibody")] == "anti-K")[columns.IndexOf("identified_by")],
            mine.Single(r => r[columns.IndexOf("antibody")] == "anti-K")[columns.IndexOf("active")]));

        var sql = await service.SaveAsync(null, new SaveReportRequest
        {
            Code = "RPT-ABR-SQL",
            Name = "SQL antibodies",
            Kind = ReportKind.Sql,
            SqlText = "SELECT antibody, active, identified_by FROM rpt_antibodies WHERE mrn = 'MRN-ABR' ORDER BY antibody"
        });
        Assert.True(sql.Succeeded, sql.Error);
        var sqlRun = await service.RunAsync(sql.Value!.Id, null);
        Assert.True(sqlRun.Succeeded, sqlRun.Error);
        Assert.Equal(new[] { "anti-Jka", "No", "County General" }, sqlRun.Value!.Rows[0].ToArray());
        Assert.Equal(new[] { "anti-K", "Yes", "This laboratory" }, sqlRun.Value.Rows[1].ToArray());
    }

    // TEST-BB-1140: the emergency releases report says, for each release before testing was complete, whether the
    // physician's statement was received and from whom; a release with testing complete says it was not needed; the SQL
    // view gives the same (end-user testing pass 120).
    [Fact]
    public async Task OverridesReport_SaysWhetherThePhysicianStatementWasReceived()
    {
        await using var db = _factory.Create();
        db.ReportDefinitions.AddRange(ReportSeedCatalog.Create());
        var patient = new Patient { MedicalRecordNumber = "MRN-URG", LastName = "Urgent", FirstName = "Pat", DateOfBirth = new DateOnly(1970, 1, 1), Sex = Sex.Unknown };
        var product = new ProductType { ProductCode = "RBC-URG", Name = "Red Blood Cells", ComponentClass = ComponentClass.RedBloodCells };
        db.AddRange(patient, product);
        await db.SaveChangesAsync();
        var units = Enumerable.Range(1, 3).Select(i => new BloodUnit { UnitNumber = $"W-URG-{i}", ProductTypeId = product.Id, Abo = AboGroup.O, RhD = RhType.Negative, ExpiresUtc = _factory.Clock.UtcNow.AddDays(20), Status = UnitStatus.Issued }).ToList();
        db.BloodUnits.AddRange(units);
        await db.SaveChangesAsync();
        Issue Emergency(BloodUnit unit, bool incomplete, string? signedBy) => new()
        {
            BloodProductId = unit.Id, PatientId = patient.Id, IssuedUtc = _factory.Clock.UtcNow.AddHours(-2), IssuedBy = "tech",
            IssueType = IssueType.EmergencyRelease, TestsIncompleteAtIssue = incomplete,
            UrgencyStatementSignedBy = signedBy, UrgencyStatementReceivedUtc = signedBy is null ? null : _factory.Clock.UtcNow
        };
        var firstIssue = Emergency(units[0], true, "Dr. Amy Lee");
        db.Issues.AddRange(firstIssue, Emergency(units[1], true, null), Emergency(units[2], false, null));
        await db.SaveChangesAsync();
        // TEST-BB-1269 (end-user testing pass 245): an override names its unit and patient; an issue override is recorded
        // against the unit, and the patient is that unit's latest issue.
        db.Overrides.Add(new Override
        {
            Action = OverrideAction.EmergencyRelease, ContextType = nameof(Issue), ContextId = units[0].Id, RuleCode = "ISS-XM-REQUIRED",
            Reason = "Massive haemorrhage", AuthorizedBy = "Dr. Amy Lee", OverriddenUtc = _factory.Clock.UtcNow.AddHours(-2)
        });
        await db.SaveChangesAsync();
        ReportViewSql.Apply(sql => db.Database.ExecuteSqlRaw(sql), db.Database.ProviderName ?? string.Empty);

        var service = Service(db);
        var list = (await service.ListAsync()).Value!;
        var visual = await service.RunAsync(list.Single(r => r.Code == "RPT-OVERRIDES").Id, new Dictionary<string, string?>());
        Assert.True(visual.Succeeded, visual.Error);
        var columns = visual.Value!.Columns.ToList();
        string? Cell(IReadOnlyList<string?> row, string column) => row[columns.IndexOf(column)];
        var overrideRow = visual.Value.Rows.Single(r => Cell(r, "rule_code") == "ISS-XM-REQUIRED");
        Assert.Equal(("W-URG-1", "MRN-URG"), (Cell(overrideRow, "unit_number"), Cell(overrideRow, "mrn")));
        Assert.Contains(visual.Value.Rows, r => Cell(r, "rule_code") == "Emergency release");
        var issueRows = visual.Value.Rows.Where(r => Cell(r, "rule_code") != "ISS-XM-REQUIRED").ToList();
        string? IssueStatement(string unit) => issueRows.Single(r => Cell(r, "unit_number") == unit)[columns.IndexOf("physician_statement")];
        Assert.Equal(("Received from Dr. Amy Lee", "Not received", "Not needed"), (IssueStatement("W-URG-1"), IssueStatement("W-URG-2"), IssueStatement("W-URG-3")));

        var sql = await service.SaveAsync(null, new SaveReportRequest
        {
            Code = "RPT-URG-SQL",
            Name = "SQL physician statements",
            Kind = ReportKind.Sql,
            SqlText = "SELECT unit_number, physician_statement FROM rpt_overrides WHERE mrn = 'MRN-URG' AND rule_code <> 'ISS-XM-REQUIRED' ORDER BY unit_number"
        });
        Assert.True(sql.Succeeded, sql.Error);
        var sqlRun = await service.RunAsync(sql.Value!.Id, null);
        Assert.True(sqlRun.Succeeded, sqlRun.Error);
        Assert.Equal(new[] { "Received from Dr. Amy Lee", "Not received", "Not needed" }, sqlRun.Value!.Rows.Select(r => r[1] ?? "").ToArray());

        var viewOverride = await service.SaveAsync(null, new SaveReportRequest
        {
            Code = "RPT-URG-OVR",
            Name = "SQL override context",
            Kind = ReportKind.Sql,
            SqlText = "SELECT unit_number, mrn FROM rpt_overrides WHERE rule_code = 'ISS-XM-REQUIRED'"
        });
        Assert.True(viewOverride.Succeeded, viewOverride.Error);
        var viewRun = await service.RunAsync(viewOverride.Value!.Id, null);
        Assert.True(viewRun.Succeeded, viewRun.Error);
        Assert.Equal(("W-URG-1", "MRN-URG"), (viewRun.Value!.Rows.Single()[0], viewRun.Value.Rows.Single()[1]));
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
        // Pass 279: needed by a time long passed, with nothing reserved.
        var waiting = NewOrder("BOT-3", OrderPriority.Routine, t0.AddHours(2));
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
        // TEST-BB-1305 (end-user testing pass 279): a needed-by time passed with no unit ready reads "No".
        Assert.Equal(("3", null, "No"), (Cell("BOT-3", "units_ordered"), Cell("BOT-3", "minutes_order_to_reserve"), Cell("BOT-3", "ready_by_needed_by")));

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
        Assert.Equal("No", sqlRun.Value.Rows[2][4]);
    }

    // TEST-BB-1270 (end-user testing pass 246): the Lookback report names the unit transfused and the date the
    // notification was opened, as the Lookback page does, in the visual report and in rpt_lookback.
    [Fact]
    public async Task LookbackReport_NamesTheUnitAndOpenedDate()
    {
        await using var db = _factory.Create();
        db.ReportDefinitions.AddRange(ReportSeedCatalog.Create());
        var patient = new Patient { MedicalRecordNumber = "MRN-LBK", LastName = "Lookback", FirstName = "Pat", DateOfBirth = new DateOnly(1970, 1, 1), Sex = Sex.Unknown };
        var product = new ProductType { ProductCode = "RBC-LBK", Name = "Red Blood Cells", ComponentClass = ComponentClass.RedBloodCells };
        db.AddRange(patient, product);
        await db.SaveChangesAsync();
        var unit = new BloodUnit { UnitNumber = "W-LBK-1", ProductTypeId = product.Id, Abo = AboGroup.O, RhD = RhType.Negative, ExpiresUtc = _factory.Clock.UtcNow.AddDays(20), Status = UnitStatus.Transfused };
        db.BloodUnits.Add(unit);
        await db.SaveChangesAsync();
        db.LookbackNotifications.Add(new LookbackNotification { Din = "W-LBK-DIN", BloodProductId = unit.Id, PatientId = patient.Id, Reason = "Donor reactive" });
        await db.SaveChangesAsync();
        ReportViewSql.Apply(sql => db.Database.ExecuteSqlRaw(sql), db.Database.ProviderName ?? string.Empty);

        var service = Service(db);
        var list = (await service.ListAsync()).Value!;
        var visual = await service.RunAsync(list.Single(r => r.Code == "RPT-LOOKBACK").Id, new Dictionary<string, string?>());
        Assert.True(visual.Succeeded, visual.Error);
        var columns = visual.Value!.Columns.ToList();
        var row = visual.Value.Rows.Single(r => r[columns.IndexOf("din")] == "W-LBK-DIN");
        Assert.Equal("W-LBK-1", row[columns.IndexOf("unit_number")]);
        Assert.False(string.IsNullOrEmpty(row[columns.IndexOf("opened_utc")]));

        var sql = await service.SaveAsync(null, new SaveReportRequest
        {
            Code = "RPT-LBK-SQL",
            Name = "SQL lookback",
            Kind = ReportKind.Sql,
            SqlText = "SELECT unit_number, opened_utc FROM rpt_lookback WHERE din = 'W-LBK-DIN'"
        });
        Assert.True(sql.Succeeded, sql.Error);
        var sqlRun = await service.RunAsync(sql.Value!.Id, null);
        Assert.True(sqlRun.Succeeded, sqlRun.Error);
        Assert.Equal("W-LBK-1", sqlRun.Value!.Rows.Single()[0]);
        Assert.False(string.IsNullOrEmpty(sqlRun.Value.Rows.Single()[1]));
    }

    // TEST-BB-1271 (end-user testing pass 247): the Open deviations report names the related record (a unit, a temperature
    // reading) as the Deviations page does, with who it is assigned to, when it is due and its FDA reportability, in the
    // visual report and in rpt_deviations.
    [Fact]
    public async Task DeviationsReport_NamesTheRelatedRecordAndFollowUp()
    {
        await using var db = _factory.Create();
        db.ReportDefinitions.AddRange(ReportSeedCatalog.Create());
        var product = new ProductType { ProductCode = "RBC-DEV", Name = "Red Blood Cells", ComponentClass = ComponentClass.RedBloodCells };
        var fridge = new InventoryLocation { Code = "DEV-FRIDGE", Name = "Deviation fridge", StorageTempMinC = 1m, StorageTempMaxC = 6m };
        db.AddRange(product, fridge);
        await db.SaveChangesAsync();
        var unit = new BloodUnit { UnitNumber = "W-DEV-1", ProductTypeId = product.Id, Abo = AboGroup.O, RhD = RhType.Negative, ExpiresUtc = _factory.Clock.UtcNow.AddDays(20), Status = UnitStatus.Available };
        var reading = new StorageTemperatureReading { InventoryLocationId = fridge.Id, ReadingC = 8.5m, ReadingUtc = _factory.Clock.UtcNow, RecordedBy = "tech" };
        db.AddRange(unit, reading);
        await db.SaveChangesAsync();
        db.Deviations.AddRange(
            new Deviation
            {
                Title = "Pass 247 unit", Description = "d", Severity = DeviationSeverity.Major, ContextType = "BloodUnit", ContextId = unit.Id,
                AssignedTo = "qa1", DueOn = new DateOnly(2026, 12, 1), FdaReportability = DeviationFdaReportability.Reportable, ReportedUtc = _factory.Clock.UtcNow
            },
            new Deviation
            {
                Title = "Pass 247 reading", Description = "d", Severity = DeviationSeverity.Major, ContextType = "StorageTemperatureReading", ContextId = reading.Id,
                ReportedUtc = _factory.Clock.UtcNow
            });
        await db.SaveChangesAsync();
        ReportViewSql.Apply(sql => db.Database.ExecuteSqlRaw(sql), db.Database.ProviderName ?? string.Empty);

        var service = Service(db);
        var list = (await service.ListAsync()).Value!;
        var visual = await service.RunAsync(list.Single(r => r.Code == "RPT-DEVIATIONS").Id, new Dictionary<string, string?>());
        Assert.True(visual.Succeeded, visual.Error);
        var columns = visual.Value!.Columns.ToList();
        string? Cell(IReadOnlyList<string?> row, string column) => row[columns.IndexOf(column)];
        var unitRow = visual.Value.Rows.Single(r => Cell(r, "title") == "Pass 247 unit");
        Assert.Equal(("W-DEV-1", "qa1", "2026-12-01", "Reportable"),
            (Cell(unitRow, "record"), Cell(unitRow, "assigned_to"), Cell(unitRow, "due_on"), Cell(unitRow, "fda_reportability")));
        var readingRow = visual.Value.Rows.Single(r => Cell(r, "title") == "Pass 247 reading");
        Assert.Equal(("DEV-FRIDGE 8.5 °C", "Not determined"), (Cell(readingRow, "record"), Cell(readingRow, "fda_reportability")));

        var sql = await service.SaveAsync(null, new SaveReportRequest
        {
            Code = "RPT-DEV-SQL",
            Name = "SQL deviations",
            Kind = ReportKind.Sql,
            SqlText = "SELECT title, record, fda_reportability FROM rpt_deviations WHERE title LIKE 'Pass 247%' ORDER BY title"
        });
        Assert.True(sql.Succeeded, sql.Error);
        var sqlRun = await service.RunAsync(sql.Value!.Id, null);
        Assert.True(sqlRun.Succeeded, sqlRun.Error);
        Assert.Equal(new[] { "DEV-FRIDGE 8.5 °C", "W-DEV-1" }, sqlRun.Value!.Rows.Select(r => r[1] ?? "").ToArray());
        Assert.Equal("Reportable", sqlRun.Value.Rows[1][2]);
    }

    // TEST-BB-1276 (end-user testing pass 251): the overrides report says who authorized each row: an override's
    // authorizer, an emergency issue's override authorizer, or, for an MTP issue without one, the physician who called the
    // protocol; in the visual report and in rpt_overrides.
    [Fact]
    public async Task OverridesReport_SaysWhoAuthorized()
    {
        await using var db = _factory.Create();
        db.ReportDefinitions.AddRange(ReportSeedCatalog.Create());
        var patient = new Patient { MedicalRecordNumber = "MRN-AUTH", LastName = "Auth", FirstName = "Pat", DateOfBirth = new DateOnly(1970, 1, 1), Sex = Sex.Unknown };
        var product = new ProductType { ProductCode = "RBC-AUTH", Name = "Red Blood Cells", ComponentClass = ComponentClass.RedBloodCells };
        db.AddRange(patient, product);
        await db.SaveChangesAsync();
        var units = Enumerable.Range(1, 2).Select(i => new BloodUnit { UnitNumber = $"W-AUTH-{i}", ProductTypeId = product.Id, Abo = AboGroup.O, RhD = RhType.Negative, ExpiresUtc = _factory.Clock.UtcNow.AddDays(20), Status = UnitStatus.Issued }).ToList();
        db.BloodUnits.AddRange(units);
        var activation = new MtpActivation { PatientId = patient.Id, RequestedBy = "Dr. Trauma", Reason = "Haemorrhage", ActivatedUtc = _factory.Clock.UtcNow.AddHours(-3), ActivatedBy = "tech" };
        var ov = new Override
        {
            Action = OverrideAction.EmergencyRelease, ContextType = nameof(Issue), ContextId = 0, RuleCode = "ISS-XM-REQUIRED",
            Reason = "Bleeding", AuthorizedBy = "Dr. Amy Lee", OverriddenUtc = _factory.Clock.UtcNow.AddHours(-2)
        };
        db.AddRange(activation, ov);
        await db.SaveChangesAsync();
        db.Issues.AddRange(
            new Issue
            {
                BloodProductId = units[0].Id, PatientId = patient.Id, IssuedUtc = _factory.Clock.UtcNow.AddHours(-2), IssuedBy = "tech",
                IssueType = IssueType.EmergencyRelease, TestsIncompleteAtIssue = true, OverrideId = ov.Id
            },
            new Issue
            {
                BloodProductId = units[1].Id, PatientId = patient.Id, IssuedUtc = _factory.Clock.UtcNow.AddHours(-2), IssuedBy = "tech",
                IssueType = IssueType.MassiveTransfusion, TestsIncompleteAtIssue = true, MtpActivationId = activation.Id
            });
        await db.SaveChangesAsync();
        ReportViewSql.Apply(sql => db.Database.ExecuteSqlRaw(sql), db.Database.ProviderName ?? string.Empty);

        var service = Service(db);
        var list = (await service.ListAsync()).Value!;
        var visual = await service.RunAsync(list.Single(r => r.Code == "RPT-OVERRIDES").Id, new Dictionary<string, string?>());
        Assert.True(visual.Succeeded, visual.Error);
        var columns = visual.Value!.Columns.ToList();
        string? Cell(IReadOnlyList<string?> row, string column) => row[columns.IndexOf(column)];
        string? Issued(string unit) => Cell(visual.Value.Rows.Single(r => Cell(r, "unit_number") == unit && Cell(r, "rule_code") != "ISS-XM-REQUIRED"), "authorized_by");
        Assert.Equal("Dr. Amy Lee", Issued("W-AUTH-1"));
        Assert.Equal($"Dr. Trauma (called MTP #{activation.Id})", Issued("W-AUTH-2"));
        Assert.Equal("Dr. Amy Lee", Cell(visual.Value.Rows.Single(r => Cell(r, "rule_code") == "ISS-XM-REQUIRED"), "authorized_by"));

        var sql = await service.SaveAsync(null, new SaveReportRequest
        {
            Code = "RPT-AUTH-SQL",
            Name = "SQL authorizers",
            Kind = ReportKind.Sql,
            SqlText = "SELECT unit_number, authorized_by FROM rpt_overrides WHERE mrn = 'MRN-AUTH' AND rule_code IN ('EmergencyRelease', 'MassiveTransfusion') ORDER BY unit_number"
        });
        Assert.True(sql.Succeeded, sql.Error);
        var sqlRun = await service.RunAsync(sql.Value!.Id, null);
        Assert.True(sqlRun.Succeeded, sqlRun.Error);
        Assert.Equal(new[] { "Dr. Amy Lee", $"Dr. Trauma (called MTP #{activation.Id})" }, sqlRun.Value!.Rows.Select(r => r[1] ?? "").ToArray());
    }

    // TEST-BB-1303 (end-user testing pass 277): the seeded Units issued and transfused report shows each issue's status, so a
    // unit that came back reads Returned rather than a blank disposition like one still out.
    [Fact]
    public async Task IssuedAndTransfusedReport_ShowsTheIssueStatus()
    {
        await using var db = _factory.Create();
        db.ReportDefinitions.AddRange(ReportSeedCatalog.Create());
        var patient = new Patient { MedicalRecordNumber = "MRN-ITX", LastName = "Issued", FirstName = "Pat", DateOfBirth = new DateOnly(1970, 1, 1), Sex = Sex.Unknown };
        var product = new ProductType { ProductCode = "RBC-ITX", Name = "Red Blood Cells", ComponentClass = ComponentClass.RedBloodCells };
        db.AddRange(patient, product);
        await db.SaveChangesAsync();
        var units = Enumerable.Range(1, 2).Select(i => new BloodUnit { UnitNumber = $"W-ITX-{i}", ProductTypeId = product.Id, Abo = AboGroup.O, RhD = RhType.Negative, ExpiresUtc = _factory.Clock.UtcNow.AddDays(20), Status = UnitStatus.Issued }).ToList();
        db.BloodUnits.AddRange(units);
        await db.SaveChangesAsync();
        db.Issues.AddRange(
            new Issue { BloodProductId = units[0].Id, PatientId = patient.Id, IssuedUtc = _factory.Clock.UtcNow.AddHours(-2), IssuedBy = "tech", Status = IssueStatus.Returned },
            new Issue { BloodProductId = units[1].Id, PatientId = patient.Id, IssuedUtc = _factory.Clock.UtcNow.AddHours(-1), IssuedBy = "tech", Status = IssueStatus.Issued });
        await db.SaveChangesAsync();

        var service = Service(db);
        var list = (await service.ListAsync()).Value!;
        var run = await service.RunAsync(list.Single(r => r.Code == "RPT-ISSUE-TRANSFUSE").Id, new Dictionary<string, string?>());
        Assert.True(run.Succeeded, run.Error);
        var columns = run.Value!.Columns.ToList();
        string? Status(string unit) => run.Value.Rows.Single(r => r[columns.IndexOf("unit_number")] == unit)[columns.IndexOf("issue_status")];
        Assert.Equal(("Returned", "Issued"), (Status("W-ITX-1"), Status("W-ITX-2")));
    }

    // TEST-BB-1325 (end-user testing pass 298): the seeded reaction investigations report carries what a hemovigilance
    // report needs: the reaction concluded, imputability, when closed, and when and under what reference it was reported.
    [Fact]
    public async Task ReactionsReport_CarriesWhatHemovigilanceNeeds()
    {
        await using var db = _factory.Create();
        db.ReportDefinitions.AddRange(ReportSeedCatalog.Create());
        var patient = new Patient { MedicalRecordNumber = "MRN-HV", LastName = "Hemo", FirstName = "Vig", DateOfBirth = new DateOnly(1970, 1, 1), Sex = Sex.Unknown };
        var product = new ProductType { ProductCode = "RBC-HV", Name = "Red Blood Cells", ComponentClass = ComponentClass.RedBloodCells };
        db.AddRange(patient, product);
        await db.SaveChangesAsync();
        var unit = new BloodUnit { UnitNumber = "W-HV-1", ProductTypeId = product.Id, Abo = AboGroup.O, RhD = RhType.Negative, ExpiresUtc = _factory.Clock.UtcNow.AddDays(20), Status = UnitStatus.Transfused };
        db.BloodUnits.Add(unit);
        await db.SaveChangesAsync();
        var issue = new Issue { BloodProductId = unit.Id, PatientId = patient.Id, IssuedUtc = _factory.Clock.UtcNow.AddDays(-3), IssuedBy = "tech", Status = IssueStatus.Transfused };
        db.Issues.Add(issue);
        await db.SaveChangesAsync();
        var tx = new TransfusionEvent { IssueId = issue.Id, BloodProductId = unit.Id, PatientId = patient.Id, ReactionSuspected = true, DocumentedBy = "rn" };
        db.TransfusionEvents.Add(tx);
        await db.SaveChangesAsync();
        db.ReactionInvestigations.Add(new ReactionInvestigation
        {
            TransfusionEventId = tx.Id, PatientId = patient.Id, BloodProductId = unit.Id, ReportedUtc = _factory.Clock.UtcNow.AddDays(-3),
            Status = ReactionInvestigationStatus.Closed, ClosedUtc = _factory.Clock.UtcNow.AddDays(-1),
            ConcludedReactionText = "Febrile non-hemolytic transfusion reaction", Imputability = ReactionImputability.Probable,
            HemovigilanceReportedOn = new DateOnly(2026, 11, 18), HemovigilanceReference = "NHSN-123"
        });
        await db.SaveChangesAsync();
        ReportViewSql.Apply(sql => db.Database.ExecuteSqlRaw(sql), db.Database.ProviderName ?? string.Empty);

        var service = Service(db);
        var run = await service.RunAsync((await service.ListAsync()).Value!.Single(r => r.Code == "RPT-REACTIONS").Id, new Dictionary<string, string?>());
        Assert.True(run.Succeeded, run.Error);
        var columns = run.Value!.Columns.ToList();
        var row = run.Value.Rows.Single(r => r[columns.IndexOf("unit_number")] == "W-HV-1");
        Assert.Equal("Febrile non-hemolytic transfusion reaction", row[columns.IndexOf("concluded_reaction")]);
        Assert.Equal("Probable", row[columns.IndexOf("imputability")]);
        Assert.Equal("NHSN-123", row[columns.IndexOf("hemovigilance_reference")]);
        Assert.StartsWith("2026-11-18", row[columns.IndexOf("hemovigilance_reported_on")]);
        Assert.False(string.IsNullOrEmpty(row[columns.IndexOf("closed_utc")]));
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
