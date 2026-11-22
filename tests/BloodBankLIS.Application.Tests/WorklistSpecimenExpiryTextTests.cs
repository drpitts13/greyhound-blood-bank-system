using BloodBankLIS.Application.Results;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Tests;

// TEST-BB-1280 (end-user testing pass 255): a worklist row on an expired specimen with no result entered says a new
// specimen is needed; one with a result entered (to verify, OCD-093) or on a specimen in date says nothing more.
public class WorklistSpecimenExpiryTextTests
{
    [Fact]
    public void Note_OnlyForAnExpiredSpecimenWithNothingEntered()
    {
        Assert.Equal("Specimen expired: collect a new specimen to test.", WorklistSpecimenExpiryText.Note(true, null));
        Assert.Equal("Specimen expired: collect a new specimen to test.", WorklistSpecimenExpiryText.Note(true, ResultStatus.Pending));
        Assert.Null(WorklistSpecimenExpiryText.Note(true, ResultStatus.Entered));
        Assert.Null(WorklistSpecimenExpiryText.Note(true, ResultStatus.PendingVerification));
        Assert.Null(WorklistSpecimenExpiryText.Note(false, null));
    }

    // TEST-BB-1281 (end-user testing pass 256): a reaction workup row on an expired tube says it may still be tested
    // there (OCD-058), not that a new specimen is needed.
    [Fact]
    public void Note_ReactionWorkup_MayUseTheExpiredTube()
    {
        Assert.Equal("Reaction workup: may be tested on this expired tube (OCD-058).", WorklistSpecimenExpiryText.Note(true, null, reactionWorkup: true));
        Assert.Null(WorklistSpecimenExpiryText.Note(false, null, reactionWorkup: true));
    }

    // TEST-BB-1282 (end-user testing pass 256): the reaction investigation showed the post-transfusion antibody screen as
    // its stored JSON; the stored phased panel reads as cell and phase readings.
    [Fact]
    public void PhasedScreenPanel_ReadsAsReadings()
    {
        const string stored = """{"version":2,"subtests":{"Cell1":{"IS":"0","37C":"0","AHG":"0","CC":"2+"},"Cell2":{"IS":"0","37C":"0","AHG":"0","CC":"2+"}}}""";
        var text = ResultValueText.For(stored);

        Assert.DoesNotContain("{", text);
        Assert.Contains("Cell1 AHG:0", text);
        Assert.Contains("Cell2 CC:2+", text);
    }
}
