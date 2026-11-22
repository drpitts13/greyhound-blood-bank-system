using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class InstrumentFailureNextStepTests
{
    // TEST-BB-1237: an open instrument failure says what to do next and links to the specimen and patient (end-user
    // testing pass 215); an unknown code links to Unmapped values; other failures get no step.
    [Fact]
    public void For_NamesTheNextStepAndWhereToDoIt()
    {
        var notOrdered = InstrumentFailureNextStep.For("Test DAT is not ordered on specimen ACC-1 (INST-NOT-ORDERED): not posted.", "ACC-1", "MRN0022");
        Assert.NotNull(notOrdered);
        Assert.StartsWith("If the test should be done on this specimen, order it on the patient's chart, then Replay.", notOrdered!.Text);
        Assert.Equal(["/patients?q=MRN0022"], notOrdered.Links.Select(l => l.Href));

        var mismatch = InstrumentFailureNextStep.For("Patient ID MRN0022 ... (INST-PATIENT): Screen not posted.", "ACC-2", "MRN0022");
        Assert.Contains("not the specimen's patient", mismatch!.Text);
        Assert.Equal(["/test-worklist?accession=ACC-2", "/patients?q=MRN0022"], mismatch.Links.Select(l => l.Href));
        Assert.Equal("Patient the instrument sent (MRN0022)", mismatch.Links[1].Text);

        var unknown = InstrumentFailureNextStep.For("Instrument code Antigen has no translation to a test (INST-CODE).", "ACC-3", "MRN1");
        Assert.Equal(["/admin/hl7/translations?tab=unmapped"], unknown!.Links.Select(l => l.Href));

        Assert.Null(InstrumentFailureNextStep.For("Unable to parse ASTM message.", "ACC-4", "MRN1"));
        Assert.Null(InstrumentFailureNextStep.For(null, "ACC-4", "MRN1"));
    }

    // TEST-BB-1310 (end-user testing pass 284): a test not ordered on a specimen that has since expired does not send the
    // bench to order and replay; it says the result can no longer post there and to resolve it.
    [Fact]
    public void NotOrdered_OnAnExpiredSpecimen_SaysToResolve()
    {
        var expired = new DateTime(2026, 11, 12, 23, 52, 0, DateTimeKind.Utc);
        var step = InstrumentFailureNextStep.For("Test DAT is not ordered on specimen ACC-DEMO-RCV1 (INST-NOT-ORDERED): not posted.", "ACC-DEMO-RCV1", "MRN0022", expired);
        Assert.StartsWith($"Specimen ACC-DEMO-RCV1 expired {expired.ToLocalTime():yyyy-MM-dd HH:mm}, so this result can no longer be posted to it", step!.Text);
        Assert.Contains("Resolve this message with the reason", step.Text);
        Assert.DoesNotContain("then Replay", step.Text);
    }
}
