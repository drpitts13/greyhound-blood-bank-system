using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-350: receiving the tube in hand by scanning its accession or label barcode.
public class SpecimenReceiptScanTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private SpecimenService Specimens(BloodBankDbContext c) =>
        new(new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c),
            c, _factory.Clock, currentUser: _factory.CurrentUser,
            audit: new AuditWriter(c, _factory.Clock, _factory.CurrentUser));

    private async Task<long> CollectedAsync(string accession, string? barcode = null)
    {
        await using var c = _factory.Create();
        var patient = new Patient { MedicalRecordNumber = "MRN-" + accession, LastName = "Scan", FirstName = "Tube", DateOfBirth = new DateOnly(1990, 2, 2), Sex = Sex.Unknown };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        var r = await Specimens(c).AccessionAsync(new AccessionSpecimenRequest(
            accession, patient.Id, "EDTA", _factory.Clock.UtcNow.AddMinutes(-20), Barcode: barcode, AcceptImmediately: false));
        Assert.True(r.Succeeded, r.Error);
        return r.Value!.Id;
    }

    [Fact]
    public async Task ScanOfTheAccession_ReceivesThatTube_AndRecordsTheMethod()
    {
        var id = await CollectedAsync("ACC-SCAN-RCV");
        await using var c = _factory.Create();
        // Lower case with the spaces and line ending a scanner can add.
        var r = await Specimens(c).ReceiveByScanAsync("  acc-scan-rcv\r\n");
        Assert.True(r.Succeeded, r.Error);
        Assert.Equal(id, r.Value!.Id);
        Assert.Equal(SpecimenStatus.Received, (await c.Specimens.AsNoTracking().SingleAsync(s => s.Id == id)).Status);
        Assert.True(await c.AuditEvents.AnyAsync(a => a.EntityType == nameof(Specimen) && a.EntityId == id && a.NewValueJson!.Contains("Scanned tube")));
    }

    [Fact]
    public async Task ScanOfTheLabelBarcode_ReceivesThatTube()
    {
        var id = await CollectedAsync("ACC-SCAN-BAR", barcode: "LBL-0099");
        await using var c = _factory.Create();
        var r = await Specimens(c).ReceiveByScanAsync("LBL-0099");
        Assert.True(r.Succeeded, r.Error);
        Assert.Equal(id, r.Value!.Id);
    }

    [Fact]
    public async Task UnknownTube_IsRefused_AndNothingChanges()
    {
        var id = await CollectedAsync("ACC-SCAN-KEEP");
        await using var c = _factory.Create();
        var r = await Specimens(c).ReceiveByScanAsync("ACC-NOT-HERE");
        Assert.False(r.Succeeded);
        Assert.Contains(SpecimenReceiptScanRule.Code, r.Error);
        Assert.Equal(SpecimenStatus.Collected, (await c.Specimens.AsNoTracking().SingleAsync(s => s.Id == id)).Status);
    }

    [Fact]
    public async Task ScanningAReceivedTubeAgain_IsRefused_NamingTheAccession()
    {
        await CollectedAsync("ACC-SCAN-TWICE");
        await using var c = _factory.Create();
        Assert.True((await Specimens(c).ReceiveByScanAsync("ACC-SCAN-TWICE")).Succeeded);
        var again = await Specimens(c).ReceiveByScanAsync("ACC-SCAN-TWICE");
        Assert.False(again.Succeeded);
        Assert.StartsWith("ACC-SCAN-TWICE:", again.Error);
    }

    // TEST-BB-351: accept by scan once received; the pending list shows time since draw.
    [Fact]
    public async Task AcceptByScan_AfterReceipt_AcceptsThatTube_AndRecordsTheMethod()
    {
        var id = await CollectedAsync("ACC-SCAN-ACC");
        await using var c = _factory.Create();
        Assert.True((await Specimens(c).ReceiveByScanAsync("ACC-SCAN-ACC")).Succeeded);
        var accepted = await Specimens(c).AcceptByScanAsync("acc-scan-acc");
        Assert.True(accepted.Succeeded, accepted.Error);
        Assert.Equal(SpecimenStatus.Accepted, (await c.Specimens.AsNoTracking().SingleAsync(s => s.Id == id)).Status);
        Assert.True(await c.AuditEvents.AnyAsync(a => a.EntityType == nameof(Specimen) && a.EntityId == id && a.NewValueJson!.Contains("AcceptMethod")));
    }

    [Fact]
    public async Task AcceptByScan_BeforeReceipt_IsRefused_NamingTheTube()
    {
        var id = await CollectedAsync("ACC-SCAN-EARLY");
        await using var c = _factory.Create();
        var r = await Specimens(c).AcceptByScanAsync("ACC-SCAN-EARLY");
        Assert.False(r.Succeeded);
        Assert.StartsWith("ACC-SCAN-EARLY:", r.Error);
        Assert.Equal(SpecimenStatus.Collected, (await c.Specimens.AsNoTracking().SingleAsync(s => s.Id == id)).Status);
    }

    [Fact]
    public async Task PendingList_ShowsTimeSinceDraw_ThenTimeToReceipt()
    {
        await CollectedAsync("ACC-SCAN-TIME");
        await using var c = _factory.Create();
        var waiting = Assert.Single(await Specimens(c).ListPendingReceiptAsync(), r => r.AccessionNumber == "ACC-SCAN-TIME");
        Assert.Equal("waiting 20 min since draw", waiting.TransitNote);

        Assert.True((await Specimens(c).ReceiveByScanAsync("ACC-SCAN-TIME")).Succeeded);
        var received = Assert.Single(await Specimens(c).ListPendingReceiptAsync(), r => r.AccessionNumber == "ACC-SCAN-TIME");
        Assert.Equal("received 20 min after draw", received.TransitNote);
    }

}
