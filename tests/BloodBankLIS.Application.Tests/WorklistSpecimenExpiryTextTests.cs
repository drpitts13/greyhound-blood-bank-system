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
}
