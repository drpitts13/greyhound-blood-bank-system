using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class PatientActivityLabelTests
{
    [Fact]
    public void Format_NamedPatient_UsesLastAndFirst()
    {
        Assert.Equal("Doe, Jane", PatientActivityLabel.Format(12, "  Doe  ", " Jane "));
        Assert.Equal("Doe, Jane (MRN0001)", PatientActivityLabel.Format(12, "Doe", "Jane", " MRN0001 "));
    }

    [Fact]
    public void Format_PartialName_DoesNotInventTheOtherPart()
    {
        Assert.Equal("Doe", PatientActivityLabel.Format(12, "Doe", "   "));
        Assert.Equal("Jane", PatientActivityLabel.Format(12, null, "Jane"));
    }

    [Fact]
    public void Format_MissingOrBlankName_KeepsThePatientId()
    {
        Assert.Equal("patient #12", PatientActivityLabel.Format(12, null, null));
        Assert.Equal("patient #12", PatientActivityLabel.Format(12, "   ", "", "  "));
        Assert.Equal("patient #12 (MRN0001)", PatientActivityLabel.Format(12, "   ", null, "MRN0001"));
    }
}
