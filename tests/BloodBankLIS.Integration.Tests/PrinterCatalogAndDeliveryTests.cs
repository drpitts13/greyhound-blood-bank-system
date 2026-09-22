using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using BloodBankLIS.Infrastructure.Printing;
using BloodBankLIS.Printing;
using BloodBankLIS.Printing.Delivery;
using BloodBankLIS.Printing.Rendering;

namespace BloodBankLIS.Integration.Tests;

public class PrinterCatalogAndDeliveryTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public PrinterCatalogAndDeliveryTests(SqliteContextFactory factory) => _factory = factory;

    private PrinterCatalogAdminService Catalog(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new PrinterCatalogAdminService(
            new EfRepository<PdfPrinter>(c),
            new EfRepository<ZebraPrinter>(c),
            new EmptyInstalledPrinterCatalog(),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            permissions);
    }

    private PrintService Printing(BloodBankDbContext c) =>
        new(new EfRepository<PrintJob>(c), new EfRepository<Specimen>(c), new EfRepository<Patient>(c),
            new EfRepository<Issue>(c), new EfRepository<BloodUnit>(c), new EfRepository<ProductType>(c),
            new EfRepository<Crossmatch>(c), new EfRepository<PatientBloodTypeHistory>(c),
            c, _factory.Clock, _factory.CurrentUser, new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            new ILabelRenderer[] { new ZplLabelRenderer(), new PdfLabelRenderer() });

    private PrintDeliveryService Delivery(
        BloodBankDbContext c, IPdfDocumentPrinter pdf, IZebraLabelSender zebra, IPermissionEvaluator? permissions = null) =>
        new(new EfRepository<PrintJob>(c), new EfRepository<PdfPrinter>(c), new EfRepository<ZebraPrinter>(c),
            pdf, zebra, c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser), permissions);

    [Fact]
    public async Task CreatePdfPrinter_WithoutEditPermission_IsRejected()
    {
        await using var c = _factory.Create();
        var denied = await Catalog(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .CreatePdfAsync(new SavePdfPrinterRequest("Ward", "HP Laser", false, null));

        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == PrinterAuthorizationRule.CreateCode);
    }

    [Fact]
    public async Task CreateZebra_RequiresHostForNetwork()
    {
        await using var c = _factory.Create();
        var result = await Catalog(c).CreateZebraAsync(new SaveZebraPrinterRequest(
            "Bench", ZebraConnectionType.NetworkTcp, null, 9100, null, null, 9600, 8,
            ZebraParity.None, ZebraStopBits.One, 203, false, null));

        Assert.False(result.Succeeded);
        Assert.Contains("Host", result.Error);
    }

    [Fact]
    public async Task DeliverPdf_UsesFakePrinterAndKeepsPreviewOnFailure()
    {
        await using var c = _factory.Create();
        var job = await PrintSpecimenPdfAsync(c);
        var printer = await Catalog(c).CreatePdfAsync(new SavePdfPrinterRequest("Front desk", "Microsoft Print to PDF", true, null));
        Assert.True(printer.Succeeded, printer.Error);

        var fake = new RecordingPdfPrinter();
        var sent = await Delivery(c, fake, new RecordingZebraSender())
            .DeliverAsync(job.Id, new DeliverPrintRequest(printer.Value!.Id, null));

        Assert.True(sent.Succeeded, sent.Error);
        Assert.Equal(PrintDeliveryStatus.Sent, sent.Value!.DeliveryStatus);
        Assert.Equal("Front desk", sent.Value.TargetPrinter);
        Assert.Equal("Microsoft Print to PDF", fake.PrinterName);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(fake.Pdf!));

        var failed = await Delivery(c, new UnavailablePdfDocumentPrinter(), new RecordingZebraSender())
            .DeliverAsync(job.Id, new DeliverPrintRequest(printer.Value.Id, null));
        Assert.False(failed.Succeeded);
        var stored = await c.PrintJobs.FindAsync(job.Id);
        Assert.Equal(PrintDeliveryStatus.Failed, stored!.DeliveryStatus);
        Assert.NotNull(stored.RenderedPdf);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(stored.RenderedPdf!));
    }

    [Fact]
    public async Task DeliverZpl_ScalesFor300DpiZebra()
    {
        await using var c = _factory.Create();
        var job = await PrintSpecimenZplAsync(c);
        var zebra = await Catalog(c).CreateZebraAsync(new SaveZebraPrinterRequest(
            "Label", ZebraConnectionType.NetworkTcp, "127.0.0.1", 9100, null, null, 9600, 8,
            ZebraParity.None, ZebraStopBits.One, 300, false, null));
        Assert.True(zebra.Succeeded, zebra.Error);

        var sender = new RecordingZebraSender();
        var sent = await Delivery(c, new UnavailablePdfDocumentPrinter(), sender)
            .DeliverAsync(job.Id, new DeliverPrintRequest(null, zebra.Value!.Id));

        Assert.True(sent.Succeeded, sent.Error);
        Assert.StartsWith("^XA", sender.Payload);
        Assert.Contains("^PW600", sender.Payload);
        Assert.DoesNotContain("^PW406", sender.Payload);
    }

    private async Task<PrintJob> PrintSpecimenPdfAsync(BloodBankDbContext c) =>
        (await Printing(c).PrintSpecimenLabelAsync(await SeedSpecimenAsync(c, "PDF-DEL"), new PrintRequest(LabelFormat.Pdf))).Value!;

    private async Task<PrintJob> PrintSpecimenZplAsync(BloodBankDbContext c) =>
        (await Printing(c).PrintSpecimenLabelAsync(await SeedSpecimenAsync(c, "ZPL-DEL"), new PrintRequest(LabelFormat.Zpl))).Value!;

    private async Task<long> SeedSpecimenAsync(BloodBankDbContext c, string mrn)
    {
        var patient = new Patient
        {
            MedicalRecordNumber = mrn,
            LastName = "Print",
            FirstName = "Delivery",
            DateOfBirth = new DateOnly(1990, 1, 2),
            Sex = Sex.Female
        };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        var specimen = new Specimen
        {
            AccessionNumber = "ACC-" + mrn,
            PatientId = patient.Id,
            SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow,
            Status = SpecimenStatus.Accepted
        };
        c.Specimens.Add(specimen);
        await c.SaveChangesAsync();
        return specimen.Id;
    }

    private sealed class RecordingPdfPrinter : IPdfDocumentPrinter
    {
        public string? PrinterName { get; private set; }
        public byte[]? Pdf { get; private set; }

        public Task SendAsync(string windowsPrinterName, byte[] pdf, CancellationToken cancellationToken = default)
        {
            PrinterName = windowsPrinterName;
            Pdf = pdf;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingZebraSender : IZebraLabelSender
    {
        public string? Payload { get; private set; }

        public Task<ZebraSendResult> SendAsync(ZebraLink link, string payload, bool readResponse, CancellationToken cancellationToken = default)
        {
            Payload = payload;
            return Task.FromResult(ZebraSendResult.Ok(readResponse ? "HI" : null));
        }
    }
}
