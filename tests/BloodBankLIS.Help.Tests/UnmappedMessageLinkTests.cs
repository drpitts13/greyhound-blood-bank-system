using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

// TEST-BB-1302 (end-user testing pass 276): an unmapped value's last message opens the HL7 log searched for its control
// ID, or Instrument Interfaces searched for the instrument's sample ID; no reference, no link.
public class UnmappedMessageLinkTests
{
    [Fact]
    public void Href_OpensTheMessageWhereItCameIn()
    {
        Assert.Equal("/hl7?search=UAT-ORM-CA-3", UnmappedMessageLink.Href("UAT-ORM-CA-3", fromInstrument: false));
        Assert.Equal("/instruments?search=QC-ABSC-POS", UnmappedMessageLink.Href("Specimen QC-ABSC-POS", fromInstrument: true));
        Assert.Null(UnmappedMessageLink.Href(" ", fromInstrument: false));
    }
}
