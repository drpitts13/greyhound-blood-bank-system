using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Isbt128;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-299: alert inbox — raise, reconcile, acknowledge, scanner sources, and producers.
public class AlertWorkflowTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private AlertService Alerts(BloodBankDbContext c) =>
        new(new EfRepository<Alert>(c), c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser));

    private AlertScanner Scanner(BloodBankDbContext c)
    {
        var audit = new AuditWriter(c, _factory.Clock, _factory.CurrentUser);
        var minimumStock = new MinimumStockService(
            new EfRepository<MinimumStockLevel>(c), new EfRepository<ProductType>(c), new InventoryRepository(c), c,
            supplierOrders: new EfRepository<SupplierOrder>(c), supplierOrderLines: new EfRepository<SupplierOrderLine>(c), clock: _factory.Clock);
        var inventory = new InventoryService(
            new InventoryRepository(c),
            new EfRepository<UnitBloodAttribute>(c),
            new EfRepository<BloodAttributeDefinition>(c),
            new IsbtLookupCatalog(new EfRepository<IsbtAboRhdCode>(c), new EfRepository<IsbtProductCode>(c)),
            c, _factory.Clock, _factory.CurrentUser, audit,
            patients: new EfRepository<Patient>(c), allocations: new EfRepository<Allocation>(c));
        var reagents = new ReagentQcService(
            new EfRepository<Reagent>(c), new EfRepository<ReagentLot>(c), new EfRepository<ReagentDailyQc>(c),
            c, _factory.Clock, _factory.CurrentUser, audit,
            new EfRepository<TestResultReagentLot>(c), new EfRepository<TestResult>(c), new EfRepository<Specimen>(c), new EfRepository<Patient>(c));
        return new AlertScanner(Alerts(c), minimumStock, inventory, reagents,
            new EfRepository<TransfusionEvent>(c), new EfRepository<Issue>(c), new EfRepository<BloodUnit>(c),
            new EfRepository<Patient>(c), new FacilityPolicyService(new EfRepository<SystemSetting>(c)), _factory.Clock);
    }

    // TEST-BB-546: a lot whose latest QC failed names the results tested with it since its last passed QC, on the
    // worklist, in the critical alert (which opens that lot's results), and in the lookback (listed first, To review).
    [Fact]
    public async Task FailedQc_NamesTheResultsToReview_AndTheAlertOpensThem()
    {
        var now = _factory.Clock.UtcNow;
        long lotId, before, after;
        await using (var c = _factory.Create())
        {
            var reagent = new Reagent { Code = "FQ-AHG", Name = "Failed QC AHG", TestCodes = "FQSC" };
            c.Reagents.Add(reagent);
            await c.SaveChangesAsync();
            var lot = new ReagentLot { ReagentId = reagent.Id, LotNumber = "FQ-1", ReceivedUtc = now.AddDays(-5), ExpiresUtc = now.AddDays(30), State = ReagentLotState.Opened };
            c.ReagentLots.Add(lot);
            var patient = new Patient { MedicalRecordNumber = "MRN-FQ", LastName = "Qc", FirstName = "Failed", DateOfBirth = new DateOnly(1980, 1, 1), Sex = Sex.Unknown };
            c.Patients.Add(patient);
            await c.SaveChangesAsync();
            var specimen = new Specimen { AccessionNumber = "ACC-FQ", PatientId = patient.Id, SpecimenType = "EDTA", CollectedUtc = now.AddDays(-2), Status = SpecimenStatus.Accepted };
            c.Specimens.Add(specimen);
            await c.SaveChangesAsync();
            TestResult Result(string value, DateTime at) => new() { SpecimenId = specimen.Id, PatientId = patient.Id, TestCode = "FQSC", Value = value, Status = ResultStatus.Verified, Source = ResultSource.Manual, EnteredUtc = at, EnteredBy = "tech" };
            var r1 = Result("Negative", now.AddHours(-30));
            var r2 = Result("Positive", now.AddHours(-2));
            c.TestResults.AddRange(r1, r2);
            await c.SaveChangesAsync();
            c.TestResultReagentLots.AddRange(
                new TestResultReagentLot { TestResultId = r1.Id, ReagentLotId = lot.Id, ReagentCode = "FQ-AHG", LotNumber = "FQ-1", RecordedUtc = now.AddHours(-30) },
                new TestResultReagentLot { TestResultId = r2.Id, ReagentLotId = lot.Id, ReagentCode = "FQ-AHG", LotNumber = "FQ-1", RecordedUtc = now.AddHours(-2) });
            c.ReagentDailyQcs.AddRange(
                new ReagentDailyQc { ReagentLotId = lot.Id, QcDateUtc = now.AddDays(-1).Date, ExpectedResult = "4+", ObservedResult = "4+", Passed = true, PerformedBy = "tech", PerformedUtc = now.AddHours(-26), ReviewedBy = "sup", ReviewedUtc = now.AddHours(-25) },
                new ReagentDailyQc { ReagentLotId = lot.Id, QcDateUtc = now.Date, ExpectedResult = "4+", ObservedResult = "0", Passed = false, PerformedBy = "tech", PerformedUtc = now.AddHours(-1) });
            await c.SaveChangesAsync();
            (lotId, before, after) = (lot.Id, r1.Id, r2.Id);
        }

        await using (var c = _factory.Create())
        {
            await Scanner(c).ScanAsync();
        }

        await using var verify = _factory.Create();
        var alert = await verify.Alerts.SingleAsync(a => a.Source == AlertRule.ReagentQc && a.Title == "Reagent QC failed: lot FQ-1" && a.ResolvedUtc == null);
        Assert.Equal(AlertSeverity.Critical, alert.Severity);
        Assert.Contains("1 result was tested with this lot since it last passed QC", alert.Message);
        Assert.Equal($"/reagents?lot={lotId}", alert.Link);

        var reagents = new ReagentQcService(
            new EfRepository<Reagent>(verify), new EfRepository<ReagentLot>(verify), new EfRepository<ReagentDailyQc>(verify),
            verify, _factory.Clock, _factory.CurrentUser, new AuditWriter(verify, _factory.Clock, _factory.CurrentUser),
            new EfRepository<TestResultReagentLot>(verify), new EfRepository<TestResult>(verify), new EfRepository<Specimen>(verify), new EfRepository<Patient>(verify));
        Assert.Equal(1, (await reagents.ListWorkAsync()).Single(w => w.LotId == lotId && w.Kind == ReagentQcWorklistRule.Failed).ResultsToReview);
        var lookback = (await reagents.ResultsForLotAsync(lotId)).Value!;
        Assert.Equal([(after, true), (before, false)], lookback.Select(r => (r.ResultId, r.ToReview)));

        // TEST-BB-547: a result to review is marked reviewed with a note (audited); the count and the alert drop it.
        Assert.Contains("Say what", (await reagents.ReviewLotResultAsync(lotId, after, " ")).Error);
        Assert.Contains("since it last passed", (await reagents.ReviewLotResultAsync(lotId, before, "checked")).Error);
        var reviewed = await reagents.ReviewLotResultAsync(lotId, after, "Repeated with lot FQ-2: Positive confirmed.");
        Assert.True(reviewed.Succeeded, reviewed.Error);
        Assert.Equal((false, "tech-test", "Repeated with lot FQ-2: Positive confirmed."), (reviewed.Value!.ToReview, reviewed.Value.ReviewedBy, reviewed.Value.ReviewNote));
        Assert.Contains("already", (await reagents.ReviewLotResultAsync(lotId, after, "again")).Error);
        Assert.Equal(0, (await reagents.ListWorkAsync()).Single(w => w.LotId == lotId && w.Kind == ReagentQcWorklistRule.Failed).ResultsToReview);
        Assert.True(await verify.AuditEvents.AnyAsync(a => a.EntityType == nameof(TestResult) && a.EntityId == after && a.Reason!.StartsWith("Reviewed after reagent lot FQ-1 failed QC")));

        await using (var c = _factory.Create())
        {
            await Scanner(c).ScanAsync();
        }

        await using var after2 = _factory.Create();
        var refreshed = await after2.Alerts.SingleAsync(a => a.Source == AlertRule.ReagentQc && a.Title == "Reagent QC failed: lot FQ-1" && a.ResolvedUtc == null);
        Assert.DoesNotContain("review", refreshed.Message);
    }

    // TEST-BB-346: a transfusion started and not completed within the reminder hours raises a warning,
    // which resolves once the transfusion is completed. A recent start raises nothing.
    private async Task<(long IssueId, long EventId)> StartedTransfusionAsync(string key, DateTime startUtc)
    {
        await using var c = _factory.Create();
        var product = new ProductType { ProductCode = $"RBC-{key}", Name = "Red cells" };
        var patient = new Patient { MedicalRecordNumber = $"MRN-{key}", LastName = "Bedside", FirstName = "Pat", DateOfBirth = new DateOnly(1970, 1, 1), Sex = Sex.Unknown };
        c.ProductTypes.Add(product);
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        var unit = new BloodUnit
        {
            UnitNumber = $"U-{key}", ProductTypeId = product.Id, Abo = AboGroup.O, RhD = RhType.Positive,
            ExpiresUtc = startUtc.AddDays(20), Status = UnitStatus.TransfusionStarted
        };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();
        var issue = new Issue { BloodProductId = unit.Id, PatientId = patient.Id, IssuedUtc = startUtc.AddMinutes(-20), IssuedBy = "tech", Status = IssueStatus.Issued };
        c.Issues.Add(issue);
        await c.SaveChangesAsync();
        var record = new TransfusionEvent { IssueId = issue.Id, BloodProductId = unit.Id, PatientId = patient.Id, StartUtc = startUtc, DocumentedBy = "nurse" };
        c.TransfusionEvents.Add(record);
        await c.SaveChangesAsync();
        return (issue.Id, record.Id);
    }

    [Fact]
    public async Task Scanner_RaisesTransfusionNotCompleted_AfterReminderHours_AndResolvesWhenCompleted()
    {
        var now = _factory.Clock.UtcNow;
        var (oldIssue, oldEvent) = await StartedTransfusionAsync("TXOLD", now.AddHours(-7));
        await StartedTransfusionAsync("TXNEW", now.AddHours(-1));

        await using (var c = _factory.Create())
        {
            await Scanner(c).ScanAsync();
            var alert = Assert.Single(await c.Alerts.Where(a => a.Source == AlertRule.TransfusionNotCompleted).ToListAsync());
            Assert.Equal($"TXINCOMPLETE:ISSUE-{oldIssue}", alert.Key);
            Assert.Equal(AlertSeverity.Warning, alert.Severity);
            Assert.Contains("U-TXOLD", alert.Title);
            Assert.Contains("MRN-TXOLD", alert.Message);
            Assert.Equal($"/issuing?desk=transfuse&issueId={oldIssue}", alert.Link);
        }

        await using (var c = _factory.Create())
        {
            var record = await c.TransfusionEvents.SingleAsync(t => t.Id == oldEvent);
            record.StopUtc = now.AddMinutes(-5);
            var issue = await c.Issues.SingleAsync(i => i.Id == oldIssue);
            issue.Status = IssueStatus.Transfused;
            await c.SaveChangesAsync();
            await Scanner(c).ScanAsync();
            Assert.NotNull((await c.Alerts.SingleAsync(a => a.Source == AlertRule.TransfusionNotCompleted)).ResolvedUtc);
        }
    }

    [Fact]
    public async Task Scanner_TransfusionReminder_OffWhenHoursAreZero()
    {
        await StartedTransfusionAsync("TXOFF", _factory.Clock.UtcNow.AddHours(-30));
        await using var c = _factory.Create();
        c.SystemSettings.Add(new SystemSetting { Key = FacilityPolicyKeys.TransfusionCompletionReminderHours, Value = "0" });
        await c.SaveChangesAsync();
        await Scanner(c).ScanAsync();
        Assert.False(await c.Alerts.AnyAsync(a => a.Source == AlertRule.TransfusionNotCompleted));
    }

    private static AlertCandidate Candidate(string source, string key) =>
        new(source, key, AlertSeverity.Warning, $"Title {key}", "Message", "/x");

    [Fact]
    public async Task Raise_SkipsAnActiveKey_EvenAfterAcknowledgement()
    {
        await using var c = _factory.Create();
        var alerts = Alerts(c);

        Assert.True(await alerts.RaiseAsync(Candidate(AlertRule.UnitsExpired, "EXPIRED:1")));
        Assert.False(await alerts.RaiseAsync(Candidate(AlertRule.UnitsExpired, "EXPIRED:1")));
        var id = (await c.Alerts.SingleAsync()).Id;
        Assert.True((await alerts.AcknowledgeAsync(id, new AcknowledgeAlertRequest())).Succeeded);
        Assert.False(await alerts.RaiseAsync(Candidate(AlertRule.UnitsExpired, "EXPIRED:1")));
        Assert.Equal(1, await c.Alerts.CountAsync());
        Assert.Equal(0, await alerts.CountOpenAsync());
    }

    [Fact]
    public async Task Sync_RaisesNew_ResolvesCleared_AndLeavesOtherSourcesAlone()
    {
        await using var c = _factory.Create();
        var alerts = Alerts(c);
        await alerts.RaiseAsync(Candidate(AlertRule.TemperatureExcursion, "TEMP:1"));

        var first = await alerts.SyncAsync(AlertRule.MinimumStockShortage, [Candidate(AlertRule.MinimumStockShortage, "PAR:1"), Candidate(AlertRule.MinimumStockShortage, "PAR:2")]);
        Assert.Equal((2, 0), first);
        var second = await alerts.SyncAsync(AlertRule.MinimumStockShortage, [Candidate(AlertRule.MinimumStockShortage, "PAR:2")]);
        Assert.Equal((0, 1), second);

        var resolved = await c.Alerts.SingleAsync(a => a.Key == "PAR:1");
        Assert.Equal(_factory.Clock.UtcNow, resolved.ResolvedUtc);
        Assert.Null((await c.Alerts.SingleAsync(a => a.Key == "TEMP:1")).ResolvedUtc);
        Assert.Equal(2, await alerts.CountOpenAsync());

        var open = await alerts.ListAsync(openOnly: true);
        Assert.Equal(["PAR:2", "TEMP:1"], open.Select(a => a.Title.Replace("Title ", "")).OrderBy(k => k));
        var withClosed = await alerts.ListAsync(openOnly: false);
        Assert.Equal(3, withClosed.Count);
    }

    // TEST-BB-379: a near-expiry alert on a reserved unit is a warning that names the patient and the next step.
    [Fact]
    public async Task NearExpiryAlert_OnAReservedUnit_NamesThePatient_AndIsAWarning()
    {
        await using var c = _factory.Create();
        var now = _factory.Clock.UtcNow;
        var product = new ProductType { ProductCode = "RBC-NEAR-RES", Name = "Red cells" };
        var patient = new Patient { MedicalRecordNumber = "MRN-ALRT-RES", LastName = "Alert", FirstName = "Ana", DateOfBirth = new DateOnly(1981, 3, 3), Sex = Sex.Female };
        c.ProductTypes.Add(product);
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        var unit = new BloodUnit { UnitNumber = "U-ALRT-RES", ProductTypeId = product.Id, Abo = AboGroup.O, RhD = RhType.Positive, ExpiresUtc = now.AddHours(5), Status = UnitStatus.Crossmatched };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();
        c.Allocations.Add(new Allocation { BloodProductId = unit.Id, PatientId = patient.Id, Status = AllocationStatus.Reserved, AllocatedUtc = now.AddHours(-1), AllocatedBy = "tech" });
        await c.SaveChangesAsync();

        await Scanner(c).ScanAsync();

        var alert = await c.Alerts.SingleAsync(a => a.Source == AlertRule.NearExpiry && a.Title == "Near expiry: U-ALRT-RES");
        Assert.Equal(AlertSeverity.Warning, alert.Severity);
        Assert.Contains("Reserved for Alert, Ana (MRN-ALRT-RES). Issue it first or reserve a replacement.", alert.Message);
    }

    [Fact]
    public async Task Acknowledge_RecordsWhoAndNote_IsAudited_AndOnlyOnce()
    {
        await using var c = _factory.Create();
        var alerts = Alerts(c);
        await alerts.RaiseAsync(Candidate(AlertRule.TemperatureExcursion, "TEMP:9"));
        var id = (await c.Alerts.SingleAsync()).Id;

        var ack = await alerts.AcknowledgeAsync(id, new AcknowledgeAlertRequest("  Fridge door closed  "));
        Assert.True(ack.Succeeded, ack.Error);
        Assert.Equal("tech-test", ack.Value!.AcknowledgedBy);
        Assert.Equal("Fridge door closed", ack.Value.AcknowledgeNote);
        Assert.True(await c.AuditEvents.AnyAsync(e => e.EntityType == nameof(Alert) && e.EntityId == id));

        Assert.False((await alerts.AcknowledgeAsync(id, new AcknowledgeAlertRequest())).Succeeded);
        Assert.False((await alerts.AcknowledgeAsync(999_999, new AcknowledgeAlertRequest())).Succeeded);
    }

    [Fact]
    public async Task Scanner_RaisesMinimumStockNearExpiryAndQc_AndResolvesMinimumStockWhenStocked()
    {
        await using var c = _factory.Create();
        var now = _factory.Clock.UtcNow;
        var product = new ProductType { ProductCode = "RBC-ALRT", Name = "Red cells" };
        c.ProductTypes.Add(product);
        await c.SaveChangesAsync();
        c.MinimumStockLevels.Add(new MinimumStockLevel { ProductTypeId = product.Id, MinimumAvailable = 2 });
        c.BloodUnits.Add(new BloodUnit
        {
            UnitNumber = "U-ALRT-NEAR",
            ProductTypeId = product.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = now.AddHours(6),
            Status = UnitStatus.Available
        });
        var reagent = new Reagent { Code = "ANTI-D", Name = "Anti-D" };
        c.Reagents.Add(reagent);
        await c.SaveChangesAsync();
        c.ReagentLots.Add(new ReagentLot
        {
            ReagentId = reagent.Id,
            LotNumber = "L-ALRT",
            ReceivedUtc = now.AddDays(-2),
            OpenedUtc = now.AddDays(-2),
            ExpiresUtc = now.AddDays(30),
            State = ReagentLotState.Opened
        });
        await c.SaveChangesAsync();

        var (raised, resolved) = await Scanner(c).ScanAsync();

        Assert.Equal(0, resolved);
        Assert.True(raised >= 3);
        var sources = await c.Alerts.Select(a => a.Source).Distinct().ToListAsync();
        Assert.Contains(AlertRule.MinimumStockShortage, sources);
        Assert.Contains(AlertRule.NearExpiry, sources);
        Assert.Contains(AlertRule.ReagentQc, sources);
        var shortage = await c.Alerts.SingleAsync(a => a.Source == AlertRule.MinimumStockShortage);
        Assert.Contains("short 1", shortage.Message);
        Assert.Equal("/inventory/minimum-stock", shortage.Link);

        // A second scan with nothing changed raises nothing new.
        Assert.Equal(0, (await Scanner(c).ScanAsync()).Raised);

        c.BloodUnits.Add(new BloodUnit
        {
            UnitNumber = "U-ALRT-2",
            ProductTypeId = product.Id,
            Abo = AboGroup.A,
            RhD = RhType.Positive,
            ExpiresUtc = now.AddDays(20),
            Status = UnitStatus.Available
        });
        await c.SaveChangesAsync();
        await Scanner(c).ScanAsync();
        Assert.NotNull((await c.Alerts.SingleAsync(a => a.Id == shortage.Id)).ResolvedUtc);
    }

    [Fact]
    public async Task UnitsExpiredAndExcursion_RaiseEventAlerts()
    {
        await using var c = _factory.Create();
        var scanner = Scanner(c);
        Assert.False(await scanner.RaiseUnitsExpiredAsync(0, _factory.Clock.UtcNow));
        Assert.True(await scanner.RaiseUnitsExpiredAsync(3, _factory.Clock.UtcNow));
        var expired = await c.Alerts.SingleAsync(a => a.Source == AlertRule.UnitsExpired);
        Assert.Equal("3 unit(s) expired", expired.Title);
        Assert.DoesNotContain("reserved for patients", expired.Message);

        // TEST-BB-359: the alert says when expired units were reserved for patients.
        Assert.True(await scanner.RaiseUnitsExpiredAsync(2, _factory.Clock.UtcNow.AddMinutes(1), reservationsEnded: 2));
        Assert.Contains(await c.Alerts.Where(a => a.Source == AlertRule.UnitsExpired).Select(a => a.Message).ToListAsync(),
            m => m.Contains("2 of them were reserved for patients"));

        var location = new InventoryLocation { Code = "ALRT-FRIDGE", Name = "Alert fridge", StorageTempMinC = 1m, StorageTempMaxC = 6m };
        c.InventoryLocations.Add(location);
        await c.SaveChangesAsync();
        var temps = new StorageTemperatureService(
            new EfRepository<StorageTemperatureReading>(c),
            new EfRepository<InventoryLocation>(c),
            new EfRepository<BloodUnit>(c),
            new EfRepository<Deviation>(c),
            c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            Alerts(c));
        await temps.RecordAsync(new RecordStorageTemperatureRequest(location.Id, 4m));
        Assert.False(await c.Alerts.AnyAsync(a => a.Source == AlertRule.TemperatureExcursion));

        var excursion = await temps.RecordAsync(new RecordStorageTemperatureRequest(location.Id, 9m));
        var alert = await c.Alerts.SingleAsync(a => a.Source == AlertRule.TemperatureExcursion);
        Assert.Equal(AlertSeverity.Critical, alert.Severity);
        Assert.Equal($"TEMP:DEV-{excursion.Value!.DeviationId}", alert.Key);
        Assert.Contains($"DEV-{excursion.Value.DeviationId}", alert.Message);
    }

    // TEST-BB-511: a shortage alert stays one alert as the shortage is ordered: covered by an order on time it drops to
    // information and says so; once that order is late it is a warning again, naming the overdue units.
    [Fact]
    public async Task ShortageAlert_FollowsWhatIsOnOrder_WithoutRaisingAgain()
    {
        await using var c = _factory.Create();
        var product = new ProductType { ProductCode = "RBC-ONORD", Name = "Red cells" };
        var supplier = new BloodSupplier { Code = "SUP-AL", Name = "Supplier", Registration = "R-AL" };
        c.AddRange(product, supplier);
        await c.SaveChangesAsync();
        c.MinimumStockLevels.Add(new MinimumStockLevel { ProductTypeId = product.Id, Abo = AboGroup.A, MinimumAvailable = 2 });
        await c.SaveChangesAsync();

        await Scanner(c).ScanAsync();
        var alert = await c.Alerts.SingleAsync(a => a.Source == AlertRule.MinimumStockShortage);
        Assert.Equal(AlertSeverity.Warning, alert.Severity);
        Assert.Equal("0 available against a minimum of 2 (short 2).", alert.Message);

        var order = new SupplierOrder
        {
            OrderNumber = "SO-AL-1", BloodSupplierId = supplier.Id, Status = SupplierOrderStatus.Submitted, SubmittedUtc = _factory.Clock.UtcNow
        };
        c.SupplierOrders.Add(order);
        await c.SaveChangesAsync();
        c.SupplierOrderLines.Add(new SupplierOrderLine { SupplierOrderId = order.Id, ProductTypeId = product.Id, Abo = AboGroup.A, Quantity = 2 });
        await c.SaveChangesAsync();

        Assert.Equal(0, (await Scanner(c).ScanAsync()).Raised);
        var covered = await c.Alerts.AsNoTracking().SingleAsync(a => a.Source == AlertRule.MinimumStockShortage);
        Assert.Equal(alert.Id, covered.Id);
        Assert.Equal(AlertSeverity.Info, covered.Severity);
        Assert.EndsWith("2 on order, covering the shortage.", covered.Message);

        _factory.Clock.UtcNow = _factory.Clock.UtcNow.AddHours(25);
        Assert.Equal(0, (await Scanner(c).ScanAsync()).Raised);
        var late = await c.Alerts.AsNoTracking().SingleAsync(a => a.Source == AlertRule.MinimumStockShortage);
        Assert.Equal(AlertSeverity.Warning, late.Severity);
        Assert.EndsWith("2 on order, 2 overdue: chase the supplier.", late.Message);
    }

    // TEST-BB-512: acknowledged while covered by a timely order, the shortage alert is raised again, open, when the order
    // is late; the acknowledged one is closed and keeps who acknowledged it.
    [Fact]
    public async Task AcknowledgedShortage_IsRaisedAgain_WhenItsOrderIsLate()
    {
        await using var c = _factory.Create();
        var product = new ProductType { ProductCode = "RBC-ACK", Name = "Red cells" };
        var supplier = new BloodSupplier { Code = "SUP-ACK", Name = "Supplier", Registration = "R-ACK" };
        c.AddRange(product, supplier);
        await c.SaveChangesAsync();
        c.MinimumStockLevels.Add(new MinimumStockLevel { ProductTypeId = product.Id, Abo = AboGroup.B, MinimumAvailable = 1 });
        var order = new SupplierOrder
        {
            OrderNumber = "SO-ACK-1", BloodSupplierId = supplier.Id, Status = SupplierOrderStatus.Submitted, SubmittedUtc = _factory.Clock.UtcNow
        };
        c.SupplierOrders.Add(order);
        await c.SaveChangesAsync();
        c.SupplierOrderLines.Add(new SupplierOrderLine { SupplierOrderId = order.Id, ProductTypeId = product.Id, Abo = AboGroup.B, Quantity = 1 });
        await c.SaveChangesAsync();

        await Scanner(c).ScanAsync();
        var covered = await c.Alerts.AsNoTracking().SingleAsync(a => a.Source == AlertRule.MinimumStockShortage);
        Assert.Equal(AlertSeverity.Info, covered.Severity);
        Assert.True((await Alerts(c).AcknowledgeAsync(covered.Id, new AcknowledgeAlertRequest("Order SO-ACK-1 placed"))).Succeeded);

        _factory.Clock.UtcNow = _factory.Clock.UtcNow.AddHours(25);
        Assert.Equal(1, (await Scanner(c).ScanAsync()).Raised);
        var rows = await c.Alerts.AsNoTracking().Where(a => a.Source == AlertRule.MinimumStockShortage).OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.NotNull(rows[0].AcknowledgedUtc);
        Assert.NotNull(rows[0].ResolvedUtc);
        Assert.Equal(AlertSeverity.Info, rows[0].Severity);
        Assert.True(AlertRule.IsOpen(rows[1]));
        Assert.Equal(AlertSeverity.Warning, rows[1].Severity);
        Assert.EndsWith("1 overdue: chase the supplier.", rows[1].Message);

        // Nothing changed: no third alert.
        Assert.Equal(0, (await Scanner(c).ScanAsync()).Raised);
    }
}
