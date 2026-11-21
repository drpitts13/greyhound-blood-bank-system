using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class InstrumentMessagePatientTextTests
{
    // TEST-BB-1189: a QC sample's instrument patient ID is not shown as a patient on the message list (pass 166).
    [Fact]
    public void For_QcSampleIsNotShownAsAPatient()
    {
        Assert.Equal(("— QC", "Not a patient: QC sample. The instrument sent patient ID 1171984.", true),
            InstrumentMessagePatientText.For(InstrumentMessageStatus.QualityControl, "1171984"));
        Assert.Equal(("MRN0022", (string?)null, false), InstrumentMessagePatientText.For(InstrumentMessageStatus.Processed, "MRN0022"));
    }
}
